using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;


namespace IntroSkip
{
    public class IntroDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log                  { get; set; }
        private ILibraryManager LibraryManager      { get; }
        private IUserManager UserManager            { get; set; }

        public long CurrentSeriesEncodingInternalId { get; set; }

        private static double Step = 0.0;
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager = user;
            
        }
        

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Beginning Intro Task");
            var config = Plugin.Instance.Configuration;
            var limit = 10;
            var episodeIntroData = new List<TitleSequenceDataService.EpisodeIntroDto>();
            //All the episodes from a particular series
            var episodesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Episode" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                Limit = limit,
                StartIndex = config.StartIndex
            });

            var episodes = episodesQuery.Items; //.GroupBy(e => e.Parent.Id);
            (int episodeIndexComparable, int episodeIndexToCompare) = new Tuple<int, int>(0, 1);

            Log.Info("Begin step through episodes");
            //Step though the index of episodes
            Log.Info($"First episodes to compare: {episodeIndexToCompare} and {episodeIndexComparable}");

            while (episodeIndexToCompare <= episodesQuery.TotalRecordCount - 1)
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



            var nextEpisodeSet = episodes.Where(episode => !config.Intros.Any(ep => ep.InternalId == episode.InternalId));
            
            foreach (var episode in nextEpisodeSet)
            {


            }


            config.Intros.AddRange(episodeIntroData);
            config.StartIndex += limit;

            Plugin.Instance.UpdateConfiguration(config);

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

