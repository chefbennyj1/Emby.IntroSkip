using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Data;
using IntroSkip.VideoBlackDetect;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

// ReSharper disable TooManyChainedReferences

namespace IntroSkip.Sequence
{
    public class SequenceDetectionManager : IServerEntryPoint
    {
        private static ILogger Log { get; set; }
        private ILibraryManager LibraryManager { get; }
        private IUserManager UserManager { get; }
        public static SequenceDetectionManager Instance { get; private set; }
        
        public SequenceDetectionManager(ILogManager logManager, ILibraryManager libMan, IUserManager user)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager = user;
            Instance = this;
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, long[] seriesInternalIds, ISequenceRepository repo)
        {
            var config = Plugin.Instance.Configuration;
            var seriesInternalItemQuery = new InternalItemsQuery()
            {
                Recursive = true,
                ItemIds = seriesInternalIds,
                IncludeItemTypes = new[] { "Series" },
                ExcludeItemIds = config.IgnoredList.ToArray(),
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            };

            var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);

            Analyze(seriesQuery, progress, cancellationToken, repo);
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress,ISequenceRepository repository)
        {
            var config = Plugin.Instance.Configuration;
            var seriesInternalItemQuery = new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
            };

            if (config.IgnoredList.Count > 0)
            {
                seriesInternalItemQuery.ExcludeItemIds = config.IgnoredList.ToArray();
            }

            var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);

            Analyze(seriesQuery, progress, cancellationToken, repository);
        }

        // ReSharper disable once ExcessiveIndentation
        // ReSharper disable once TooManyArguments

        private void Analyze(QueryResult<BaseItem> seriesQuery, IProgress<double> progress, CancellationToken cancellationToken, ISequenceRepository repository)
        {

            if (cancellationToken.IsCancellationRequested)
            {
                progress.Report(100.0);
            }
            var config = Plugin.Instance.Configuration;
            var currentProgress = 0.2;
            var step = 100.0 / seriesQuery.TotalRecordCount;

            
            
            Parallel.ForEach(seriesQuery.Items,
                new ParallelOptions() { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, (series, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {

                        state.Break();
                        progress.Report(100.0);
                    }

                    progress?.Report((currentProgress += step) - 1);

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

                        

                        //Our database info
                        QueryResult<SequenceResult> dbResults = null;
                        try
                        {

                            dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = season.InternalId });

                        }
                        catch (Exception ex)
                        {
                            Log.Warn(ex.Message);

                            continue;
                        }


                        var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                        {
                            Parent = season,
                            Recursive = true,
                            IncludeItemTypes = new[] { "Episode" },
                            User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                            IsVirtualItem = false
                        });


                        var dbEpisodes = dbResults.Items.ToList();

                        if (!dbEpisodes.Any()) //<-- this should not happen unless the fingerprint task was never run on this season. 
                        {
                            dbEpisodes = new List<SequenceResult>();
                        }

                        // An entire season with no title sequence might be the case. However, something else might have caused an entire season to have no results. - Warn the log.
                        if (dbEpisodes.All(item => item.HasTitleSequence == false) && dbEpisodes.All(item => item.Processed))
                        {
                            //Log.Warn($"{series.Name} {season.Name}: There currently are no title sequences available for this season.\n");
                        }

                        

                        //After processing, the DB entry is marked as 'processed'. if the item has been processed already, just move on.
                        if (dbEpisodes.All(item => item.Processed))
                        {
                            Log.Debug($"{series.Name} S:{season.IndexNumber} have no new episodes to scan.");
                            continue;
                        }


                        // All our processed episodes with sequences, or user confirmed information
                        var exceptIds = new HashSet<long>(dbEpisodes.Where(e => e.HasTitleSequence || e.Confirmed).Select(y => y.InternalId).Distinct());
                        // A list of episodes with all our episodes containing sequence data removed from it. All that is left is what we need to process.
                        var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                        if (!unmatched.Any()) //<-- this is redundant because we checked for 'processed' above. 
                        {
                            continue;
                        }

                        
                        
                        var fastDetect = Plugin.Instance.Configuration.FastDetect;
                        Log.Info($"Using: {(fastDetect ? "Fast Detection: ON" : " Fast Detection: OFF")} - Processing {unmatched.Count()} episode(s) for {season.Parent.Name} - {season.Name}.");
                        

                        var episodeResults = new ConcurrentDictionary<long, ConcurrentBag<SequenceResult>>();

                        unmatched.AsParallel().WithCancellation(cancellationToken).WithDegreeOfParallelism(2).ForAll((unmatchedItem) =>
                        {
                            //Compare the unmatched episode  with every other episode in the season until there is a match.
                            for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                            {

                                if (cancellationToken.IsCancellationRequested)
                                {
                                    break;
                                }
                                
                                var comparableItem = episodeQuery.Items[episodeComparableIndex];

                                //Don't compare the same episode with itself. The episodes must be different or we'll match the entire encoding.
                                if (comparableItem.InternalId == unmatchedItem.InternalId)
                                {
                                    continue;
                                }

                                if (!fastDetect)
                                {
                                    //Log.Debug($"Comparing {unmatchedItem.Parent.Parent.Name} {unmatchedItem.Parent.Name} E:{unmatchedItem.IndexNumber} to E:{ comparableItem.IndexNumber }");
                                }

                                if (fastDetect)
                                {
                                    // If we have valid title sequence data for both items move on
                                    if (dbEpisodes.Any(item => item.InternalId == unmatchedItem.InternalId) && dbEpisodes.Any(item => item.InternalId == comparableItem.InternalId))
                                    {
                                        var dbResultComparableItem = dbEpisodes.FirstOrDefault(i => i.InternalId == comparableItem.InternalId);
                                        var dbResultUnmatchedItem  = dbEpisodes.FirstOrDefault(i => i.InternalId == unmatchedItem.InternalId);
                                        
                                        if (dbResultUnmatchedItem.HasTitleSequence && dbResultUnmatchedItem.HasCreditSequence && 
                                            dbResultComparableItem.HasTitleSequence && dbResultComparableItem.HasCreditSequence)
                                        {
                                            continue;
                                        }
                                    }
                                }


                                try
                                {

                                    // The magic!
                                    var sequenceDetection = TitleSequenceDetection.Instance;

                                    var stopWatch = new Stopwatch();
                                    stopWatch.Start();

                                    var sequences = sequenceDetection.DetectSequences(episodeQuery.Items[episodeComparableIndex], unmatchedItem, dbResults, stopWatch);
                                    
                                    if (!fastDetect)
                                    {
                                        if (!episodeResults.ContainsKey(unmatchedItem.InternalId))
                                        {
                                            episodeResults.TryAdd(unmatchedItem.InternalId, new ConcurrentBag<SequenceResult>());
                                        }

                                        if (!episodeResults.ContainsKey(comparableItem.InternalId))
                                        {
                                            episodeResults.TryAdd(comparableItem.InternalId, new ConcurrentBag<SequenceResult>());
                                        }

                                        episodeResults[unmatchedItem.InternalId].Add(sequences.FirstOrDefault(s => s.InternalId == unmatchedItem.InternalId));
                                        episodeResults[comparableItem.InternalId].Add(sequences.FirstOrDefault(s => s.InternalId == comparableItem.InternalId));
                                    }

                                    else
                                    {
                                        foreach (var sequence in sequences)
                                        {
                                            //Just remove these entries in the episode list (if they exist) and add the new result back. Easier!
                                            if (dbEpisodes.Exists(item =>
                                                item.IndexNumber == sequence.IndexNumber &&
                                                item.SeasonId == sequence.SeasonId))
                                            {
                                                dbEpisodes.RemoveAll(item =>
                                                    item.IndexNumber == sequence.IndexNumber &&
                                                    item.SeasonId == sequence.SeasonId);
                                            }

                                            dbEpisodes.Add(sequence);
                                        }
                                    }

                                    stopWatch.Stop();

                                    // ReSharper disable once AccessToModifiedClosure

                                    Log.Debug(
                                        $"{series.Name} - {season.Name} - Episode: {unmatchedItem.IndexNumber} and Episode: {comparableItem.IndexNumber} - total detection time took {stopWatch.ElapsedMilliseconds} milliseconds.");

                                }
                                catch (SequenceInvalidDetectionException)
                                {

                                    //Keep going!
                                    if (episodeComparableIndex != episodeQuery.Items.Count() - 1)
                                    {
                                        continue;
                                    }

                                    if (fastDetect) continue;

                                    if (!episodeResults.ContainsKey(unmatchedItem.InternalId))
                                    {
                                        var unmatchedSequence = repository.GetResult(unmatchedItem.InternalId.ToString());
                                        if (episodeQuery.TotalRecordCount > 1)
                                        {
                                            unmatchedSequence.Processed = true;
                                            repository.SaveResult(unmatchedSequence, cancellationToken);
                                        }
                                    }

                                    if (!episodeResults.ContainsKey(episodeQuery.Items[episodeComparableIndex].InternalId))
                                    {
                                        var comparableSequence = repository.GetResult(episodeQuery.Items[episodeComparableIndex].InternalId.ToString());
                                        if (episodeQuery.TotalRecordCount > 1)
                                        {
                                            comparableSequence.Processed = true;
                                            repository.SaveResult(comparableSequence, cancellationToken);
                                        }
                                    }

                                    Log.Debug(
                                        $"Unable to match {unmatchedItem.Parent.Parent.Name} {unmatchedItem.Parent.Name} E: {unmatchedItem.IndexNumber} with E: {episodeQuery.Items[episodeComparableIndex].IndexNumber}");




                                    ////We have exhausted all our episode comparing
                                    //if (dbEpisodes.Exists(item => item.InternalId == unmatchedItem.InternalId)) continue;

                                }
                                catch (AudioFingerprintMissingException ex)
                                {
                                    Log.Debug(
                                        $"{unmatchedItem.Parent.Parent.Name} S: {unmatchedItem.Parent.IndexNumber} E: {unmatchedItem.IndexNumber} {ex.Message}");

                                }
                                catch(Exception ex)
                                {
                                    Log.Warn(ex.Message);
                                }

                            }
                        });

                        switch (fastDetect)
                        {
                            case true:
                            {
                                foreach (var episode in dbEpisodes)
                                {
                                    //If this is the only episode don't mark it as processed.
                                    //Wait until there are more episodes available for the season.
                                    if (episodeQuery.TotalRecordCount > 1)
                                    {
                                        episode.Processed = true; //<-- now we won't process episodes again over and over
                                    }

                                    repository.SaveResult(episode, cancellationToken);

                                    var found = LibraryManager.GetItemById(episode.InternalId); //<-- This will take up time, and could be removed later
                                    Log.Info(
                                        $"{ found.Parent.Parent.Name } S: { found.Parent.IndexNumber } E: { found.IndexNumber } title sequence successful.");
                                }

                                break;
                            }
                            case false:
                            {
                                
                                //Find our common title sequence duration.
                                var commonTitleSequenceDuration = TimeSpan.Zero;
                                var commonCreditSequenceDuration = TimeSpan.Zero;

                                if (!episodeResults.Any()) continue;
                                //All our results from every comparison
                                var fullResults = new List<SequenceResult>();
                                episodeResults.ToList().ForEach(item => fullResults.AddRange(item.Value));
                                
                                //Group the title sequence results by the duration if the intro
                                var titleSequenceDurationGroups = fullResults.GroupBy(i => i.TitleSequenceEnd - i.TitleSequenceStart);
                                commonTitleSequenceDuration = CommonTimeSpan(titleSequenceDurationGroups);

                                
                                //Group the title sequence results by the duration if the intro
                                var creditSequenceDurationGroups = fullResults.GroupBy(i => i.CreditSequenceEnd - i.CreditSequenceStart);
                                commonCreditSequenceDuration = CommonTimeSpan(creditSequenceDurationGroups, longestCommonTimeSpan: true);


                                Log.Debug($"DETECTION: Common duration for  {season.Parent.Name} - { season.Name } intro is: { commonTitleSequenceDuration } - calculated from: { fullResults.Count } results");
                                Log.Debug($"DETECTION: Common duration for  {season.Parent.Name} - { season.Name } credits are: { commonCreditSequenceDuration } - calculated from: { fullResults.Count } results");
                                //fullResults.Clear(); //<--Free up memory

                                episodeResults
                                    .AsParallel()
                                    .WithDegreeOfParallelism(2)
                                    .WithCancellation(cancellationToken)
                                    .ForAll(
                                        item =>
                                        {

                                            //try
                                            //{
                                                var (titleSequenceConfidence, titleSequence)   = Tuple.Create(0.0, item.Value.FirstOrDefault()); //Default
                                                var (creditSequenceConfidence, creditSequence) = Tuple.Create(0.0, item.Value.FirstOrDefault()); //Default

                                                try
                                                {
                                                    (titleSequenceConfidence, titleSequence) = GetBestTitleSequenceResult(commonTitleSequenceDuration, item.Value, cancellationToken);
                                                }
                                                catch { }

                                                try
                                                {
                                                    (creditSequenceConfidence, creditSequence) = GetBestCreditSequenceResult(commonTitleSequenceDuration, item.Value, cancellationToken);
                                                }
                                                catch { }

                                                var sequenceResult = repository.GetResult(item.Key.ToString());
                                                sequenceResult.HasCreditSequence   = creditSequence.HasCreditSequence;
                                                sequenceResult.CreditSequenceStart = creditSequence.CreditSequenceStart;
                                                sequenceResult.HasTitleSequence    = titleSequence.HasTitleSequence;
                                                sequenceResult.TitleSequenceStart  = titleSequence.TitleSequenceStart;
                                                sequenceResult.TitleSequenceEnd    = titleSequence.TitleSequenceEnd;
                                                //sequenceResult.CreditSequenceFingerprint.Clear();
                                                //sequenceResult.TitleSequenceFingerprint.Clear();

                                                //if (episodeQuery.TotalRecordCount > 1)
                                                //{
                                                sequenceResult.Processed = true; //<-- now we won't process episodes again over and over
                                                //}

                                                repository.SaveResult(sequenceResult, cancellationToken);
                                                
                                                var e = LibraryManager.GetItemById(sequenceResult.InternalId);
                                                Log.Debug(
                                                    $"\nBest result              : {season.Parent.Name} - {season.Name} Episode: {e.IndexNumber} " +
                                                    $"\nTITLE SEQUENCE START     : {sequenceResult.TitleSequenceStart} " +
                                                    $"\nTITLE SEQUENCE END       : {sequenceResult.TitleSequenceEnd} " +
                                                    $"\nTITLE SEQUENCE CONFIDENCE: {titleSequenceConfidence}" +
                                                    $"\nCREDIT START             : {sequenceResult.CreditSequenceStart}" +
                                                    $"\nCREDIT END               : {sequenceResult.CreditSequenceEnd}" +
                                                    $"\nCREDIT CONFIDENCE        : {creditSequenceConfidence}");
                                            //}
                                            //catch (Exception)
                                            //{
                                            //    //Something happens here a sequence has nothing in it. Could be the CommonTimespan function. it is unclear.
                                            //}
                                        });

                                break;
                            }
                        }
                        episodeResults.Clear();
                        dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = season.InternalId });
                        Clean(dbResults.Items.ToList(), season, repository, cancellationToken);
                    }

                });
            
            progress.Report(100.0);
        }
        

        private Tuple <double, SequenceResult> GetBestTitleSequenceResult(TimeSpan common,
            ConcurrentBag<SequenceResult> titleSequences, CancellationToken cancellationToken)
        {
            var weightedResults = new ConcurrentDictionary<double, SequenceResult>();
            
            titleSequences
                .AsParallel()
                .WithDegreeOfParallelism(2)
                .WithCancellation(cancellationToken)
                .ForAll(result =>
                {
                    if (result.TitleSequenceStart - TimeSpan.FromSeconds(20) <= TimeSpan.Zero)
                    {
                        result.TitleSequenceStart = TimeSpan.Zero;
                        result.TitleSequenceEnd = common;

                    }
                    
                    var duration = result.TitleSequenceEnd - result.TitleSequenceStart;

                    var startGroups = titleSequences.GroupBy(sequence => sequence.TitleSequenceStart);
                    var commonStart = CommonTimeSpan(startGroups);

                    var endGroups = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd);
                    var commonEnd = CommonTimeSpan(endGroups);


                    double durationWeight = 1.0;
                    double startWeight    = 1.0;
                    double endWeight      = 1.0;


                    if (common != TimeSpan.Zero)
                    {
                        durationWeight = (double)duration.Ticks / common.Ticks;
                    }

                    if(result.TitleSequenceStart != TimeSpan.Zero || commonStart != TimeSpan.Zero) //Start weight remains 1
                    {
                        startWeight =  (double)result.TitleSequenceStart.Ticks / commonStart.Ticks;
                    }

                    if(result.TitleSequenceEnd != TimeSpan.Zero || commonStart != TimeSpan.Zero) //Start weight remains 1
                    {
                        endWeight = (double)result.TitleSequenceEnd.Ticks / commonEnd.Ticks;
                    }

                    var score = durationWeight + startWeight + endWeight;
                    var avg = Math.Round(score / 3, 2, MidpointRounding.AwayFromZero);
                    if (avg >= Plugin.Instance.Configuration.DetectionConfidence)
                    {
                        //Add a weight to each result, by adding up the differences between them. 
                        weightedResults.TryAdd(avg, result);
                    }

                });

            var bestResult = weightedResults[weightedResults.Keys.Max()];
            var confidence = weightedResults.Keys.Max();
            return Tuple.Create(confidence, bestResult); //<-- Take the result with the highest rank. 

        }

        private Tuple<double, SequenceResult> GetBestCreditSequenceResult(TimeSpan common,
            ConcurrentBag<SequenceResult> sequences, CancellationToken cancellationToken)
        {
            var weightedResults = new ConcurrentDictionary<double, SequenceResult>();
            
            sequences
                .AsParallel()
                .WithDegreeOfParallelism(2)
                .WithCancellation(cancellationToken)
                .ForAll(result =>
                {

                    var duration = result.CreditSequenceEnd - result.CreditSequenceStart;

                    var startGroups = sequences.GroupBy(sequence => sequence.CreditSequenceStart);
                    var commonStart = CommonTimeSpan(startGroups, longestCommonTimeSpan: false);
                    
                    double durationWeight = 0.0;
                    double startWeight = 0.0;
                    
                    if (common != TimeSpan.Zero)
                    {
                        durationWeight = (double)duration.Ticks / common.Ticks;
                        var durationOffset = TimeSpan.FromSeconds(2);
                        if (duration >= common + -durationOffset && duration <= common + durationOffset) //a contiguous region that the duration might fall into
                        {
                            durationWeight += 0.1;
                        }
                    }

                    if (result.CreditSequenceStart != TimeSpan.Zero || commonStart != TimeSpan.Zero) //Start weight remains 1
                    {
                        startWeight = (double)result.CreditSequenceStart.Ticks / commonStart.Ticks;
                    }

                    var score = durationWeight + startWeight;
                    var avg = Math.Round(score / 2, 2, MidpointRounding.AwayFromZero);
                    if (avg >= Plugin.Instance.Configuration.DetectionConfidence)
                    {
                        //Add a weight to each result, by adding up the differences between them. 
                        weightedResults.TryAdd(avg, result);
                    }

                });

            var bestResult = weightedResults[weightedResults.Keys.Max()];
            var confidence = weightedResults.Keys.Max();

            //Look for a black screen close to where the fingerprint found comparisons in our bestResult.
            var offset = bestResult.CreditSequenceStart.Add(-TimeSpan.FromMinutes(1));
            var upperLimit = bestResult.CreditSequenceStart.Add(TimeSpan.FromSeconds(45));
            var blackDetection = VideoBlackDetectionManager.Instance.Analyze(bestResult.InternalId, offset, upperLimit, cancellationToken);
            
            if (blackDetection != null || blackDetection != TimeSpan.Zero)
            {
                bestResult.CreditSequenceStart = blackDetection;
            }

            if (bestResult.CreditSequenceStart == TimeSpan.Zero)bestResult.HasCreditSequence = false; //<-- this is impossible. So we are incorrect with our result.

            return Tuple.Create(confidence, bestResult); //<-- Take the result with the highest rank. The smallest difference.
        }


        private TimeSpan CommonTimeSpan(IEnumerable<IGrouping<TimeSpan, SequenceResult>> groups, bool longestCommonTimeSpan = false)
        {
            var enumerableGroup = groups.ToList();
            int maxCount        = enumerableGroup.Max(g => g.Count());
            var mostCommon      = enumerableGroup.First(g => g.Count() == maxCount).Key; //The most common timespan in the group
            var longest         = enumerableGroup.OrderByDescending(g => g.Count()).Select(g => g.Key).First(); //The longest time span in the group
            return longestCommonTimeSpan ? longest : mostCommon;
        }

        private bool IsComplete(BaseItem season, List<SequenceResult> dbEpisodes)
        {
            var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
            {
                Parent = season,
                IsVirtualItem = true,
                IncludeItemTypes = new[] { "Episode" },
                Recursive = true
            });

           

            if (episodeQuery.Items.Any(item => item.IsVirtualItem || item.IsUnaired))  return false; 

            return true;
        }

        private void Clean(List<SequenceResult> dbEpisodes, BaseItem season,  ISequenceRepository repo, CancellationToken cancellationToken)
        {
            // The DB file gets really big with all the finger print data. If we can remove some, do it here.
            if (dbEpisodes.All(result => result.HasTitleSequence || result.Confirmed))
            {
                if (IsComplete(season, dbEpisodes))
                {
                    //Remove the fingerprint data for these episodes. The db will be vacuumed at the end of this task.
                    foreach (var result in dbEpisodes)
                    {
                        try
                        {
                            if (!(result.TitleSequenceFingerprint is null))
                            {
                                result.TitleSequenceFingerprint.Clear();                  //Empty fingerprint List                                                                                             
                                repo.SaveResult(result, cancellationToken);  //Save it back to the db
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(ex.Message);
                        }
                    }
                }

                repo.Vacuum();
            }
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