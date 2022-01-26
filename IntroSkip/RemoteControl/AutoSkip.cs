using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using IntroSkip.Configuration;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;
using MediaBrowser.Model.Tasks;

namespace IntroSkip.RemoteControl
{
    public class SessionAutoSkip
    {
        public BaseSequence Sequence { get; set; }
        public Timer PlaybackMonitor { get; set; }
    }
    public class AutoSkip : IServerEntryPoint
    {
        private readonly ConcurrentDictionary<string, SessionAutoSkip> TitleSequenceAutoSkipSessions  = new ConcurrentDictionary<string, SessionAutoSkip>();
        private readonly ConcurrentDictionary<string, SessionAutoSkip> CreditSequenceAutoSkipSessions = new ConcurrentDictionary<string, SessionAutoSkip>();

        private ISessionManager SessionManager { get; }
        private IUserManager UserManager { get; }
        private ILogger Log { get; }
        private ITaskManager TaskManager { get; }
        public AutoSkip(ISessionManager sessionManager, IUserManager userManager, ILogManager logMan, ITaskManager taskManager)
        {
            Log = logMan.GetLogger(Plugin.Instance.Name);
            SessionManager = sessionManager;
            UserManager = userManager;
            TaskManager = taskManager;
        }
        public void Dispose()
        {
            SessionManager.PlaybackStart    -= SessionManager_PlaybackStart;
            SessionManager.PlaybackProgress -= SessionManager_PlaybackProgress;
            SessionManager.PlaybackStopped  -= SessionManager_PlaybackStopped;
        }

        public void Run()
        {
            SessionManager.PlaybackStart    += SessionManager_PlaybackStart;
            SessionManager.PlaybackProgress += SessionManager_PlaybackProgress;
            SessionManager.PlaybackStopped  += SessionManager_PlaybackStopped;
        }
        
        private enum SequenceSkip 
        {
            INTRO  = 0,
            CREDIT = 1
        }
        private async void SkipSequence(SessionInfo session, long seek, SequenceSkip sequenceSkip) 
        {
            Log.Debug($"AutoSkip: {sequenceSkip} - {session.Client} - Episode {session.FullNowPlayingItem.Name}");

            await SessionManager.SendPlaystateCommand(null, session.Id, new PlaystateRequest()
            {
                Command           = PlaystateCommand.Seek,
                ControllingUserId = UserManager.Users.FirstOrDefault(u => u.Policy.IsAdministrator)?.Id.ToString(),
                SeekPositionTicks = seek

            }, CancellationToken.None);

            if (Plugin.Instance.Configuration.ShowAutoTitleSequenceSkipMessage)
            {
                Log.Debug($"AUTOSKIP:Sending Auto Skip message to {session.Client}");
                SendMessageToClient(session, sequenceSkip);
                Log.Debug($"AUTOSKIP:Auto Skip message to {session.Client} was successful.");
            }

            //We have moved the stream to the end of the intro or credits. Remove it from the appropriate Sequences Dictionary.
            switch (sequenceSkip)
            {
                case SequenceSkip.INTRO:
                    TitleSequenceAutoSkipSessions.TryRemove(session.Id, out _);
                    break;
                case SequenceSkip.CREDIT:
                    CreditSequenceAutoSkipSessions.TryRemove(session.Id, out _);
                    break;
            }

        }

        
        private void SessionManager_PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            //Check if we have data in our dictionaries. If not move on.
            if (!CreditSequenceAutoSkipSessions.ContainsKey(e.Session.Id) && !TitleSequenceAutoSkipSessions.ContainsKey(e.Session.Id)) return;

