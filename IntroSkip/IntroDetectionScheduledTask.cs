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
                    Log.Info(series.Name + " season: " + season.Name);
                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent = season,
                        Recursive = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
                    });

                    var episodeComparableIndex = 1;
                    var workingEpisodeSet = new List<BaseItem>();
                    foreach (var episode in episodeQuery.Items)
                    {
                        if (string.IsNullOrEmpty(episode.Path)) continue;
                        if (config.Intros.Exists(e => e.InternalId == episode.InternalId)) continue;
                        workingEpisodeSet.Add(episode);
                    }
                    Log.Info("working episode set has: " + workingEpisodeSet.Count + " item(s).");

                    for(var i = 0; i <= workingEpisodeSet.Count -1; i++)
                    {
                        if (i == episodeComparableIndex) episodeComparableIndex++;
                        
                        if (config.Intros.Exists(e => e.InternalId == workingEpisodeSet[i].InternalId)) continue;

                        try
                        {
                            var data = await Task.FromResult(IntroDetection.Instance.CompareAudioFingerPrint(workingEpisodeSet[episodeComparableIndex], workingEpisodeSet[i]));

                            foreach (var data_point in data)
                            {
                                if (!config.Intros.Exists(intro => intro.InternalId == data_point.InternalId))
                                {
                                    config.Intros.Add(data_point);
                                }
                            }
                            
                            Plugin.Instance.UpdateConfiguration(config);
                            episodeComparableIndex = 1;
                            Log.Info("Episode Intro Data obtained successfully.");
                            

                        }
                        catch (InvalidIntroDetectionException)
                        {
                            Log.Info("Episode Intro Data obtained failed. Trying new episode match");
                            episodeComparableIndex ++; 
                            i = 0; 
                        }
                    }
                }
            }

            /*
            //All the episodes from a particular series
            var episodesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Episode" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                //Limit = limit,
                //StartIndex = config.StartIndex
            });

            var episodeItems = episodesQuery.Items; //.GroupBy(e => e.Parent.Id);

            
            var episodes = episodeItems.Where(episode => config.Intros.All(ep => ep.InternalId != episode.InternalId)).ToList();
            
            (int episodeComparableIndex, int episodeToCompareIndex) = new Tuple<int, int>(1, 0);

            Log.Info($"Scanning {episodesQuery.TotalRecordCount} episodes");
            var scanningEnabled = true;
           
            
            while(scanningEnabled)
            {
                if (config.Intros.Count() >= episodesQuery.TotalRecordCount - 1) scanningEnabled = false;

                if (episodes[episodeToCompareIndex].Parent.InternalId != episodes[episodeComparableIndex].Parent.InternalId)
                {
                    episodeComparableIndex++;
                }

                try
                {
                    var data = await Task.FromResult(IntroDetection.Instance.CompareAudioFingerPrint(episodes[episodeComparableIndex], episodes[episodeToCompareIndex]));
                    
                    var newIntroItem = data.FirstOrDefault(dataPoint => config.Intros.All(item => item.InternalId != dataPoint.InternalId));
                    
                    config.Intros.Add(newIntroItem);
                    Plugin.Instance.UpdateConfiguration(config);
                    //episodeIntroData.Add(data.FirstOrDefault(dataPoint => episodeIntroData.All(item => item.InternalId != dataPoint.InternalId)));
                    
                    Log.Info("Episode Intro Data obtained successfully.");
                    //Skip over the episode if the indexes are the same.
                    if (episodeToCompareIndex == episodeComparableIndex)
                    {
                        episodeToCompareIndex += 2;
                    }
                    else
                    {
                        episodeToCompareIndex += 1;
                    }

                }
                catch (InvalidIntroDetectionException)
                {
                    Log.Info("Episode Intro Data obtained failed. Trying new episode match");
                    episodeComparableIndex = episodeToCompareIndex ++; 
                    episodeToCompareIndex = 0; 
                }

                //Stop the scan if we have made it through all the elements.
                if (episodeComparableIndex > episodesQuery.TotalRecordCount || episodeToCompareIndex > episodesQuery.TotalRecordCount ) scanningEnabled = false;
                

            }

          

            //config.Intros.AddRange(episodeIntroData);
            config.StartIndex += limit;

            Plugin.Instance.UpdateConfiguration(config);
              */
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

        public string Name        => "Detect Episode Intro Skip";
        public string Key         => "Intro Skip Options";
        public string Description => "Detect start and finish time of episode title sequences to allow for a 'skip' option";
        public string Category    =>  "Detect Episode Intro Skip";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;
    }
}

