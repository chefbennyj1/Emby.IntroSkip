using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntroSkip.Data;
using IntroSkip.Sequence;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;

namespace IntroSkip.Statistics
{
    public class StatsManager : IServerEntryPoint
    {
        private ILogger Log { get; }
        private ILibraryManager LibraryManager { get; }
        private IFileSystem FileSystem { get; }
        private IApplicationPaths ApplicationPaths { get; }
        public static StatsManager Instance { get; set; }

        public static string statsFilePath;

        public static List<DetectionStats> ReturnedDetectionStatsList = new List<DetectionStats>();


        // ReSharper disable once TooManyDependencies
        public StatsManager(ILogManager logMan, ILibraryManager libraryManager, IFileSystem fileSystem, IApplicationPaths applicationPaths)
        {
            Log = logMan.GetLogger(Plugin.Instance.Name);
            LibraryManager = libraryManager;
            FileSystem = fileSystem;
            ApplicationPaths = applicationPaths;
            Instance = this;
        }

        public void GetDetectionStatistics()
        {
            ReturnedDetectionStatsList.Clear();
            var seriesList = new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                IsVirtualItem = false,
            };

            var seriesItems = LibraryManager.GetItemList(seriesList);
            //var seriesItemsCount = seriesItems.Count();

            Log.Info("STATISTICS: Series Count = {0}", seriesItems.Length.ToString());
            List<long> seasonIds = new List<long>();
            foreach (var season in seriesItems)
            {
                var seasonInternalItemQuery = new InternalItemsQuery()
                {
                    Parent = season,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Season" },
                    IsVirtualItem = false,
                };
                BaseItem[] seasonItems = LibraryManager.GetItemList(seasonInternalItemQuery);

                foreach (var id in seasonItems)
                {
                    seasonIds.Add(id.InternalId);

                }
            }
            Log.Info("STATISTICS: No of Seasons to process = {0}", seasonIds.Count.ToString());

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            try
            {
                foreach (var season in seasonIds)
                {
                    var query = new SequenceResultQuery() { SeasonInternalId = season };
                    var dbResults = repository.GetBaseTitleSequenceResults(query);

                    var seasonItem = LibraryManager.GetItemById(season);
                    var detectedSequences = dbResults.Items.ToList();

                    TimeSpan commonDuration;
                    try
                    {
                        commonDuration = CalculateCommonTitleSequenceLength(detectedSequences);
                    }
                    catch
                    {
                        commonDuration = new TimeSpan(0, 0, 0);
                    }


                    int hasIntroCount = 0;
                    int hasEndCount = 0;
                    int totalEpisodeCount = 0;

                    foreach (var episode in detectedSequences)
                    {
                        totalEpisodeCount++;

                        if (episode.HasTitleSequence)
                        {
                            hasIntroCount++;
                        }
                        if (episode.HasCreditSequence)
                        {
                            hasEndCount++;
                        }
                        else
                        {
                            hasIntroCount += 0;
                        }
                    }

                    //Probably only one episode so we can't detect data yet.
                    if (hasIntroCount == 0 && hasEndCount == 0)
                    {
                        ReturnedDetectionStatsList.Add(new DetectionStats
                        {
                            SeriesId = seasonItem.Parent.InternalId,
                            SeasonId = seasonItem.InternalId,
                            TVShowName = seasonItem.Parent.Name,
                            Season = seasonItem.Name,
                            EpisodeCount = totalEpisodeCount,
                            HasSeqCount = hasIntroCount,
                            PercentDetected = 0,
                            EndPercentDetected = 0,
                            IntroDuration = commonDuration,
                            HasIssue = false
                        });
                    }
                    //Hoping not using this will increase performance massively.
                    if (totalEpisodeCount == hasIntroCount)
                    {
                        int y = totalEpisodeCount;
                        int z = hasEndCount;
                        double creditPercentage = Math.Round((double)z / y * 100);
                        ReturnedDetectionStatsList.Add(new DetectionStats
                        {
                            SeriesId = seasonItem.Parent.InternalId,
                            SeasonId = seasonItem.InternalId,
                            TVShowName = seasonItem.Parent.Name,
                            Season = seasonItem.Name,
                            EpisodeCount = totalEpisodeCount,
                            HasSeqCount = hasIntroCount,
                            PercentDetected = 100,
                            EndPercentDetected = creditPercentage,
                            IntroDuration = commonDuration,
                            HasIssue = false
                        });
                    }

                    else
                    {
                        int x = hasIntroCount;
                        int y = totalEpisodeCount;
                        int z = hasEndCount;
                        double introPercentage = Math.Round((double)x / y * 100);
                        double creditPercentage = Math.Round((double)z / y * 100);

                        ReturnedDetectionStatsList.Add(new DetectionStats
                        {
                            SeasonId = seasonItem.InternalId,
                            SeriesId = seasonItem.Parent.InternalId,
                            TVShowName = seasonItem.Parent.Name,
                            Season = seasonItem.Name,
                            EpisodeCount = totalEpisodeCount,
                            HasSeqCount = hasIntroCount,
                            PercentDetected = introPercentage,
                            EndPercentDetected = creditPercentage,
                            IntroDuration = commonDuration,
                            HasIssue = true
                        });
                    }
                }


                DisposeRepository(repository);
                Log.Info("STATISTICS: Completed Statistics Successfully");
                ReturnedDetectionStatsList.Sort((x, y) => string.CompareOrdinal(x.TVShowName, y.TVShowName));
                //Sorts the Seasons and Tv Shows in Season order but not alphabetically for the Shows.
                // List<DetectionStats> list = ReturnedDetectionStatsList.OrderBy(x => x.TVShowName).ThenBy(x => x.Season).ToList();
                // ReturnedDetectionStatsList = list;

                CreateStatisticsTextFile();
            }
            catch (Exception thisIsPissingMeOffNow)
            {
                Log.Warn("STATISTICS: ******* ISSUE CREATING STATS FOR INTROSKIP *********");
                Log.ErrorException(thisIsPissingMeOffNow.Message, thisIsPissingMeOffNow);
            }
        }
        private TimeSpan CalculateCommonTitleSequenceLength(List<BaseSequence> season)
        {
            var titleSequences = season.Where(intro => intro.HasTitleSequence);
            var groups = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd - sequence.TitleSequenceStart);
            var enumerableSequences = groups.ToList();
            int maxCount = enumerableSequences.Max(g => g.Count());
            var mode = enumerableSequences.First(g => g.Count() == maxCount).Key;
            return mode;
        }

        private void DisposeRepository(ISequenceRepository repository)
        {
            // ReSharper disable once UsePatternMatching
            var repo = repository as IDisposable;
            repo?.Dispose();
        }

        private string GetIntroSkipInfoDir()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return Path.Combine(configDir, "IntroSkipInfo"); //$"{configDir}{Separator}IntroSkipInfo";
        }

        private void CreateStatisticsTextFile()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            Log.Debug("STATISTICS: Writing statistics to file");

            var stats = ReturnedDetectionStatsList;
            var filePath = Path.Combine(configDir, "IntroSkipInfo", "DetectionResults.txt");//$"{configDir}{Separator}IntroSkipInfo{Separator}DetectionResults.txt";
            statsFilePath = filePath;

            if (stats == null)
            {
                Log.Info("STATISTICS: NOTHING TO WRITE TO THE FILE");
            }
            else
            {
                using (StreamWriter writer = new StreamWriter(filePath, false))
                {
                    var delim = ("\t");
                    var headers = string.Join(delim, "Has Issue", "TV Show", "SeriesId", "Season", "SeasonId", "Episode Count", "Duration", "Intro Results", "EndCredit Results");
                    writer.WriteLine(headers);

                    foreach (var stat in stats)
                    {
                        bool issue = stat.HasIssue;
                        long seriesId = stat.SeriesId;
                        string show = stat.TVShowName;
                        string season = stat.Season;
                        long seasonId = stat.SeasonId;
                        int episode = stat.EpisodeCount;
                        TimeSpan duration = stat.IntroDuration;
                        double introResults = stat.PercentDetected;
                        double endCreditResults = stat.EndPercentDetected;

                        var statLine = string.Join(delim, issue, show, seriesId, season, seasonId, episode, duration, introResults, endCreditResults);
                        writer.WriteLine(statLine);
                    }
                }
            }
        }

        #region IServerEntryPoint Implemented Members

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void Run()
        {
            var errorDir = GetIntroSkipInfoDir();
            if (!FileSystem.DirectoryExists($"{errorDir}")) FileSystem.CreateDirectory($"{errorDir}");
        }

        #endregion
    }
}