            if (e.Session.PlayState.IsPaused)
            {
                if (!TitleSequenceAutoSkipSessions.ContainsKey(e.Session.Id)) return;
                var titleSequenceAutoSkipData = TitleSequenceAutoSkipSessions[e.Session.Id];
                titleSequenceAutoSkipData.PlaybackMonitor.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else //Sync the progress with our monitor
            {
                if (TitleSequenceAutoSkipSessions.ContainsKey(e.Session.Id))
                {
                    var sequenceAutoSkipData = TitleSequenceAutoSkipSessions[e.Session.Id];
                    sequenceAutoSkipData.PlaybackMonitor.Change(
                        (int) TimeSpan.FromTicks(sequenceAutoSkipData.Sequence.TitleSequenceStart.Ticks - e.PlaybackPositionTicks.Value).TotalMilliseconds, Timeout.Infinite);

                }
            
                if (CreditSequenceAutoSkipSessions.ContainsKey(e.Session.Id))
                {
                    var sequenceAutoSkipData = CreditSequenceAutoSkipSessions[e.Session.Id];
                    sequenceAutoSkipData.PlaybackMonitor.Change(
                       (int) TimeSpan.FromTicks(sequenceAutoSkipData.Sequence.CreditSequenceStart.Ticks - e.PlaybackPositionTicks.Value).TotalMilliseconds, Timeout.Infinite);
                }
            }

        }

        private void SessionManager_PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            if (CreditSequenceAutoSkipSessions.ContainsKey(e.Session.Id)) 
                if(!CreditSequenceAutoSkipSessions.TryRemove(e.Session.Id, out _)) Log.Debug("Unable to remove Session ID from Credit Auto Skip Session List.");

            if (TitleSequenceAutoSkipSessions.ContainsKey(e.Session.Id)) 
                if(!TitleSequenceAutoSkipSessions.TryRemove(e.Session.Id, out _)) Log.Debug("Unable to remove Session ID from Intro Auto Skip Session List.");
        }
        
        private void SessionManager_PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            //We can not use Auto Skip while Detection or Fingerprinting tasks are running. Possibly because we need to access the database.
            var tasks = TaskManager.ScheduledTasks.ToList();
            if (tasks.FirstOrDefault(task => task.Name == "Episode Title Sequence Detection")?.State == TaskState.Running)
            {
                Log.Info("Auto Skip will not be enabled while the Detection Task is running.");
                return;
            }
            if (tasks.FirstOrDefault(task => task.Name == "Episode Audio Fingerprinting")?.State == TaskState.Running)
            {
                Log.Info("Auto Skip will not be enabled while the Fingerprinting Task is running.");
                return;
            }

            //We only want tv episodes
            if(e.Item.GetType().Name != nameof(Episode)) return;
            
            var config = Plugin.Instance.Configuration;
            
            if (!config.EnableAutoSkipTitleSequence && !config.EnableAutoSkipCreditSequence) return; //Both features are not enabled.
            if (config.AutoSkipUsers is null) return;                                                //No users have opt'd in to the feature
            if (!config.AutoSkipUsers.Contains(e.Session.UserId)) return;                            //The list of opt'd users does not contain this sessions user.
            

            var episodeIndex = e.Item.IndexNumber;
            var seasonName   = e.Item.Parent.Name;
            var seriesName   = e.Item.Parent.Parent.Name;

            var presentationName = $"{seriesName} - {seasonName} Episode {episodeIndex}";

            Log.Debug($"AUTOSKIP:Playback start - { presentationName }");
            
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            
            var sequence = repository.GetBaseTitleSequence(e.Item.InternalId.ToString());

            
            if ((sequence.HasCreditSequence || sequence.CreditSequenceStart > TimeSpan.Zero) && config.EnableAutoSkipCreditSequence)
            {
                Log.Debug($"{presentationName} has credit sequence data.");
                PrepareCreditSequenceSkip(e, presentationName, sequence);
            }
            else
            {
                Log.Debug($"AUTOSKIP:{ presentationName } will not skip credit sequence.");
            }

            
            if (sequence.HasTitleSequence && config.EnableAutoSkipTitleSequence)
            {
                Log.Debug($"{presentationName} has title sequence data.");

                PrepareTitleSequenceSkip(e, presentationName, sequence, config);

            } 
            else
            {
                Log.Debug($"AUTOSKIP:{ presentationName } will not skip title sequence.");
            }

