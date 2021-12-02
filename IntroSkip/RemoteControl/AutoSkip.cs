﻿using System;
using System.Collections.Concurrent;
using System.Globalization;
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
        private readonly ConcurrentDictionary<string, BaseSequence> AutoSkipSessions = new ConcurrentDictionary<string, BaseSequence>();
        private ISessionManager SessionManager { get; }
        private IUserManager UserManager { get; }
        private ILogger Log { get; }
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
                Log.Debug($"AUTOSKIP:Sending Intro skip message to {session.Client}");
                SendMessageToClient(session);
                Log.Debug($"AUTOSKIP:Intro skip message to {session.Client} was successful.");
            }
            //We have moved the stream to the end of the intro. Remove it from the Sequences Dictionary.
            AutoSkipSessions.TryRemove(session.Id, out _);
        }

        
        private void SessionManager_PlaybackProgress(object sender, PlaybackProgressEventArgs e)
        {
            //This session does not exists in our sequence dictionary.
            //It either has no title sequence data, or the stream was resumed, and we are past the title sequence
            //move on.
            if (!AutoSkipSessions.ContainsKey(e.Session.Id)) return;

            //Here is the sequence data for the sessions currently playing item.
            var sequence = AutoSkipSessions[e.Session.Id];
            var episodeIndex = e.Item.IndexNumber;
            var seasonName = e.Item.Parent.Name;
            var seriesName = e.Item.Parent.Parent.Name;

            var presentationName = $"AUTOSKIP:{seriesName} - {seasonName} Episode {episodeIndex}";
            if (e.PlaybackPositionTicks >= sequence.TitleSequenceStart.Ticks && e.PlaybackPositionTicks <= sequence.TitleSequenceEnd.Ticks)
            {
                Log.Debug($"AUTOSKIP:{presentationName} skipping intro...");
                //Seek the stream to the end of the intro
                SkipIntro(e.Session, sequence.TitleSequenceEnd.Ticks);
                Log.Debug($"AUTOSKIP:{presentationName} intro has been skipped.");
            }
        }

        private void SessionManager_PlaybackStart(object sender, PlaybackProgressEventArgs e)
        {
            //We only want tv episodes
            if(e.Item.GetType().Name != nameof(Episode))
            {
                return;
            }

            var config = Plugin.Instance.Configuration;
            
            if (!config.EnableAutoSkipTitleSequence) return;
            if (config.AutoSkipUsers is null) return;
            if (!config.AutoSkipUsers.Contains(e.Session.UserId)) return;
            

            var episodeIndex = e.Item.IndexNumber;
            var seasonName = e.Item.Parent.Name;
            var seriesName = e.Item.Parent.Parent.Name;

            var presentationName = $"AUTOSKIP:{seriesName} - {seasonName} Episode {episodeIndex}";

            Log.Debug($"AUTOSKIP:Playback start - { presentationName }");
            //It is episode one of a season, and the option to show the title sequence has been set to true
            if (e.Item.IndexNumber == 1)
            {
                if (Plugin.Instance.Configuration.IgnoreEpisodeOneTitleSequenceSkip)
                {
                    Log.Debug($"AUTOSKIP:Will not skip intro for { presentationName }.");
                    return;
                }
            }

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            
            var sequence = repository.GetBaseTitleSequence(e.Item.InternalId.ToString());
            
            if (sequence.HasTitleSequence)
            { 
                Log.Debug($"{ presentationName } has title sequence data.");
                //We are at the beginning of the stream
                //We want to ignore streams that are 'Resumed'
                if (e.PlaybackPositionTicks == 0 || (e.PlaybackPositionTicks <= sequence.CreditSequenceStart.Ticks))
                {
                    Log.Debug($"AUTOSKIP:{presentationName} preparing intro skip...");
                    if (!AutoSkipSessions.ContainsKey(e.Session.Id))
                    {
                        Log.Debug($"AUTOSKIP:{presentationName} ready to skip intro.");
                        AutoSkipSessions.TryAdd(e.Session.Id, sequence);
                    }
                }
            } else
            {
                Log.Debug($"AUTOSKIP:{ presentationName } has no title sequence data.");
            }

            var repo = repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }

        private void SessionManager_PlaybackStopped(object sender, PlaybackStopEventArgs e)
        {
            if (AutoSkipSessions.ContainsKey(e.Session.Id))
            {
                AutoSkipSessions.TryRemove(e.Session.Id, out _);
            }
        }

        private async void SendMessageToClient(SessionInfo session)
        {
            CultureInfo ci = CultureInfo.InstalledUICulture;
            Log.Debug(ci.Name);
            await SessionManager.SendMessageCommand(session.Id, session.Id,
                new MessageCommand
                {
                    Header = "",
                    Text = Localization.Languages[ci.Name],//"Intro Skipped",
                    TimeoutMs = Plugin.Instance.Configuration.AutoTitleSequenceSkipMessageDuration

                }, CancellationToken.None);
        }
    }
}
