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
using MediaBrowser.Controller.Entities.TV;
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
                IncludeItemTypes = new[] { nameof(Series) },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
            };

            
            if (config.IgnoredList != null)
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
                    //LibraryMonitor.ReportFileSystemChangeBeginning(series.Path);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        //LibraryMonitor.ReportFileSystemChangeComplete(series.Path, false);
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
                            //dbEpisodes = new List<SequenceResult>();
                            continue;
                        }


                        //After processing, the DB entry is marked as 'processed'. if all the item has been processed already, just move on.
                        if (dbEpisodes.All(item => item.Processed))
                        {
                            Log.Debug($"{series.Name} {season.Name} have no new episodes to scan.");
                            continue;
                        }


                        // All our processed episodes with sequences, or user confirmed information
                        var exceptIds = new HashSet<long>(dbEpisodes.Where(e => e.Processed).Select(y => y.InternalId)).Distinct();
                        // A list of episodes with all our sequence data removed from it. All that is left is what we need to process.
                        var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                        if (!unmatched.Any()) //<-- this is redundant because we checked for 'processed' above. 
                        {
                            continue;
                        }

                        var episodeSequenceResults = new ConcurrentDictionary<long, ConcurrentBag<SequenceResult>>();

                        Log.Debug($"{series.Name} {season.Name} has {unmatched.Count} items to process...");
                        
                        var fingerprints = new ConcurrentDictionary<long, AudioFingerprint>();

                        unmatched.AsParallel().WithCancellation(cancellationToken).WithDegreeOfParallelism(config.MaxDegreeOfParallelism == 1 ? 1 : 2).ForAll((unmatchedItem) =>
                        {

                            //Make sure the database has this entry for this item available to edit. If it doesn't move on to the next item to process.
                            if(!repository.ResultExists(unmatchedItem.InternalId.ToString()))
                            {
                                Log.Warn($"{series.Name} { season.Name} Episode: {unmatchedItem.IndexNumber} doesn't exist in the sequence database.");
                                return;
                            }


                            //Compare the unmatched episode  with every other episode in the season until there is a match.
                            for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                            {

                                if (cancellationToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                var comparableItem = episodeQuery.Items[episodeComparableIndex];

                                //Don't compare the same episode with itself. The episodes must be different or we'll match the entire encoding.
                                if (comparableItem.InternalId == unmatchedItem.InternalId) continue;
                                

                                var comparableItemFingerprint = new AudioFingerprint();
                                
                                //Have we read the comparable item's fingerprint binary yet? if so it will exist in the ifngerprints dictionary.
                                if (!fingerprints.ContainsKey(comparableItem.InternalId))
                                {
                                    var sequenceResult = dbEpisodes.FirstOrDefault(e => e.InternalId == comparableItem.InternalId);
                                    if (sequenceResult is null)
                                    {
                                        Log.Warn("Comparable item sequence result is null.");
                                        return;
                                    }
                                    var duration = TimeSpan.FromMinutes(sequenceResult.Duration);
                                    comparableItemFingerprint.TitleSequenceFingerprint  = AudioFingerprintManager.Instance.GetTitleSequenceFingerprint(comparableItem, duration, cancellationToken);
                                    comparableItemFingerprint.CreditSequenceFingerprint = AudioFingerprintManager.Instance.GetCreditSequenceFingerprint(comparableItem, duration, cancellationToken);
                                    comparableItemFingerprint.InternalId = comparableItem.InternalId;
                                    comparableItemFingerprint.Duration = sequenceResult.Duration;

                                    fingerprints.TryAdd(comparableItem.InternalId, comparableItemFingerprint);
                                     
                                }
                                else
                                {
                                    comparableItemFingerprint = fingerprints[comparableItem.InternalId];
                                }

                                var unmatchedItemFingerprint = new AudioFingerprint();
                                //Have we read the unmatched item's fingerprint binary yet? if so it will exist in the dictionary, otherwise we'll have to create it.
                                if (!fingerprints.ContainsKey(unmatchedItem.InternalId))
                                {
                                    var sequenceResult = dbEpisodes.FirstOrDefault(e => e.InternalId == unmatchedItem.InternalId);
                                    if (sequenceResult is null)
                                    {
                                        Log.Warn("unmatched item sequence result is null.");
                                        return;
                                    }
                                    var duration = TimeSpan.FromMinutes(sequenceResult.Duration);
                                    unmatchedItemFingerprint.TitleSequenceFingerprint  = AudioFingerprintManager.Instance.GetTitleSequenceFingerprint(unmatchedItem, duration, cancellationToken);
                                    unmatchedItemFingerprint.CreditSequenceFingerprint = AudioFingerprintManager.Instance.GetCreditSequenceFingerprint(unmatchedItem, duration, cancellationToken);
                                    unmatchedItemFingerprint.InternalId = unmatchedItem.InternalId;
                                    unmatchedItemFingerprint.Duration = sequenceResult.Duration;

                                    fingerprints.TryAdd(unmatchedItem.InternalId, unmatchedItemFingerprint);

                                } 
                                else
                                {
                                    unmatchedItemFingerprint = fingerprints[unmatchedItem.InternalId];
                                }

                                //Huh... no fingerprint data. Move on...
                                if (!comparableItemFingerprint.TitleSequenceFingerprint.Any() || !comparableItemFingerprint.CreditSequenceFingerprint.Any()) continue;

                                try
                                {

                                    // The magic!
                                    var sequenceDetection = SequenceDetection.Instance;

                                    var stopWatch = new Stopwatch();
                                    stopWatch.Start();

                                    var sequences = sequenceDetection.DetectSequences(comparableItem, unmatchedItem, comparableItemFingerprint, unmatchedItemFingerprint, dbResults, stopWatch);
                                    
                                    //Created the keys in the result dictionary if we don't have one yet.
                                    if (!episodeSequenceResults.ContainsKey(unmatchedItem.InternalId))  episodeSequenceResults.TryAdd(unmatchedItem.InternalId, new ConcurrentBag<SequenceResult>());
                                    if (!episodeSequenceResults.ContainsKey(comparableItem.InternalId)) episodeSequenceResults.TryAdd(comparableItem.InternalId, new ConcurrentBag<SequenceResult>());
                                    
                                    //Add the result to the dictionary key
                                    episodeSequenceResults[unmatchedItem.InternalId].Add(sequences.FirstOrDefault(s => s.InternalId == unmatchedItem.InternalId));
                                    episodeSequenceResults[comparableItem.InternalId].Add(sequences.FirstOrDefault(s => s.InternalId == comparableItem.InternalId));

                                    stopWatch.Stop();

                                    // ReSharper disable once AccessToModifiedClosure - its just loggin.
                                    Log.Info(
                                        $"{series.Name} - {season.Name} - Episode: {unmatchedItem.IndexNumber} and Episode: {comparableItem.IndexNumber} total detection time took {stopWatch.ElapsedMilliseconds} milliseconds.");

                                }
                                catch (SequenceInvalidDetectionException)
                                {
                                    //Keep going!
                                    if (episodeComparableIndex != episodeQuery.Items.Count() - 1)
                                    {
                                        continue;
                                    }

                                    //No comparison results between these two items. Mark them as processed and move on...
                                    if (!episodeSequenceResults.ContainsKey(unmatchedItem.InternalId))
                                    {
                                        var unmatchedSequence = repository.GetResult(unmatchedItem.InternalId.ToString());
                                        unmatchedSequence.Processed = true;
                                        unmatchedSequence.CreditSequenceFingerprint = new List<uint>();
                                        unmatchedSequence.TitleSequenceFingerprint = new List<uint>();
                                        repository.SaveResult(unmatchedSequence, cancellationToken);

                                    }

                                    if (!episodeSequenceResults.ContainsKey(episodeQuery.Items[episodeComparableIndex].InternalId))
                                    {
                                        var comparableSequence = repository.GetResult(episodeQuery.Items[episodeComparableIndex].InternalId.ToString());
                                        comparableSequence.Processed = true;
                                        comparableSequence.CreditSequenceFingerprint = new List<uint>();
                                        comparableSequence.TitleSequenceFingerprint = new List<uint>();
                                        repository.SaveResult(comparableSequence, cancellationToken);

                                    }

                                    Log.Debug(
                                        $"Unable to match {unmatchedItem.Parent.Parent.Name} {unmatchedItem.Parent.Name} E: {unmatchedItem.IndexNumber} with E: {episodeQuery.Items[episodeComparableIndex].IndexNumber}");
                                    

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
                        

                        if (!episodeSequenceResults.Any()) continue; //Could happen...
                        
                        //All our results from every comparison of this unmatched item.
                        var fullResults = new List<SequenceResult>();
                        episodeSequenceResults.ToList().ForEach(item => fullResults.AddRange(item.Value));


                        //Find our common title sequence duration.
                        var commonTitleSequenceDuration = TimeSpan.Zero;
                        //Group the title sequence results by the duration if the intro
                        var titleSequenceDurationGroups = fullResults.GroupBy(i => i.TitleSequenceEnd - i.TitleSequenceStart);
                        commonTitleSequenceDuration = CommonTimeSpan(titleSequenceDurationGroups);


                        //Find our common credit sequence duration.
                        var commonCreditSequenceDuration = TimeSpan.Zero;
                        //Group the title sequence results by the duration if the credits
                        var creditSequenceDurationGroups = fullResults.GroupBy(i => i.CreditSequenceEnd - i.CreditSequenceStart);
                        commonCreditSequenceDuration = CommonTimeSpan(creditSequenceDurationGroups, longestCommonTimeSpan: true);
                        

                        //Calculate the best result from all the possibilities against our common duration results.
                        foreach (var item in episodeSequenceResults) //<-- We can't process this in parallel because there is no throttle when we run ffmpeg, and it pushes the CPU toooo much.
                        {

                            var sequenceResult = repository.GetResult(item.Key.ToString());
                            const double defaultConfidenceScore = 0.0;

                            var (titleSequenceConfidence, titleSequence)   = Tuple.Create(defaultConfidenceScore, sequenceResult); //Default
                            var (creditSequenceConfidence, creditSequence) = Tuple.Create(defaultConfidenceScore, sequenceResult); //Default

                            try
                            {
                                (titleSequenceConfidence, titleSequence) = GetBestTitleSequenceResult(commonTitleSequenceDuration, item.Value, cancellationToken);
                            }
                            catch
                            {
                                //Will be default values
                            }

                            try
                            {
                                (creditSequenceConfidence, creditSequence) = GetBestCreditSequenceResult(commonCreditSequenceDuration, item.Value, cancellationToken);
                            }
                            catch
                            {
                                //Will be default values
                            }

                            sequenceResult.HasCreditSequence         = creditSequence.HasCreditSequence;
                            sequenceResult.CreditSequenceStart       = creditSequence.CreditSequenceStart;
                            sequenceResult.CreditSequenceEnd         = creditSequence.CreditSequenceEnd;
                            sequenceResult.HasTitleSequence          = titleSequence.HasTitleSequence;
                            sequenceResult.TitleSequenceStart        = titleSequence.TitleSequenceStart;
                            sequenceResult.TitleSequenceEnd          = titleSequence.TitleSequenceEnd;
                            sequenceResult.CreditSequenceFingerprint = new List<uint>();
                            sequenceResult.TitleSequenceFingerprint  = new List<uint>();
                            sequenceResult.Processed                 = true; //<-- now we won't process episodes again over and over

                            try
                            {
                                repository.SaveResult(sequenceResult, cancellationToken);
                                var baseItem = LibraryManager.GetItemById(sequenceResult.InternalId);
                                Log.Debug(
                                    $"\n\n{season.Parent.Name} { season.Name } Episode {baseItem.IndexNumber}:" +
                                    $"\nCommon intro duration: { commonTitleSequenceDuration }" +
                                    $"\nCommon credit duration: { commonCreditSequenceDuration }" +
                                    $"\nDetection processed {fullResults.Count} results" +
                                    $"\nResults with highest confidence score:" +
                                    $"\nTitle sequence start time: {sequenceResult.TitleSequenceStart}" +
                                    $"\nTitle sequence end time: {sequenceResult.TitleSequenceEnd} " +
                                    $"\nTitle Sequence confidence score: {titleSequenceConfidence}" +
                                    $"\nCredit sequence start time: {sequenceResult.CreditSequenceStart}" +
                                    $"\nCredit sequence end time: {sequenceResult.CreditSequenceEnd}" +
                                    $"\nCredit confidence score: {creditSequenceConfidence}" +
                                    "\nSequence save successful.\n");
                            }
                            catch (Exception ex)
                            {
                                Log.Warn(ex.Message, ex);
                            }
                            
                            //AudioFingerprintManager.Instance.RemoveEpisodeFingerprintBinFiles(sequenceResult.InternalId);


                        };
                        
                        
                    }

                    

                });


            //progress.Report(100.0);
        }


        private Tuple<double, SequenceResult> GetBestTitleSequenceResult(TimeSpan common, ConcurrentBag<SequenceResult> titleSequences, CancellationToken cancellationToken)
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

                    //Add a weight to each result, by adding up the differences between them. 
                    weightedResults.TryAdd(avg, result);


                });

            //Log.Debug($"HIGHEST SCORE: {weightedResults.Keys.Max()}");
            //Log.Debug($"COMMON SCORE:  {CommonScore(weightedResults)}");

            var bestResult = weightedResults[weightedResults.Keys.Max()];
            var confidence = weightedResults.Keys.Max() > 1 ? 1 : weightedResults.Keys.Max();
            return Tuple.Create(confidence, bestResult); //<-- Take the result with the highest rank. 

        }

        private Tuple<double, SequenceResult> GetBestCreditSequenceResult(TimeSpan common, ConcurrentBag<SequenceResult> sequences, CancellationToken cancellationToken)
        {
            var weightedResults = new ConcurrentDictionary<double, SequenceResult>();
            var config = Plugin.Instance.Configuration;
            sequences
                .AsParallel()
                .WithDegreeOfParallelism(config.MaxDegreeOfParallelism == 1 ? 1 : 2)
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
                Log.Info($"DETECTION Adjusting black frame detection for {item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber} end credit sequence.");
                offset = runtime - TimeSpan.FromMinutes(2.1);
                upperLimit = runtime;
            }


            var blackDetections = new List<TimeSpan>();

            try
            {
                blackDetections.AddRange(VideoBlackDetectionManager.Instance.Analyze(bestResult.InternalId, cancellationToken));
            }
            catch { }

            if (blackDetections.Any())
            {
                var blackDetection = blackDetections.FirstOrDefault(d => d >= offset && d <= upperLimit); //The results found in our contiguous region.


                if (!Equals(blackDetection, TimeSpan.Zero))
                {
                    Log.Debug($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}:" +
                              $"\nCredit sequence audio detection start: {creditSequenceAudioDetectionStart}." +
                              $"\nBlack frame detected within contiguous regions: {blackDetection}." +
                              $"\nMoving credit sequence start time to: {blackDetection}.");
                    bestResult.CreditSequenceStart = blackDetection;
                    bestResult.CreditSequenceEnd = TimeSpan.FromTicks(item.RunTimeTicks.Value);
                    bestResult.HasCreditSequence = true;
                    confidence = creditSequenceAudioDetectionStart == blackDetection ? 1 : confidence; //<-- If the audio result was the same/ or close to a black frame detection we are perfect.
                }
                else
                {
                    Log.Debug($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}:" +
                              $"\nCredit sequence audio detection start: {creditSequenceAudioDetectionStart}." +
                              "\nNo black frame was detected within contiguous regions. Using Audio detection.");
                }
            }
            else
            {
                Log.Debug($"{item.Parent.Parent.Name} { item.Parent.Name} Episode {item.IndexNumber}:" +
                          $"\nCredit sequence audio detection start: {creditSequenceAudioDetectionStart}." +
                          "\nNo black frame was detected within contiguous regions. Using Audio detection.");
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

            return !episodeQuery.Items.Any(item => item.IsVirtualItem || item.IsUnaired);
        }

        private void Clean(BaseItem season, ISequenceRepository repo, CancellationToken cancellationToken)
        {
            var dbEpisodes = repo.GetResults(new SequenceResultQuery() { SeasonInternalId = season.InternalId });
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

                        if (!vacuum) vacuum = true;

                        try
                        {
                            result.TitleSequenceFingerprint = new List<uint>();                  //Empty fingerprint List                                                                                             
                            result.CreditSequenceFingerprint = new List<uint>();                 //Empty fingerprint List                                                                                            

                            repo.SaveResult(result, cancellationToken);  //Save it back to the db
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(ex.Message);
                        }
                    }
                }

                if (vacuum) repo.Vacuum();
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