            var repo = repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }

        private void PrepareCreditSequenceSkip(PlaybackProgressEventArgs e, string presentationName, BaseSequence sequence)
        {
            //Stream is not near the credit sequence, and is not near the end of the stream. We are good to ready the credit sequence skip.
            if (e.PlaybackPositionTicks > sequence.CreditSequenceStart.Ticks || e.PlaybackPositionTicks > (e.Item.RunTimeTicks - TimeSpan.FromSeconds(10).Ticks)) return;

            Log.Debug($"AUTOSKIP:{presentationName} preparing credit skip...");
            if (CreditSequenceAutoSkipSessions.ContainsKey(e.Session.Id)) return;
            
            Log.Debug($"AUTOSKIP:{presentationName} ready to skip credits.");
            
            var sessionAutoSkip = new SessionAutoSkip()
            {
                Sequence = sequence,
                PlaybackMonitor = new Timer(sender =>
                {
                    SkipSequence(e.Session, sequence.CreditSequenceEnd.Ticks, SequenceSkip.CREDIT);

                }, null, Timeout.Infinite, Timeout.Infinite)
            };

            CreditSequenceAutoSkipSessions.TryAdd(e.Session.Id, sessionAutoSkip);
        }

        private void PrepareTitleSequenceSkip(PlaybackProgressEventArgs e, string presentationName, BaseSequence sequence, PluginConfiguration config)
        {
            //It is episode one of a season, and the option to show the title sequence has been set to true
            if (e.Item.IndexNumber == 1)
            {
                if (config.IgnoreEpisodeOneTitleSequenceSkip)
                {
                    Log.Debug($"AUTOSKIP:Will not skip intro for { presentationName }.");
                    return;
                }
            }

            //If the stream started in the middle of the intro.. act on it right away.
            if (e.PlaybackPositionTicks > sequence.TitleSequenceStart.Ticks && e.PlaybackPositionTicks < sequence.TitleSequenceEnd.Ticks)
            {
                Log.Debug($"AUTOSKIP:{presentationName} skipping intro...");
                //Seek the stream to the end of the intro
                SkipSequence(e.Session, sequence.TitleSequenceEnd.Ticks, SequenceSkip.INTRO);
                Log.Debug($"AUTOSKIP:{presentationName} intro has been skipped.");
                return;
            }
            
            
            //We want to ignore streams that are 'Resumed'
            if (e.PlaybackPositionTicks >= sequence.TitleSequenceEnd.Ticks) return;

            //Already added
            if (TitleSequenceAutoSkipSessions.ContainsKey(e.Session.Id)) return;

            Log.Debug($"AUTOSKIP:{presentationName} preparing intro skip...");
            Log.Debug($"AUTOSKIP:{presentationName} ready to skip intro.");

            //Auto skip has a hard time skipping intros that start immediately at the 00:00:00 timestamp (or the very beginning)
            //we'll push the intro sequence start time a head by two seconds so that we can best skip the intro
            if (sequence.TitleSequenceStart == TimeSpan.Zero) sequence.TitleSequenceStart += TimeSpan.FromSeconds(1);
            

            //if (config.AutoSkipDelay.HasValue) sequence.TitleSequenceStart += TimeSpan.FromMilliseconds(config.AutoSkipDelay.Value);

            var sessionAutoSkip = new SessionAutoSkip()
            {
                Sequence = sequence,
                PlaybackMonitor = new Timer(sender =>
                {
                    SkipSequence(e.Session, sequence.TitleSequenceEnd.Ticks, SequenceSkip.INTRO);

                }, null, Timeout.Infinite, Timeout.Infinite)
            };

            TitleSequenceAutoSkipSessions.TryAdd(e.Session.Id, sessionAutoSkip);
        }

        private async void SendMessageToClient(SessionInfo session, SequenceSkip sequenceSkip)
        {
            if (!session.Capabilities.SupportedCommands.Contains("DisplayMessage")) return;
            if (session.Client.Contains("Apple") || session.Client.Contains("Roku SG")) return;
            var messageText = string.Empty;
            var config = Plugin.Instance.Configuration;
            switch (sequenceSkip)
            {
                case SequenceSkip.INTRO:
                    messageText = Localization.IntroSkipLanguages[config.AutoSkipLocalization];
                    break;
                case SequenceSkip.CREDIT:
                    messageText = Localization.CreditSkipLanguages[config.AutoSkipLocalization];
                    break;
            }
           
            await SessionManager.SendMessageCommand(session.Id, session.Id,
                new MessageCommand
                {
                    Header = "",
                    Text = messageText,
                    TimeoutMs = Plugin.Instance.Configuration.AutoTitleSequenceSkipMessageDuration ?? 800

                }, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
