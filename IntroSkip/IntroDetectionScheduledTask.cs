using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;


namespace IntroSkip
{
    public class IntroDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log                  { get; set; }
        private ILibraryManager LibraryManager      { get; }
        private IUserManager UserManager            { get; set; }
        private IFileSystem FileSystem { get; set; }
        public long CurrentSeriesEncodingInternalId { get; set; }

        private static double Step = 0.0;
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user, IFileSystem file)
        {
            Log            = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager    = user;
            FileSystem     = file;
        }
        
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Beginning Intro Task");
            var config = Plugin.Instance.Configuration;
            
            if (!FileSystem.DirectoryExists("../programdata/IntroEncodings"))
            {
                FileSystem.CreateDirectory("../programdata/IntroEncodings");
            }

            if (config.Intros is null)
            {
                config.Intros = new List<TitleSequenceDataService.EpisodeIntroDto>();
            }

            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
            });

            foreach (var series in seriesQuery.Items)
            {
                Log.Info(series.Name);
                var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    Parent = series,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Season" },
                    User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
                });
                foreach (var season in seasonQuery.Items)
                {
                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent = season,
                        Recursive = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem = false
                    });

                    var episodeComparableIndex = 1;
                    //DO a foreach loop here using indexAt something is wrong!!

                    for(var i = 0; i <= episodeQuery.Items.Count() - 1; i++)
                    {
                        if (config.Intros.Exists(e => e.InternalId == episodeQuery.Items[i].InternalId)) continue;
                        
                        //Don't compare the same episode with it's self
                        if (i == episodeComparableIndex) episodeComparableIndex++;
                        
                        Log.Info($"episodeComparableIndex: {episodeComparableIndex} i: {i}");
                        try
                        {
                            var data = await Task.FromResult(
                                IntroDetection.Instance.CompareAudioFingerPrint(
                                    episodeQuery.Items[episodeComparableIndex], episodeQuery.Items[i]));

                            foreach (var data_point in data)
                            {
                                if (!config.Intros.Exists(intro => intro.InternalId == data_point.InternalId))
                                {
                                    config.Intros.Add(data_point);
                                }
                            }

                            Plugin.Instance.UpdateConfiguration(config);

                            //If the episodeComparableIndex was changed because the episode would've compared itself, change it back;
                            if (i == (episodeComparableIndex -= 1)) episodeComparableIndex--;

                            Log.Info("Episode Intro Data obtained successfully.");
                        }
                        catch (InvalidIntroDetectionException)
                        {
                            Log.Info("Episode Intro Data obtained failed. Trying new episode match");
                            if (episodeComparableIndex <= episodeQuery.Items.Count() -1)
                            {
                                episodeComparableIndex++;
                            }
                            else
                            {
                                episodeComparableIndex = 1;
                                config.Intros.Add(new TitleSequenceDataService.EpisodeIntroDto()
                                {
                                    HasIntro = false,
                                    SeriesInternalId = series.InternalId,
                                    InternalId = episodeQuery.Items[i].InternalId
                                });
                            }
                           
                        } 
                    }
                }
            }

            
            progress.Report(100.0);
        }
        

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }


        //Do the amount of episodes we have scanned equal the amount of episodes in the Series 
        private bool IsCompleteSeriesData(List<TitleSequenceDataService.EpisodeIntroDto> introData, long seriesInternalId, int episodeCount)
        {
            if (introData is null) return false;
            if (!introData.Any()) return false;
            
            var seriesIntroData = introData.Select(s => s.SeriesInternalId == seriesInternalId);
            return episodeCount <= seriesIntroData.Count();
        }

        public string Name        => "Detect Episode Title Sequence";
        public string Key         => "Intro Skip Options";
        public string Description => "Detect start and finish times of episode title sequences to allow for a 'skip' option";
        public string Category    => "Detect Episode Title Sequence";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;
    }
}

