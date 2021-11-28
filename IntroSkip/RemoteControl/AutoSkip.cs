using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Session;

namespace IntroSkip.RemoteControl
{
    public class AutoSkip : IServerEntryPoint
    {
        private ISessionManager SessionManager { get; set; }
        private IUserManager UserManager { get; set; }
        private ILogger Log { get; set; }
        public AutoSkip(ISessionManager sessionManager, IUserManager userManager, ILogManager logMan)
        {
            SessionManager = sessionManager;
            UserManager = userManager;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }
        public void Dispose()
        {
            SessionManager.PlaybackStart -= SessionManager_PlaybackStart;
            SessionManager.PlaybackProgress -= SessionManager_PlaybackProgress;
            SessionManager.PlaybackStopped -= SessionManager_PlaybackStopped;
        }

        public void Run()
        {
            SessionManager.PlaybackStart += SessionManager_PlaybackStart;
            SessionManager.PlaybackProgress += SessionManager_PlaybackProgress;
            SessionManager.PlaybackStopped += SessionManager_PlaybackStopped;
            //throw new System.NotImplementedException();
        }
        
        private void SkipIntro(SessionInfo session, long seek) 
        {
            SessionManager.SendPlaystateCommand(null, session.Id, new PlaystateRequest()
            {
                Command = PlaystateCommand.Seek,
                ControllingUserId = UserManager.Users.FirstOrDefault(u => u.Policy.IsAdministrator)?.Id.ToString(),
                SeekPositionTicks = seek
            }, CancellationToken.None);

            if (Plugin.Instance.Configuration.ShowAutoTitleSequenceSkipMessage)
            {
                SendMessageToClient(session);
            }
            //We have moved the stream to the end of the intro. Remove it from the Sequences Dictionary.
            SessionSequences.Remove(session.Id);
        }

        private Dictionary<string, BaseSequence> SessionSequences = new Dictionary<string, BaseSequence>();
        private void SessionManager_PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            //This session does not exists in our sequence dictionary.
            //It either has no title sequence data, or the stream was resumed, and we are past the title sequence
            //move on.
            if (!SessionSequences.ContainsKey(e.Session.Id)) return;

            //Here is the sequence data for the sessions currently playing item.
            var sequence = SessionSequences[e.Session.Id];

            if (e.PlaybackPositionTicks >= sequence.TitleSequenceStart.Ticks && e.PlaybackPositionTicks <= sequence.TitleSequenceEnd.Ticks)
            {
                Log.Info("INTRO SKIP: skipping intro...");
                //Seek the stream to the end of the intro
                SkipIntro(e.Session, sequence.TitleSequenceEnd.Ticks);
                Log.Info("INTRO SKIP: intro has been skipped");
            }
        }

        private void SessionManager_PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            var config = Plugin.Instance.Configuration;
            //This feature is not enabled
            if (!config.EnableAutoSkipTitleSequence) return;

            //This user does not have this feature enabled.
            if (config.AutoSkipUsers != null)
            {
                if (!config.AutoSkipUsers.Contains(e.Session.UserId))
                {
                    return;
                }
            }

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var sequence = repository.GetBaseTitleSequence(e.Item.InternalId.ToString());
            Log.Info("INTRO SKIP: PLAYBACK STARTED");
            if (sequence.HasTitleSequence)
            { 
                Log.Info("INTRO SKIP: item has title sequence data.");
                //We are at the beginning of the stream
                //We want to ignore streams that are 'Resumed'
                if (e.PlaybackPositionTicks == 0 || (e.PlaybackPositionTicks <= sequence.CreditSequenceStart.Ticks))
                {
                    Log.Info("INTRO SKIP: preparing intro skip...");
                    if (!SessionSequences.ContainsKey(e.Session.Id))
                    {
                        Log.Info("INTRO SKIP: item ready.");
                        SessionSequences.Add(e.Session.Id, sequence);
                    }
                }
            }

            var repo = repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }

        private void SessionManager_PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            if (SessionSequences.ContainsKey(e.Session.Id))
            {
                SessionSequences.Remove(e.Session.Id);
            }
        }

        private async void SendMessageToClient(SessionInfo session)
        {
            await SessionManager.SendMessageCommand(session.Id, session.Id,
                new MessageCommand
                {
                    Header = "",
                    Text = "Intro Skipped",
                    TimeoutMs = 1000

                }, CancellationToken.None);
        }
    }
}
