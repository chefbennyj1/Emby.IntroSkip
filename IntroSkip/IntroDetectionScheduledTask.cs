using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;


namespace IntroSkip
{
    public class IntroDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log             { get; set; }
        private ILibraryManager LibraryManager { get; }
        
        private static double Step = 0.0;
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
        }
        

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Beginning Intro Task");
            var config = Plugin.Instance.Configuration;
            
            //var introData = config.Intros;

            //All our series (...even the ones emby likes to ghost in the database)
            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { "Series" },
                Limit = 5 //This limit can be removed after extensive testing. If we remove this it will run the task on the entire library
            });

            Log.Info($"Series Result has {seriesQuery.TotalRecordCount} items");

            foreach (var seriesItem in seriesQuery.Items)
            {
                Log.Info($"Starting intro scan: {seriesItem.Name}");
                
                //All the episodes from a particular series
                var episodesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
                {
                    Parent = seriesItem,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Episode" }
                });

                // Our data length for the series episode count doesn't equal what emby says is in the library.
                // Something is new
                if (config.Intros is null || !IsCompleteSeriesData(config.Intros, seriesItem.InternalId, episodesQuery.TotalRecordCount))
                {
                    Log.Info($"Episode Result has {episodesQuery.TotalRecordCount} items");
                    foreach (var i in episodesQuery.Items)
                    {
                        Log.Info(i.Name);
                    }

                    if(episodesQuery.TotalRecordCount <= 0) continue; //<--Classic Emby ghost series
                    
                    var episodeIntroData = new List<TitleSequenceDataService.EpisodeIntroDto>();
                    
                    (int episodeIndexComparable, int episodeIndexToCompare) = new Tuple<int, int>(0, 1);
                    
                    var episodes = episodesQuery.Items;

                    //If the Series doesn't have an entry in our saved intro data - attempt to create intro data for all episodes in the series
                    if (config.Intros?.FirstOrDefault(s => s.SeriesInternalId == seriesItem.InternalId) is null)
                    {
                        Log.Info("Begin step through episodes");
                        //Step though the index of episodes
                        Log.Info($"First episodes to compare: {episodeIndexToCompare} and {episodeIndexComparable}");
                        
                        while (episodeIndexToCompare <= episodesQuery.TotalRecordCount -1)
                        {
                            try
                            {
                                var data = await Task.FromResult(IntroDetection.Instance.CompareAudioFingerPrint(episodes[episodeIndexComparable], episodes[episodeIndexToCompare]));
                                foreach (var item in data)
                                {
                                    if (!episodeIntroData.Contains(item))
                                    {
                                        episodeIntroData.Add(item);
                                    }
                                }
                                episodeIndexToCompare++;
                            }
                            catch (InvalidIntroDetectionException)
                            {
                                episodeIndexComparable++; //2
                                episodeIndexToCompare++;  //3
                                //We've skipped 1, we need to return to calculate it.
                                var data = await Task.FromResult(IntroDetection.Instance.CompareAudioFingerPrint(episodes[episodeIndexComparable], episodes[episodeIndexToCompare]));
                                foreach (var item in data)
                                {
                                    if (!episodeIntroData.Contains(item))
                                    {
                                        episodeIntroData.Add(item);
                                    }
                                }
                            }
                        }
                        
                    }

                    // There is an entry in our data for this series, but it is not complete, create the missing episode entry
                    // (Most likely a new episode has been added to the library)
                    if (!(config.Intros.FirstOrDefault(s => s.SeriesInternalId == seriesItem.InternalId) is null))
                    {
                        var episodeQuery = config.Intros.Where(s => s.SeriesInternalId == seriesItem.InternalId);
                        foreach (var episode in episodes)
                        {
                            //Identify the missing episode
                            if (!episodeQuery.Any(e => e.InternalId == episode.InternalId))
                            {
                                //Create Missing Episode Intro Data using "episode"
                                //We have to Take(2) episodes at a time, the new one, and one of the episodes in the library.
                            }
                        }
                    }

                    foreach (var item in episodeIntroData)
                    {
                        Log.Info(item.InternalId.ToString());
                        Log.Info($"IntroStart: {item.IntroStart}");
                        Log.Info($"IntroEnd: {item.IntroEnd}");
                    }

                    config.Intros.AddRange(episodeIntroData);
                    Plugin.Instance.UpdateConfiguration(config);

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

        public string Name        => "Detect Episode Intro Skip";
        public string Key         => "Intro Skip Options";
        public string Description => "Detect start and finish time of episode title sequences to allow for a 'skip' option";
        public string Category    =>  "Detect Episode Intro Skip";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;
    }
}

