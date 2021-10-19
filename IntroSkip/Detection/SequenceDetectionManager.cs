using IntroSkip.AudioFingerprinting;
using IntroSkip.Data;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace IntroSkip.Detection
{
    public class SequenceDetectionManager : IServerEntryPoint
    {
        private class BestResult
        {
            public bool HasSequence  { get; set; }
            public double Confidence { get; set; }
            public TimeSpan Start    { get; set; }
            public TimeSpan End      { get; set; }
        }

        private static ILogger Log                      { get; set; }
        private ILibraryManager LibraryManager          { get; }
        private IUserManager UserManager                { get; }
        public static SequenceDetectionManager Instance { get; private set; }

        public SequenceDetectionManager(ILogManager logManager, ILibraryManager libMan, IUserManager user)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager = user;
            Instance = this;
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, long[] seriesInternalIds, ITitleSequenceRepository repo)
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

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, ITitleSequenceRepository repository)
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
        

        private void Analyze(QueryResult<BaseItem> seriesQuery, IProgress<double> progress, CancellationToken cancellationToken, ITitleSequenceRepository repository)
        {

            if (cancellationToken.IsCancellationRequested)
            {
                progress.Report(100.0);
            }

            var config          = Plugin.Instance.Configuration;
            var currentProgress = 0.2;
            var step            = 100.0 / seriesQuery.TotalRecordCount;


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
                        catch (Exception)
                        {
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


                        //After processing, the DB entry is marked as 'processed'. if the item has been processed already, just move on.
                        if (dbEpisodes.All(item => item.Processed))
                        {
                            Log.Debug($"{series.Name} S:{season.IndexNumber} have no episodes to scan.");
                            continue;
                        }


                        // All our processed episodes with sequences, or user confirmed information
                        var exceptIds = new HashSet<long>(dbEpisodes.Where(e => e.HasTitleSequence && e.HasEndCreditSequence || e.Confirmed).Select(y => y.InternalId).Distinct());
                        // A list of episodes with all our episodes containing sequence data removed from it. All that is left is what we need to process.
                        var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                        if (!unmatched.Any()) //<-- this is redundant because we checked for 'processed' above. 
                        {
                            continue;
                        }


                        var fastDetect = Plugin.Instance.Configuration.FastDetect;
                        Log.Info($"Using: {(fastDetect ? "Fast Detection: ON" : " Fast Detection: OFF")} - Processing {unmatched.Count} episode(s) for {season.Parent.Name} - {season.Name}.");


                        var episodeResults = new ConcurrentDictionary<long, ConcurrentBag<SequenceResult>>();

                        unmatched.AsParallel().WithCancellation(cancellationToken).WithDegreeOfParallelism(2).ForAll((unmatchedBaseItem) =>
                        {
                            //Compare the unmatched episode  with every other episode in the season until there is a match.
                            for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                            {

                                if (cancellationToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                var comparableBaseItem = episodeQuery.Items[episodeComparableIndex];

                                //Don't compare the same episode with itself. The episodes must be different or we'll match the entire encoding.
                                if (comparableBaseItem.InternalId == unmatchedBaseItem.InternalId)
                                {
                                    continue;
                                }
                                
                                if (fastDetect)
                                {
                                    // If we have valid title sequence data for both items move on
                                    if (dbEpisodes.Any(item => item.InternalId == unmatchedBaseItem.InternalId) && dbEpisodes.Any(item => item.InternalId == comparableBaseItem.InternalId))
                                    {
                                        // ReSharper disable PossibleNullReferenceException
                                        var unmatchedHasTitleSequence      = dbEpisodes.FirstOrDefault(i => i.InternalId == unmatchedBaseItem.InternalId).HasTitleSequence;
                                        var unmatchedHasEndCreditSequence  = dbEpisodes.FirstOrDefault(i => i.InternalId == unmatchedBaseItem.InternalId).HasEndCreditSequence;
                                        var comparableHasTitleSequence     = dbEpisodes.FirstOrDefault(i => i.InternalId == comparableBaseItem.InternalId).HasTitleSequence;
                                        var comparableHasEndCreditSequence = dbEpisodes.FirstOrDefault(i => i.InternalId == comparableBaseItem.InternalId).HasEndCreditSequence;

                                        //We are good for these shows on fast detect
                                        if (unmatchedHasTitleSequence && comparableHasTitleSequence && unmatchedHasEndCreditSequence && comparableHasEndCreditSequence)
                                        {
                                            continue;
                                        }
                                    }
                                }
                                
                                
                                // The magic!
                                var sequenceDetection = SequenceDetection.Instance;

                                var stopWatch = new Stopwatch();
                                stopWatch.Start();

                                var comparableSequenceItem = dbEpisodes.FirstOrDefault(r => r.InternalId == comparableBaseItem.InternalId);
                                var unmatchedSequenceItem  = dbEpisodes.FirstOrDefault(r => r.InternalId == unmatchedBaseItem.InternalId);

                                //TODO: These items may not exists yet. Check to make sure or continue

                                var comparableRuntime = comparableBaseItem.RunTimeTicks.Value;
                                var unmatchedRuntime  = unmatchedBaseItem.RunTimeTicks.Value;


                                var titleSequencesResult = Tuple.Create(comparableSequenceItem, unmatchedSequenceItem); //Create the default result (no sequence data)
                                var endCreditResult      = Tuple.Create(comparableSequenceItem, unmatchedSequenceItem); //Create the default result (no sequence data)

                                try
                                {
                                    titleSequencesResult = sequenceDetection.DetectTitleSequence(comparableSequenceItem, unmatchedSequenceItem); //If something is going to change the result, it will here
                                }
                                catch (AudioFingerprintMissingException ex)
                                {
                                    Log.Debug($"{unmatchedBaseItem.Parent.Parent.Name} S: {unmatchedBaseItem.Parent.IndexNumber} E: {unmatchedBaseItem.IndexNumber} Title Sequence {ex.Message}");
                                    
                                }
                                catch (SequenceInvalidDetectionException)
                                {
                                    //Can't compare the items
                                }

                                try
                                {
                                    endCreditResult = sequenceDetection.DetectEndCreditSequence(comparableSequenceItem, unmatchedSequenceItem, comparableRuntime, unmatchedRuntime); //If something is going to change the result, it will here
                                }
                                catch (AudioFingerprintMissingException ex)
                                {
                                    Log.Debug($"{unmatchedBaseItem.Parent.Parent.Name} S: {unmatchedBaseItem.Parent.IndexNumber} E: {unmatchedBaseItem.IndexNumber} End Credit {ex.Message}");
                                    
                                }
                                catch (SequenceInvalidDetectionException)
                                {
                                    
                                    //Can compare the items
                                }

                                if (episodeComparableIndex == episodeQuery.Items.Count() - 1) //We have used all our posibilites for a match, mark it as processed notmatter what.
                                {
                                    unmatchedSequenceItem.Processed = true;
                                    repository.SaveResult(unmatchedSequenceItem, cancellationToken);
                                }

                                var comparableTitleSequenceResult     = titleSequencesResult.Item1;
                                var unmatchedTitleSequenceResult      = titleSequencesResult.Item2;

                                var comparableEndCreditSequenceResult = endCreditResult.Item1;
                                var unmatchedEndCreditSequenceResult  = endCreditResult.Item2;

                                

                                if (NoTitleSequenceResult(comparableTitleSequenceResult) && NoEndCreditSequenceResult(comparableEndCreditSequenceResult) &&
                                    NoTitleSequenceResult(unmatchedTitleSequenceResult)  && NoEndCreditSequenceResult(unmatchedEndCreditSequenceResult))
                                {
                                    continue;
                                }

                                comparableSequenceItem.HasTitleSequence       = comparableTitleSequenceResult.HasTitleSequence;
                                comparableSequenceItem.TitleSequenceStart     = comparableTitleSequenceResult.TitleSequenceStart;
                                comparableSequenceItem.TitleSequenceEnd       = comparableTitleSequenceResult.TitleSequenceEnd;
                                comparableSequenceItem.HasEndCreditSequence   = comparableEndCreditSequenceResult.HasEndCreditSequence;
                                comparableSequenceItem.EndCreditSequenceStart = comparableEndCreditSequenceResult.EndCreditSequenceStart;
                                comparableSequenceItem.EndCreditSequenceEnd   = comparableEndCreditSequenceResult.EndCreditSequenceEnd;

                                unmatchedSequenceItem.HasTitleSequence        = unmatchedTitleSequenceResult.HasTitleSequence;
                                unmatchedSequenceItem.TitleSequenceStart      = unmatchedTitleSequenceResult.TitleSequenceStart;
                                unmatchedSequenceItem.TitleSequenceEnd        = unmatchedTitleSequenceResult.TitleSequenceEnd;
                                unmatchedSequenceItem.HasEndCreditSequence    = unmatchedEndCreditSequenceResult.HasEndCreditSequence;
                                unmatchedSequenceItem.EndCreditSequenceStart  = unmatchedEndCreditSequenceResult.EndCreditSequenceStart;
                                unmatchedSequenceItem.EndCreditSequenceEnd    = unmatchedEndCreditSequenceResult.EndCreditSequenceEnd;



                                if (!fastDetect)
                                {
                                    if (!episodeResults.ContainsKey(unmatchedBaseItem.InternalId))
                                    {
                                        episodeResults.TryAdd(unmatchedBaseItem.InternalId, new ConcurrentBag<SequenceResult>());
                                    }

                                    if (!episodeResults.ContainsKey(comparableBaseItem.InternalId))
                                    {
                                        episodeResults.TryAdd(comparableBaseItem.InternalId, new ConcurrentBag<SequenceResult>());
                                    }

                                    episodeResults[unmatchedBaseItem.InternalId].Add(unmatchedSequenceItem);

                                    episodeResults[comparableBaseItem.InternalId].Add(comparableSequenceItem);
                                }

                                else
                                {
                                    //Just remove these entries in the episode list (if they exist) and add the new result back. Easier!
                                    if (dbEpisodes.Exists(item => item.InternalId == unmatchedSequenceItem.InternalId))
                                    {
                                        dbEpisodes.RemoveAll(item => item.InternalId == unmatchedSequenceItem.InternalId);
                                    }

                                    dbEpisodes.Add(unmatchedSequenceItem);

                                    if (dbEpisodes.Exists(item => item.InternalId == comparableSequenceItem.InternalId))
                                    {
                                        dbEpisodes.RemoveAll(item => item.InternalId == comparableSequenceItem.InternalId);
                                    }

                                    dbEpisodes.Add(comparableSequenceItem);
                                }

                                stopWatch.Stop();
                                
                                Log.Debug(
                                    $"{series.Name} - {season.Name} - E: {unmatchedBaseItem.IndexNumber} matched E: {comparableBaseItem.IndexNumber} - detection took {stopWatch.ElapsedMilliseconds} milliseconds.");
                                
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
                                            $"{ found.Parent.Parent.Name } S: { found.Parent.IndexNumber } E: { found.IndexNumber } sequences successful.");
                                    }

                                    break;
                                }
                            case false:
                                {

                                    //Our common title sequence duration.
                                    var commonTitleSequenceDuration = TimeSpan.Zero;

                                    //Our common end credit duration
                                    var commonEndCreditDuration = TimeSpan.Zero;

                                    if (!episodeResults.Any()) continue;

                                    //All our results for intros and credits from every comparison
                                    var fullResults = new List<SequenceResult>();
                                    episodeResults.ToList().ForEach(item => fullResults.AddRange(item.Value));

                                    if (!fullResults.Any()) continue; //<--Apparently we haven't any results...

                                    //Group the results by the duration if the intro
                                    var titleSequenceDurationGroups = fullResults.GroupBy(i => i.TitleSequenceEnd - i.TitleSequenceStart);
                                    commonTitleSequenceDuration     = CommonTimeSpan(titleSequenceDurationGroups);

                                    //All our results from end credits
                                    var endCreditDurationGroups = fullResults.GroupBy(i => i.EndCreditSequenceEnd - i.EndCreditSequenceStart);
                                    commonEndCreditDuration     = CommonTimeSpan(endCreditDurationGroups);


                                    Log.Debug($"DETECTION: Common duration for  {season.Parent.Name} - { season.Name } intro is  : { commonTitleSequenceDuration } - calculated from: { fullResults.Count } results.");
                                    Log.Debug($"DETECTION: Common duration for  {season.Parent.Name} - { season.Name } credits is: { commonEndCreditDuration } - calculated from: { fullResults.Count } results.");
                                    //fullResults.Clear(); //<--Free up memory

                                    episodeResults
                                        .AsParallel()
                                        .WithDegreeOfParallelism(2)
                                        .WithCancellation(cancellationToken)
                                        .ForAll(
                                            item =>
                                            {
                                                var introResult  = GetBestTitleSequence(commonTitleSequenceDuration, item.Value, cancellationToken);
                                                var creditResult = GetBestEndCreditSequence(item.Key, commonEndCreditDuration, item.Value, cancellationToken);
                                                
                                                var result = repository.GetResult(item.Key.ToString());
                                                
                                                result.HasTitleSequence       = introResult.HasSequence;
                                                result.TitleSequenceStart     = introResult.Start;
                                                result.TitleSequenceEnd       = introResult.End;
                                                result.HasEndCreditSequence   = creditResult.HasSequence;
                                                result.EndCreditSequenceStart = creditResult.Start;
                                                result.EndCreditSequenceEnd   = creditResult.End;
                                                result.Processed              = true; //<-- now we won't process episode again over and over

                                                var baseItem = LibraryManager.GetItemById(result.InternalId);
                                                try
                                                {
                                                    
                                                    Log.Debug($"Saving {season.Parent.Name} - { season.Name } Episode:{baseItem.IndexNumber} ...");
                                                    repository.SaveResult(result, cancellationToken);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Log.Error(ex.Message);
                                                }


                                                Log.Debug($"DETECTION: Best title sequence and end credit result:  {season.Parent.Name} - { season.Name } Episode:{baseItem.IndexNumber} " +
                                                    $"\nTITLE SEQUENCE START     : { result.TitleSequenceStart } " +
                                                    $"\nTITLE SEQUENCE END       : { result.TitleSequenceEnd } " +
                                                    $"\nTITLE SEQUENCE CONFIDENCE: { introResult.Confidence }" +
                                                    $"\nCREDITS START            : { result.EndCreditSequenceStart }" +
                                                    $"\nCREDITS END              : { result.EndCreditSequenceEnd }" +
                                                    $"\nCREDITS CONFIDENCE       : { creditResult.Confidence }");

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

        private bool NoEndCreditSequenceResult(BaseSequence result)
        {
            return result.EndCreditSequenceEnd == TimeSpan.Zero;            
        }

        private bool NoTitleSequenceResult(BaseSequence result)
        {
            return result.TitleSequenceEnd == TimeSpan.Zero;            
        }

         private BestResult GetBestEndCreditSequence(long internalId, TimeSpan commonEndCreditSequenceDuration,
            ConcurrentBag<SequenceResult> titleSequences, CancellationToken cancellationToken)
         {
            var weightedResults = new ConcurrentDictionary<double, SequenceResult>();
            
            var item = LibraryManager.GetItemById(internalId);

            TimeSpan runtime;
            if (!item.RunTimeTicks.HasValue)
            {
                Log.Warn($"{item.Parent.Parent.Name} {item.Parent.Name} Episode: {item.IndexNumber} - has no runtime metadata.");
            }
            else
            {
                runtime = TimeSpan.FromTicks(item.RunTimeTicks.Value);
            }

            //var defaultSequenceStart = runtime - TimeSpan.FromSeconds(30);
            
            return new BestResult()
            {
                Confidence  = 1.0,
                Start       = runtime - commonEndCreditSequenceDuration,
                End         = runtime,
                HasSequence = true
            };
        }


        private BestResult GetBestTitleSequence(TimeSpan commonTitleSequenceDuration,
            ConcurrentBag<SequenceResult> titleSequences, CancellationToken cancellationToken)
        {
            var weightedResults = new ConcurrentDictionary<double, SequenceResult>();

            titleSequences
                .AsParallel()
                .WithDegreeOfParallelism(2)
                .WithCancellation(cancellationToken)
                .ForAll(result =>
                {
                    double durationWeight = 0.0;
                    double startWeight    = 0.0;
                    double endWeight      = 0.0;

                    if (result.TitleSequenceStart - TimeSpan.FromSeconds(20) <= TimeSpan.Zero)
                    {
                        result.TitleSequenceStart = TimeSpan.Zero;
                        result.TitleSequenceEnd = commonTitleSequenceDuration;
                        durationWeight = 1.0;
                        startWeight    = 1.0;
                        endWeight      = 1.0;
                    }

                    var duration = result.TitleSequenceEnd - result.TitleSequenceStart;

                    var titleSequenceStartGroups = titleSequences.GroupBy(sequence => sequence.TitleSequenceStart);
                    var commonStart = CommonTimeSpan(titleSequenceStartGroups);

                    var titleSequenceEndGroups = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd);
                    var commonEnd = CommonTimeSpan(titleSequenceEndGroups);
                    
                    var offset = TimeSpan.FromSeconds(2);

                    if (commonTitleSequenceDuration != TimeSpan.Zero)
                    {
                        durationWeight = duration.Ticks / commonTitleSequenceDuration.Ticks;
                        //Add extra weight if the value falls within contiguous region.
                        if (duration >= commonTitleSequenceDuration.Add(-offset) && duration <= commonTitleSequenceDuration.Add(offset))
                        {
                            durationWeight += 0.1;
                        }
                    }

                    if (result.TitleSequenceStart != TimeSpan.Zero || commonStart != TimeSpan.Zero) //else result weight remains 1
                    {
                        startWeight = result.TitleSequenceStart.Ticks / commonStart.Ticks;
                        //Add extra weight if the value falls within contiguous region.
                        if (result.TitleSequenceStart >= commonStart - offset && result.TitleSequenceStart <= commonStart + offset)
                        {
                            startWeight += 0.1;
                        }
                    }

                    if (result.TitleSequenceEnd != TimeSpan.Zero || commonEnd != TimeSpan.Zero) //else result weight remains 1
                    {
                        endWeight = result.TitleSequenceEnd.Ticks / commonEnd.Ticks;
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
            
            return new BestResult()
            {
                Confidence  = confidence,
                Start       = bestResult.TitleSequenceStart,
                End         = bestResult.TitleSequenceEnd,
                HasSequence = bestResult.HasTitleSequence
            };
            
        }

        private TimeSpan CommonTimeSpan(IEnumerable<IGrouping<TimeSpan, SequenceResult>> groups)
        {
            var enumerableGroup = groups.ToList();
            int maxCount = enumerableGroup.Max(g => g.Count());
            return enumerableGroup.First(g => g.Count() == maxCount).Key; //The most common timespan in the group
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

            if (episodeQuery.Items.Any(item => item.IsVirtualItem || item.IsUnaired)) return false;

            return true;
        }

        private void Clean(List<SequenceResult> dbEpisodes, BaseItem season, ITitleSequenceRepository repo, CancellationToken cancellationToken)
        {
            // The DB file gets really big with all the finger print data. If we can remove some, do it here.
            if (!dbEpisodes.All(result => result.HasTitleSequence && result.HasEndCreditSequence || result.Confirmed)) return;

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
                            repo.SaveResult(result, cancellationToken);               //Save it back to the db
                        }
                        if (!(result.EndCreditFingerprint is null))
                        {
                            result.EndCreditFingerprint.Clear();                  //Empty fingerprint List                                                                                             
                            repo.SaveResult(result, cancellationToken);           //Save it back to the db
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


        public void Dispose()
        {

        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {

        }
    }
}