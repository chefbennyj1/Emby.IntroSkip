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

namespace IntroSkip.RemoteControl
{
    public class AutoSkip : IServerEntryPoint
    {
        private readonly ConcurrentDictionary<string, BaseSequence> AutoTitleSequenceSkipSessions = new ConcurrentDictionary<string, BaseSequence>();
        private readonly ConcurrentDictionary<string, BaseSequence> AutoCreditSequenceSkipSessions = new ConcurrentDictionary<string, BaseSequence>();

        private ISessionManager SessionManager { get; }
        private IUserManager UserManager { get; }
        private ILogger Log { get; }
        public AutoSkip(ISessionManager sessionManager, IUserManager userManager, ILogManager logMan)
        {
            Log = logMan.GetLogger(Plugin.Instance.Name);
            SessionManager = sessionManager;
            UserManager = userManager;
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
            //throw new System.NotImplementedException();
        }
        
        private enum SequenceSkip 
        {
            INTRO  = 0,
            CREDIT = 1
        }
        private void SkipSequence(SessionInfo session, long seek, SequenceSkip sequenceSkip) 
        {
            SessionManager.SendPlaystateCommand(null, session.Id, new PlaystateRequest()
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

            //We have moved the stream to the end of the intro or credits. Remove it from the appropriate  Sequences Dictionary.
            switch (sequenceSkip)
            {
                case SequenceSkip.INTRO:
                    AutoTitleSequenceSkipSessions.TryRemove(session.Id, out _);
                    break;
                case SequenceSkip.CREDIT:
                    AutoCreditSequenceSkipSessions.TryRemove(session.Id, out _);
                    break;
            }

        }

        
        private void SessionManager_PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            //Check if we have data in our dictionaries. If not move on.
            if (!AutoCreditSequenceSkipSessions.ContainsKey(e.Session.Id) && !AutoTitleSequenceSkipSessions.ContainsKey(e.Session.Id)) return;

            BaseSequence titleSequence  = null;
            BaseSequence creditSequence = null;
            
            if (AutoTitleSequenceSkipSessions.ContainsKey(e.Session.Id)) titleSequence = AutoTitleSequenceSkipSessions[e.Session.Id];
            
            if (AutoCreditSequenceSkipSessions.ContainsKey(e.Session.Id)) creditSequence = AutoCreditSequenceSkipSessions[e.Session.Id];
            
            var episodeIndex = e.Item.IndexNumber;
            var seasonName   = e.Item.Parent.Name;
            var seriesName   = e.Item.Parent.Parent.Name;

            var presentationName = $"AUTOSKIP:{seriesName} - {seasonName} Episode {episodeIndex}";

            if (titleSequence != null)
            {
                if (e.PlaybackPositionTicks >= titleSequence.TitleSequenceStart.Ticks && e.PlaybackPositionTicks <= titleSequence.TitleSequenceEnd.Ticks)
                {
                    Log.Debug($"AUTOSKIP:{presentationName} skipping intro...");
                    //Seek the stream to the end of the intro
                    SkipSequence(e.Session, titleSequence.TitleSequenceEnd.Ticks, SequenceSkip.INTRO);
                    Log.Debug($"AUTOSKIP:{presentationName} intro has been skipped.");
                }
            }

            if (creditSequence != null)
            {
                if (e.PlaybackPositionTicks >= creditSequence.CreditSequenceStart.Ticks)
                {
                    Log.Debug($"AUTOSKIP:{presentationName} skipping credits...");
                    SkipSequence(e.Session, creditSequence.CreditSequenceEnd.Ticks, SequenceSkip.CREDIT);
                    Log.Debug($"AUTOSKIP:{presentationName} credit has been skipped.");
                }
            }
            
        }

        private void SessionManager_PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            if (AutoCreditSequenceSkipSessions.ContainsKey(e.Session.Id)) AutoCreditSequenceSkipSessions.TryRemove(e.Session.Id, out _);
            if (AutoTitleSequenceSkipSessions.ContainsKey(e.Session.Id)) AutoTitleSequenceSkipSessions.TryRemove(e.Session.Id, out _);
        }
        private void SessionManager_PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
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

                //If the intro starts at the beginning of the stream, or se started the stream in the middle of the intro.. act on it right away.
                if (sequence.CreditSequenceStart == TimeSpan.Zero || (e.PlaybackPositionTicks > sequence.TitleSequenceStart.Ticks && e.PlaybackPositionTicks < sequence.TitleSequenceEnd.Ticks))
                {
                    Log.Debug($"AUTOSKIP:{presentationName} skipping intro...");
                    //Seek the stream to the end of the intro
                    SkipSequence(e.Session, sequence.TitleSequenceEnd.Ticks, SequenceSkip.INTRO);
                    Log.Debug($"AUTOSKIP:{presentationName} intro has been skipped.");
                    
                }
                else
                {
                    PrepareTitleSequenceSkip(e, presentationName, sequence, config);
                }

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
            if (AutoCreditSequenceSkipSessions.ContainsKey(e.Session.Id)) return;
            
            Log.Debug($"AUTOSKIP:{presentationName} ready to skip credits.");
                    
            AutoCreditSequenceSkipSessions.TryAdd(e.Session.Id, sequence);
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

            //We are at the beginning of the stream
            //We want to ignore streams that are 'Resumed'
            if (e.PlaybackPositionTicks == 0 || (e.PlaybackPositionTicks <= sequence.TitleSequenceStart.Ticks))
            {
                Log.Debug($"AUTOSKIP:{presentationName} preparing intro skip...");
                if (AutoTitleSequenceSkipSessions.ContainsKey(e.Session.Id)) return;
                Log.Debug($"AUTOSKIP:{presentationName} ready to skip intro.");

                //Auto skip has a hard time skipping intros that start immediately at the 00:00:00 timestamp (or the very beginning)
                //we'll push the intro sequence start time a head by two seconds so that we can best skip the intro
                if (sequence.TitleSequenceStart == TimeSpan.Zero)
                {
                    sequence.TitleSequenceStart += TimeSpan.FromSeconds(2);
                }

                if (config.AutoSkipDelay.HasValue) sequence.TitleSequenceStart += TimeSpan.FromMilliseconds(config.AutoSkipDelay.Value);

                AutoTitleSequenceSkipSessions.TryAdd(e.Session.Id, sequence);
            }
        }

        private async void SendMessageToClient(SessionInfo session, SequenceSkip sequenceSkip)
        {
            var messageText = string.Empty;
            var config = Plugin.Instance.Configuration;
            switch (sequenceSkip)
            {
                case SequenceSkip.INTRO:
                    messageText = Localization.Languages[config.AutoSkipLocalization];
                    break;
                case SequenceSkip.CREDIT:
                    messageText = "Credits Skipped";
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
