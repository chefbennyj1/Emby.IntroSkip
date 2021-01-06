using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.AudioFingerprinting;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

// ReSharper disable TooManyChainedReferences

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceDetectionManager : IServerEntryPoint
    {
        private static ILogger Log                           { get; set; }
        private ILibraryManager LibraryManager               { get; }
        private IUserManager UserManager                     { get; }
        public static TitleSequenceDetectionManager Instance { get; private set; }

        public TitleSequenceDetectionManager(ILogManager logManager, ILibraryManager libMan, IUserManager user)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager = user;
            Instance = this;
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, long[] seriesInternalIds)
        {
            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive = true,
                ItemIds = seriesInternalIds,
                IncludeItemTypes = new[] { "Series" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)

            }); 
            
            seriesQuery.Items.ToList().ForEach(item =>  TitleSequenceFileManager.Instance.RemoveSeriesTitleSequenceData(item));
          
            Analyze(seriesQuery, cancellationToken, progress);
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            Analyze(seriesQuery, cancellationToken, progress);
        }

        // ReSharper disable once ExcessiveIndentation
        // ReSharper disable once TooManyArguments
      
        private void Analyze(QueryResult<BaseItem> seriesQuery, CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance.Configuration;
            var currentProgress = 0.2;
            var step = 100.0 / seriesQuery.TotalRecordCount;

            Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, (series, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    state.Break();
                    progress.Report(100.0);
                }

                progress?.Report((currentProgress += step) - 1);
                

                var titleSequence = TitleSequenceFileManager.Instance.GetTitleSequenceFromFile(series);

                var titleSequenceSeasons = titleSequence.Seasons is null ? new List<Season>() : titleSequence.Seasons.ToList();

                var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    Parent = series,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Season" },
                    User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                    IsVirtualItem = false

                });

                foreach (var season in seasonQuery.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    Season titleSequenceSeason = null;
                    if (titleSequenceSeasons.Exists(s => s.IndexNumber == season.IndexNumber))
                    {
                        titleSequenceSeason = titleSequenceSeasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber);
                    }
                    else
                    {
                        titleSequenceSeason = new Season()
                        {
                            IndexNumber = season.IndexNumber
                        };
                    }
                      

                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent = season,
                        Recursive = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem = false
                    });

                    
                    var episodes = titleSequenceSeason.Episodes is null ? new List<Episode>() : titleSequenceSeason.Episodes.ToList();
                    
                    var exceptIds = new HashSet<long>(episodes.Select(y => y.InternalId).Distinct());
                    var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();

                    if (!unmatched.Any())
                    {
                        Log.Info($"{season.Parent.Name} S: {season.IndexNumber} OK.");
                        continue;
                    }

                    Log.Info($"{season.Parent.Name} S: {season.IndexNumber} has {unmatched.Count()} episodes to scan...\n");
                    

                    for (var index = 0; index <= unmatched.Count() - 1; index++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        //Compare the unmatched baseItem with every other item ion the season.
                        for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            var unmatchedItem = unmatched[index];
                            var comparableItem = episodeQuery.Items[episodeComparableIndex];

                            //Don't compare the same episode with itself.
                            if (comparableItem.InternalId == unmatchedItem.InternalId)
                            {

                                ////We can't compare an episode with itself, however it is the final episode in the comparing list, and it doesn't already exist, save the false intro
                                if (episodeComparableIndex == episodeQuery.Items.Count() - 1)
                                {
                                    if (!episodes.Exists(item => item.InternalId == unmatchedItem.InternalId))
                                    {
                                        episodes.Add(new Episode()
                                        {
                                            IndexNumber = unmatchedItem.IndexNumber,
                                            HasIntro = false,
                                            InternalId = unmatchedItem.InternalId
                                        });

                                        break;
                                    }
                                }
                                continue;
                            }

                            //If we have valid titles sequences data for both items move on
                            // ReSharper disable twice ComplexConditionExpression
                            if (episodes.Any(item => item.InternalId == unmatchedItem.InternalId) && episodes.Any(item => item.InternalId == comparableItem.InternalId))
                            {
                                if (episodes.FirstOrDefault(i => i.InternalId == unmatchedItem.InternalId).HasIntro && episodes.FirstOrDefault(i => i.InternalId == comparableItem.InternalId).HasIntro)
                                {
                                    continue;
                                }
                            }

                            try
                            {
                                //The magic!
                                var data = TitleSequenceDetection.Instance.DetectTitleSequence(episodeQuery.Items[episodeComparableIndex], unmatched[index]);

                                foreach (var dataPoint in data)
                                {
                                    if (episodes.Exists(item => item.IndexNumber == dataPoint.IndexNumber))
                                    {
                                        episodes.RemoveAll(item => item.IndexNumber == dataPoint.IndexNumber);
                                    }

                                    episodes.Add(dataPoint);
                                    var found = episodeQuery.Items.FirstOrDefault(item => item.InternalId == dataPoint.InternalId);
                                    Log.Info($"{found.Parent.Parent.Name} S: {found.Parent.IndexNumber} E: {found.IndexNumber} title sequence obtained successfully.");
                                }

                            }
                            catch (TitleSequenceInvalidDetectionException)
                            {
                                if (episodeComparableIndex != episodeQuery.Items.Count() - 1) continue;

                                //We have exhausted all our episode comparing

                                if (episodes.Exists(item => item.InternalId == unmatchedItem.InternalId)) continue;

                                Log.Info($"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} currently has no title sequence.");

                                episodes.Add(new Episode()
                                {
                                    IndexNumber = unmatchedItem.IndexNumber,
                                    HasIntro = false,
                                    InternalId = unmatchedItem.InternalId
                                });
                            }
                            catch (AudioFingerprintMissingException ex)
                            {
                                Log.Info($"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} {ex}");
                            }
                        }
                    }

                    titleSequenceSeason.Episodes = episodes;

                    if (titleSequenceSeasons.Exists(item => item.IndexNumber == season.IndexNumber))
                    {
                        titleSequenceSeasons.RemoveAll(item => item.IndexNumber == titleSequenceSeason.IndexNumber);
                    }

                    titleSequenceSeasons.Add(titleSequenceSeason);
                    

                    titleSequence.Seasons = (titleSequenceSeasons);

                    TitleSequenceFileManager.Instance.SaveTitleSequenceJsonToFile(series, titleSequence);
                }

            });

        }
        

        public void Dispose()
        {

        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {

        }
    }
}
