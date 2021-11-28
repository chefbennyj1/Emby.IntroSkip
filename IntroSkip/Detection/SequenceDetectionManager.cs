using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Data;
using IntroSkip.Sequence;
using IntroSkip.VideoBlackDetect;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

// ReSharper disable TooManyChainedReferences

namespace IntroSkip.Detection
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

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, ISequenceRepository repository)
        {
            var config = Plugin.Instance.Configuration;
            var seriesInternalItemQuery = new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
            };

            if (config.IgnoredList != null)
                if(config.IgnoredList.Count > 0)
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

                        /*
                         * Initially we're going to use dbEpisodes to locate all the unmatched items in the database.
                         *
                         * While 'FastDetect' is enabled, we'll only grab the first detection found by the algorithm
                         * We use dbEpisodes to handle the sequence detections.
                         * It is the dbEpisodes List we'll alter and save the first found detection to.
                         * Then put the dbEpisodes items in to the Database.
                         *
                         *
                         * If 'FastDetect' is disabled, we'll only use dbEpisodes to locate the unmatched items in the Database.
                         * Then we create and use a ConcurrentDictionary ('episodeResults') to hold all our detection results
                         * Key   = InternalId of the episode
                         * Value = Concurrent List of detection results for the item.
                         * 
                         */

                        var dbEpisodes = dbResults.Items.ToList();
                        
                        if (!dbEpisodes.Any()) //<-- this should not happen unless the fingerprint task was never run on this season. 
                        {
                            dbEpisodes = new List<SequenceResult>();
                        }


                        //After processing, the DB entry is marked as 'processed'. if the item has been processed already, just move on.
                        if (dbEpisodes.All(item => item.Processed))
                        {
                            Log.Debug($"{series.Name} {season.Name} have no new episodes to scan.");
                            try
                            {
                                Clean(season, repository, cancellationToken);
                            }
                            catch { }

                            continue;
                        }


                        // All our processed episodes with sequences, or user confirmed information
                        var exceptIds = new HashSet<long>(dbEpisodes.Where(e => e.Processed).Select(y => y.InternalId).Distinct());
                        // A list of episodes with all our episodes containing sequence data removed from it. All that is left is what we need to process.
                        var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                        if (!unmatched.Any()) //<-- this is redundant because we checked for 'processed' above. 
                        {
                            continue;
                        }

                        var fastDetect = Plugin.Instance.Configuration.FastDetect;
                        Log.Info($"Using Fast Detection : {(fastDetect ? "ON" : "OFF")} - Processing {unmatched.Count()} episode(s) for {series.Name} - {season.Name}.");

                        //We'll only use this dictionary if fast detect is off. Probably shouldn't create it if not necessary.
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

                                if (fastDetect)
                                {
                                    // If we have valid title sequence data for both items move on - these items have been processed during this scan.
                                    if (dbEpisodes.Any(item => item.InternalId == unmatchedItem.InternalId) && dbEpisodes.Any(item => item.InternalId == comparableItem.InternalId))
                                    {
                                        var dbResultComparableItem = dbEpisodes.FirstOrDefault(i => i.InternalId == comparableItem.InternalId);
                                        var dbResultUnmatchedItem = dbEpisodes.FirstOrDefault(i => i.InternalId == unmatchedItem.InternalId);

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
                                    var sequenceDetection = SequenceDetection.Instance;

                                    var stopWatch = new Stopwatch();
                                    stopWatch.Start();

                                    var sequences = sequenceDetection.DetectSequences(comparableItem, unmatchedItem, dbResults, stopWatch);

                                    if (!fastDetect)
                                    {

                                        //Created the keys in the dictionary for the results if we don't have one yet.
                                        if (!episodeResults.ContainsKey(unmatchedItem.InternalId))
                                        {
                                            episodeResults.TryAdd(unmatchedItem.InternalId, new ConcurrentBag<SequenceResult>());
                                        }

                                        if (!episodeResults.ContainsKey(comparableItem.InternalId))
                                        {
                                            episodeResults.TryAdd(comparableItem.InternalId, new ConcurrentBag<SequenceResult>());
                                        }

                                        //Add the result to the dictionary key
                                        episodeResults[unmatchedItem.InternalId].Add(sequences.FirstOrDefault(s => s.InternalId == unmatchedItem.InternalId));
                                        episodeResults[comparableItem.InternalId].Add(sequences.FirstOrDefault(s => s.InternalId == comparableItem.InternalId));
                                    }

                                    else
                                    {
                                        foreach (var sequence in sequences)
                                        {
                                            //Add the new result into dbEpisodes. 
                                            if (dbEpisodes.Exists(item => item.InternalId == sequence.InternalId))
                                            {
                                                dbEpisodes.RemoveAll(item => item.InternalId == sequence.InternalId);
                                            }

                                            dbEpisodes.Add(sequence);
                                        }
                                    }

                                    stopWatch.Stop();

                                    // ReSharper disable once AccessToModifiedClosure

                                    Log.Debug(
                                        $"{series.Name} - {season.Name} - Episode: {unmatchedItem.IndexNumber} and Episode: {comparableItem.IndexNumber} total detection time took {stopWatch.ElapsedMilliseconds} milliseconds.");

                                }
                                catch (SequenceInvalidDetectionException)
                                {
                                    //Keep going!
                                    if (episodeComparableIndex != episodeQuery.Items.Count() - 1)
                                    {
                                        continue;
                                    }

                                    if (!fastDetect)
                                    {
                                        if (!episodeResults.ContainsKey(unmatchedItem.InternalId))
                                        {
                                            var unmatchedSequence = repository.GetResult(unmatchedItem.InternalId.ToString());
                                            unmatchedSequence.Processed = true;
                                            repository.SaveResult(unmatchedSequence, cancellationToken);

                                        }

                                        if (!episodeResults.ContainsKey(episodeQuery.Items[episodeComparableIndex].InternalId))
                                        {
                                            var comparableSequence = repository.GetResult(episodeQuery.Items[episodeComparableIndex].InternalId.ToString());
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
                                catch (Exception ex)
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
                                    //var commonCreditSequenceDuration = TimeSpan.Zero;

                                    if (!episodeResults.Any()) continue;

                                    //All our results from every comparison
                                    var fullResults = new List<SequenceResult>();
                                    episodeResults.ToList().ForEach(item => fullResults.AddRange(item.Value));

                                    //Group the title sequence results by the duration if the intro
                                    var titleSequenceDurationGroups = fullResults.GroupBy(i => i.TitleSequenceEnd - i.TitleSequenceStart);
                                    commonTitleSequenceDuration = CommonTimeSpan(titleSequenceDurationGroups);


                                    //Group the title sequence results by the duration if the credits
                                    var creditSequenceDurationGroups = fullResults.GroupBy(i => i.CreditSequenceEnd - i.CreditSequenceStart);
                                    var commonCreditSequenceDuration = CommonTimeSpan(creditSequenceDurationGroups, longestCommonTimeSpan: true);

                                    
                                    
                                    Log.Debug($"DETECTION: Common duration for  {season.Parent.Name} - { season.Name } intro is: { commonTitleSequenceDuration } - calculated from: { fullResults.Count } results");
                                    Log.Debug($"DETECTION: Common duration for  {season.Parent.Name} - { season.Name } credits is: { commonCreditSequenceDuration } - calculated from: { fullResults.Count } results");

                                    var results = new ConcurrentBag<SequenceResult>();
                                    
                                    foreach(var item in episodeResults) //<-- We can't process this in parallel because there is no throttle when we run ffmpeg, and it pushes the CPU toooo much.
                                    {

                                        var sequenceResult = repository.GetResult(item.Key.ToString());
                                        const double defaultConfidenceScore = 0.0;
                                        var (titleSequenceConfidence, titleSequence)   = Tuple.Create(defaultConfidenceScore, sequenceResult); //Default
                                        var (creditSequenceConfidence, creditSequence) = Tuple.Create(defaultConfidenceScore, sequenceResult); //Default

                                        try
                                        {
                                            (titleSequenceConfidence, titleSequence) =
                                                GetBestTitleSequenceResult(commonTitleSequenceDuration,
                                                    item.Value, cancellationToken);
                                        }
                                        catch
                                        {
                                            //Will be default values
                                        }

                                        try
                                        {
                                            (creditSequenceConfidence, creditSequence) =
                                                GetBestCreditSequenceResult(commonCreditSequenceDuration,
                                                    item.Value, cancellationToken);
                                        }
                                        catch
                                        {
                                            //Will be default values
                                        }

                                        sequenceResult.HasCreditSequence         = creditSequence.HasCreditSequence;
                                        sequenceResult.CreditSequenceStart       = creditSequence.CreditSequenceStart;
                                        sequenceResult.HasTitleSequence          = titleSequence.HasTitleSequence;
                                        sequenceResult.TitleSequenceStart        = titleSequence.TitleSequenceStart;
                                        sequenceResult.TitleSequenceEnd          = titleSequence.TitleSequenceEnd;
                                        sequenceResult.CreditSequenceFingerprint = creditSequence.CreditSequenceFingerprint ?? new List<uint>();
                                        sequenceResult.TitleSequenceFingerprint  = titleSequence.TitleSequenceFingerprint ?? new List<uint>();
                                        sequenceResult.Processed                 = true; //<-- now we won't process episodes again over and over

                                        //repository.SaveResult(sequenceResult, cancellationToken);
                                        results.Add(sequenceResult);
                                        var e = LibraryManager.GetItemById(sequenceResult.InternalId);
                                          
                                            
                                        Log.Debug(
                                            $"\nDETECTION processed {episodeResults.Count} compared items for {season.Parent.Name} { season.Name } Episode {e.IndexNumber}." +
                                            $"\nBest result: {season.Parent.Name} - {season.Name} Episode {e.IndexNumber} " +
                                            $"\nTITLE SEQUENCE START: {sequenceResult.TitleSequenceStart} " +
                                            $"\nTITLE SEQUENCE END: {sequenceResult.TitleSequenceEnd} " +
                                            $"\nCREDIT START: {sequenceResult.CreditSequenceStart}" +
                                            $"\nCREDIT END: {sequenceResult.CreditSequenceEnd}" +
                                            $"\nCREDIT CONFIDENCE: {creditSequenceConfidence}" +
                                            $"\nTITLE SEQUENCE CONFIDENCE: {titleSequenceConfidence}\n");


                                    };

                                    //Save to our database.
                                    results.ToList().ForEach(result =>
                                    {
                                        try
                                        {
                                            repository.SaveResult(result, cancellationToken);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.ErrorException(ex.Message, ex);
                                        }
                                    });

                                    break;
                                }
                        }
                        //episodeResults.Clear();
                        //dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = season.InternalId });
                        Clean(season, repository, cancellationToken);
                    }

                });

            progress.Report(100.0);
        }
        
        private Tuple<double, SequenceResult> GetBestTitleSequenceResult(TimeSpan common,
            ConcurrentBag<SequenceResult> titleSequences, CancellationToken cancellationToken)
        {
            var weightedResults = new ConcurrentDictionary<double, SequenceResult>();

            titleSequences
                .AsParallel()
                .WithDegreeOfParallelism(2)
                .WithCancellation(cancellationToken)
                .ForAll(result =>
                {
                    //We're close enough to the beginning of the stream, we'll call it the beginning.
                    //Make the end the common title sequence duration.
                    if (result.TitleSequenceStart - TimeSpan.FromSeconds(20) <= TimeSpan.Zero)
                    {
                        result.TitleSequenceStart = TimeSpan.Zero;
                        result.TitleSequenceEnd = common;

                    }

                    //This episodes duration
                    var duration = result.TitleSequenceEnd - result.TitleSequenceStart;

                    var startGroups = titleSequences.GroupBy(sequence => sequence.TitleSequenceStart);
                    var commonStart = CommonTimeSpan(startGroups);

                    var endGroups = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd);
                    var commonEnd = CommonTimeSpan(endGroups);


                    double durationWeight = 1.0;
                    double startWeight = 1.0;
                    double endWeight = 1.0;


                    if (common != TimeSpan.Zero)
                    {
                        durationWeight = (double)duration.Ticks / common.Ticks;
                    }

                    if (result.TitleSequenceStart != TimeSpan.Zero || commonStart != TimeSpan.Zero) //Start weight remains 1
                    {
                        startWeight = (double)result.TitleSequenceStart.Ticks / commonStart.Ticks;
                    }

                    if (result.TitleSequenceEnd != TimeSpan.Zero || commonStart != TimeSpan.Zero) //Start weight remains 1
                    {
                        endWeight = (double)result.TitleSequenceEnd.Ticks / commonEnd.Ticks;
                    }

                    var score = durationWeight + startWeight + endWeight;
                    var avg = Math.Round(score / 3, 2, MidpointRounding.ToEven) - 0.1;
                    if (avg >= Plugin.Instance.Configuration.DetectionConfidence)
                    {
                        //Add a weight to each result, by adding up the differences between them. 
                        weightedResults.TryAdd(avg, result);
                    }

                });

            var bestResult = weightedResults[weightedResults.Keys.Max()];
            var confidence = weightedResults.Keys.Max() > 1 ? 1 : weightedResults.Keys.Max();
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
                    }

                    if (result.CreditSequenceStart != TimeSpan.Zero || commonStart != TimeSpan.Zero) //Start weight remains 1
                    {
                        startWeight = (double)result.CreditSequenceStart.Ticks / commonStart.Ticks;
                    }

                    var score = durationWeight + startWeight;
                    var avg = Math.Round(score / 2, 2, MidpointRounding.ToEven) - 0.1;
                    weightedResults.TryAdd(avg, result);

                });

            var bestResult = weightedResults[weightedResults.Keys.Max()];
            var item = LibraryManager.GetItemById(bestResult.InternalId);

            var confidence = weightedResults.Keys.Max() > 1 ? 1 : weightedResults.Keys.Max();

            //Look for a black screen close to where the fingerprint found comparisons in our bestResult.
            var runtime = TimeSpan.Zero;
            if (item.RunTimeTicks.HasValue)
            {
                runtime = TimeSpan.FromTicks(item.RunTimeTicks.Value);
            }
            else
            {
                Log.Warn($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber} has no runtime metadata. Exiting...");
                bestResult.HasCreditSequence = false; 
                confidence = 0.0;
                bestResult.Processed = true;
                return Tuple.Create(confidence, bestResult); 
            }
            

            var offset = runtime > TimeSpan.FromMinutes(35)
                ? bestResult.CreditSequenceStart.Add(-TimeSpan.FromSeconds(35))
                : bestResult.CreditSequenceStart.Add(-TimeSpan.FromSeconds(8));

            var upperLimit = runtime > TimeSpan.FromMinutes(35)
                ? bestResult.CreditSequenceStart.Add(TimeSpan.FromMinutes(1.5))
                : bestResult.CreditSequenceStart.Add(TimeSpan.FromSeconds(8));

            var creditSequenceAudioDetectionStart = bestResult.CreditSequenceStart;

            //End Credits for longer shows are NEVER 20 seconds long.
            //End credits can not start at 00:00:00.
            //Change the offset to 2.1 minutes before the end of the show, to look for black frame
            if (runtime > TimeSpan.FromMinutes(35) && common < TimeSpan.FromSeconds(20) || creditSequenceAudioDetectionStart == TimeSpan.Zero)
            {
                Log.Debug($"DETECTION Adjusting black frame detection for {item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}");
                offset = runtime - TimeSpan.FromMinutes(2.1);
                upperLimit = runtime;
            }
            

            var blackDetections = VideoBlackDetectionManager.Instance.Analyze(bestResult.InternalId, cancellationToken);

            if (blackDetections.Any())
            {
                var blackDetection = blackDetections.FirstOrDefault(d => d >= offset && d <= upperLimit); //The results found in our contiguous region.
                

                if (!Equals(blackDetection, TimeSpan.Zero))
                {
                    Log.Debug($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}:" +
                              $"\nCredit sequence audio detection start: {creditSequenceAudioDetectionStart}." +
                              $"\nBlack frame detected within contiguous regions: {blackDetection}." +
                              $"\nMoving sequence start to: {blackDetection}.");
                    bestResult.CreditSequenceStart = blackDetection;
                    confidence = creditSequenceAudioDetectionStart == blackDetection ? 1 : confidence; //<-- If the audio result was the same a black frame detection we are perfect.
                }
                else
                {
                    Log.Debug($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}:" +
                              $"\nCredit sequence audio detection start: {creditSequenceAudioDetectionStart}." +
                              "\nNo black frame detected within contiguous regions.");
                }
            }
            else
            {
                Log.Debug($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}:" +
                          $"\nCredit sequence audio detection start: {creditSequenceAudioDetectionStart}." +
                          "\nNo black frame detected within contiguous regions.");
            }

            if (bestResult.CreditSequenceStart == TimeSpan.Zero) //<-- this is impossible. So we are incorrect with our result.
            {
                bestResult.HasCreditSequence = false; 
                confidence = 0.0;
                Log.Debug($"Unable to find credit sequence for {item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}");
            }

            bestResult.Processed = true;
            return Tuple.Create(confidence, bestResult);
            
        }

       

        private TimeSpan CommonTimeSpan(IEnumerable<IGrouping<TimeSpan, SequenceResult>> groups, bool longestCommonTimeSpan = false)
        {
            var enumerableGroup = groups.ToList();
            int maxCount        = enumerableGroup.Max(g => g.Count());
            var mostCommon      = enumerableGroup.First(g => g.Count() == maxCount).Key; //The most common timespan in the group
            var longest         = enumerableGroup.OrderByDescending(g => g.Count()).Select(g => g.Key).First(); //The longest time span in the group
            return longestCommonTimeSpan ? longest : mostCommon;
        }

        private bool IsComplete(BaseItem season)
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

        private void Clean(BaseItem season, ISequenceRepository repo, CancellationToken cancellationToken)
        {
            var dbEpisodes = repo.GetResults(new SequenceResultQuery(){ SeasonInternalId = season.InternalId });
            // The DB file gets really big with all the finger print data. If we can remove some, do it here.
            var vacuum = false;
            if (dbEpisodes.Items.All(result => result.Processed))
            {
                if (IsComplete(season))
                {
                    //Remove the fingerprint data for these episodes. The db will be vacuumed at the end of this task.
                    foreach (var result in dbEpisodes.Items)
                    {
                        if (!result.CreditSequenceFingerprint.Any() || !result.TitleSequenceFingerprint.Any()) continue;
                        
                        if(!vacuum) vacuum = true;
                            
                        try
                        {
                            result.TitleSequenceFingerprint = new List<uint>();                  //Empty fingerprint List                                                                                             
                            result.CreditSequenceFingerprint= new List<uint>();                 //Empty fingerprint List                                                                                            

                            repo.SaveResult(result, cancellationToken);  //Save it back to the db
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(ex.Message);
                        }
                    }
                }

                if(vacuum) repo.Vacuum();
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