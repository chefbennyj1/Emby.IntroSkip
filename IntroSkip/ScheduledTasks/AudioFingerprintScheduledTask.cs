using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Data;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;

// ReSharper disable ComplexConditionExpression
// ReSharper disable TooManyChainedReferences

namespace IntroSkip.ScheduledTasks
{
    public class AudioFingerprintScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private IUserManager UserManager { get; }
        private ILibraryManager LibraryManager { get; }
        private ILogger Log { get; }
        private ITaskManager TaskManager { get; }

        //private IDtoService DtoService { get; set; }
        // ReSharper disable once TooManyDependencies
        public AudioFingerprintScheduledTask(ILogManager logMan, IUserManager userManager, ILibraryManager libraryManager, ITaskManager taskManager)
        {
            UserManager = userManager;
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }

#pragma warning disable 1998
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#pragma warning restore 1998
        {
            if (!AudioFingerprintManager.Instance.HasChromaprint())
            {
                Log.Warn("Emby Server doesn't contain chromaprint libraries.");
                progress.Report(100.0);
                return;
            }

            var tasks = TaskManager.ScheduledTasks.ToList();
            if (tasks.FirstOrDefault(task => task.Name == "Episode Title Sequence Detection")?.State == TaskState.Running)
            {
                Log.Info("FINGERPRINT: Chroma-printing task will wait until title sequence task has finished.");
                progress.Report(100.0);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                progress.Report(100.0);
            }

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            
           
            //Sync repository Items
            try
            {
                progress.Report(1.0);
                var syncStopWatch = new Stopwatch();
                syncStopWatch.Start();
                Log.Info("Syncing Repository Items...");
                RepositoryItemSync(repository, cancellationToken);
                syncStopWatch.Stop();
                Log.Info($"Repository item sync completed. Duration: {syncStopWatch.ElapsedMilliseconds} milliseconds.");
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
            }


            try
            {
                Log.Info("FINGERPRINT: Starting episode fingerprint task.");

                var config = Plugin.Instance.Configuration;

                var seriesInternalItemQuery = new InternalItemsQuery()
                {
                    Recursive = true,
                    IncludeItemTypes = new[] { "Series" },
                    User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
                };

                if (!(config.IgnoredList is null))
                {
                    if (config.IgnoredList.Any())
                    {
                        seriesInternalItemQuery.ExcludeItemIds = config.IgnoredList.ToArray();
                    }
                }


                var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);

                var step = 100.0 / seriesQuery.TotalRecordCount;
                var currentProgress = 0.1;

                //Our database info
                QueryResult<SequenceResult> dbResults = null;
                List<SequenceResult> titleSequences = null;
                try
                {
                    dbResults = repository.GetResults(new SequenceResultQuery());
                    titleSequences = dbResults.Items.ToList();
                    Log.Info($"FINGERPRINT: Chroma-print database contains {dbResults.TotalRecordCount} items.");
                }
                catch (Exception)
                {
                    Log.Info("Title sequence database is new.");
                    titleSequences = new List<SequenceResult>();
                }

                progress.Report((currentProgress += step) - 1); //Give the user some kind of progress to show the task has started

                //We divide by two because we are going to split up the parallel function for both series and episodes.
                var fpMax = config.FingerprintingMaxDegreeOfParallelism / 2;
                var seriesIndex = 0;
                Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = fpMax }, (series, state) =>
                 {
                     if (cancellationToken.IsCancellationRequested)
                     {
                         state.Break();
                         progress.Report(100.0);
                     }

                     var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                     {
                         Parent = series,
                         Recursive = true,
                         IncludeItemTypes = new[] { "Season" },
                         User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                         IsVirtualItem = false
                     });

                     for (var seasonIndex = 0; seasonIndex <= seasonQuery.Items.Count() - 1; seasonIndex++)
                     {
                         if (cancellationToken.IsCancellationRequested)
                         {
                             break;
                         }

                         // ReSharper disable once AccessToModifiedClosure <-- That's ridiculous, it's right there!
                         var processedEpisodeResults = titleSequences.Where(s => s.SeasonId == seasonQuery.Items[seasonIndex].InternalId); //Items we have fingerprinted.

                         var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                         {
                             Parent = seasonQuery.Items[seasonIndex],
                             Recursive = true,
                             IncludeItemTypes = new[] { "Episode" },
                             User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                             IsVirtualItem = false
                         });


                        //The season has been processed and all episodes have a sequence - move on.
                        if (processedEpisodeResults != null)
                        {
                            if (processedEpisodeResults.Count() == episodeQuery.Items.Count())
                            {
                                Log.Debug($"FINGERPRINT: {series.Name} - {seasonQuery.Items[seasonIndex].Name} fingerprint profile is up to date.");
                                continue;
                            }
                        }
                        

                         var averageRuntime = GetSeasonRuntimeAverage(episodeQuery.Items);
                         var duration = GetEncodingDuration(averageRuntime);

                         //If we are processing the final series, increase the amount of episodes to process at once.
                         if (seriesIndex == seriesQuery.Items.Count() - 1) fpMax *= 2;

                         var index = seasonIndex;
                         Parallel.ForEach(episodeQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = (int)Math.Round((double)fpMax / 2, MidpointRounding.AwayFromZero) }, (episode, st) =>
                         {
                             if (cancellationToken.IsCancellationRequested)
                             {
                                 st.Break();
                             }

                             //The episode data exists in the database
                             
                             if (titleSequences.Exists(result => result.InternalId == episode.InternalId))
                             {
                                 var titleSequenceResult = titleSequences.FirstOrDefault(result => result.InternalId == episode.InternalId);

                                 // ReSharper disable once PossibleNullReferenceException <-- no it's not null, it was existing right up there...
                                 // ReSharper disable once CompareOfFloatsByEqualityOperator
                                 if (titleSequenceResult.Duration == duration)
                                 {
                                     return;
                                 }
                                 else //If new episodes are added to the season it may alter the encoding duration for the fingerprint. The duration for all fingerprints must be the same.
                                 {
                                     Log.Info(
                                         $"Encoding duration has changed for {series.Name} - {seasonQuery.Items[index].Name}");
                                     repository.Delete(titleSequenceResult.InternalId.ToString());

                                     dbResults = repository.GetResults(new SequenceResultQuery());

                                     titleSequences = dbResults.Items.ToList();
                                     processedEpisodeResults = titleSequences.Where(s =>
                                         s.SeasonId == seasonQuery.Items[index].InternalId);
                                 }
                             }

                             //var test = VideoDurationManager.Instance.CalculateRuntime(episode.Path, cancellationToken);
                             //Log.Debug($"DURATION TEST {test}");
                             var stopWatch = new Stopwatch();
                             stopWatch.Start();

                             List<uint> titleSequenceAudioFingerPrintData = null;
                             List<uint> creditSequenceAudioFingerPrintData = null;
                             try
                             {
                                 titleSequenceAudioFingerPrintData = AudioFingerprintManager.Instance.GetAudioFingerprint(episode, cancellationToken, TimeSpan.FromMinutes(duration));
                             }
                             catch (Exception ex)
                             {
                                 stopWatch.Stop();
                                 Log.Warn(ex.Message);
                                 //return;
                             }

                             try
                             {

                                 creditSequenceAudioFingerPrintData = AudioFingerprintManager.Instance.GetAudioFingerprint(episode, cancellationToken, TimeSpan.FromMinutes(3), isTitleSequence: false);
                             }
                             catch (Exception ex)
                             {
                                 stopWatch.Stop();
                                 Log.Warn(ex.Message);
                                 //return;
                             }

                             //Something has happened where we didn't encode anything
                             if (titleSequenceAudioFingerPrintData is null && creditSequenceAudioFingerPrintData is null) return;

                             try
                             {
                                 Log.Info($"{series.Name} {seasonQuery.Items[index].Name} Episode: {episode.IndexNumber} Credit and Title Sequence Fingerprinting Successful.");
                                 repository.SaveResult(new SequenceResult()
                                 {
                                     Duration = duration,
                                     TitleSequenceFingerprint = titleSequenceAudioFingerPrintData,
                                     CreditSequenceFingerprint = creditSequenceAudioFingerPrintData,
                                     HasTitleSequence = false,  //Set this to true when we scan the fingerprint data in the other scheduled task
                                     HasCreditSequence = false,  //Set this to true when we scan the fingerprint data in the other scheduled task
                                     IndexNumber = episode.IndexNumber,
                                     InternalId = episode.InternalId,
                                     SeasonId = seasonQuery.Items[index].InternalId,
                                     SeriesId = series.InternalId,
                                     TitleSequenceStart = new TimeSpan(),
                                     TitleSequenceEnd = new TimeSpan(),
                                     CreditSequenceStart = new TimeSpan(),
                                     CreditSequenceEnd = episode.RunTimeTicks.HasValue ? TimeSpan.FromTicks(episode.RunTimeTicks.Value) : new TimeSpan(),
                                     Confirmed = false,
                                     Processed = false,
                                     HasRecap = false
                                 }, cancellationToken);

                                 Log.Info(
                                   $"FINGERPRINT: {episode.Parent.Parent.Name} - S:{episode.Parent.IndexNumber} - E:{episode.IndexNumber} complete - {stopWatch.ElapsedMilliseconds / 1000} seconds.");
                             }
                             catch (NullReferenceException)
                             {
                                 //This is stream files. We'll just ignore it.
                                 stopWatch.Stop();
                             }
                             catch (Exception ex)
                             {
                                 stopWatch.Stop();
                                 Log.Error(ex.Message);
                             }

                         });
                     }

                     progress.Report((currentProgress += step) - 1);
                     seriesIndex += 1;
                 });
            }
            catch (TaskCanceledException)
            {
                progress.Report(100.0);
            }

            Log.Info("FINGERPRINT: Chromaprint Task Complete");

            var repo = repository as IDisposable;
            repo?.Dispose();
            progress.Report(100.0);


        }


        private int GetEncodingDuration(TimeSpan? averageRuntime)
        {
            if (!averageRuntime.HasValue) return 15;

            if (averageRuntime.Value >= TimeSpan.FromMinutes(40))
            {
                return 20;
            }

            if (averageRuntime.Value <= TimeSpan.FromMinutes(13))
            {
                return 8;
            }

            return 15;

        }

        private TimeSpan? GetSeasonRuntimeAverage(BaseItem[] episodes)
        {
            var totalCount = episodes.Count();
            long? runtimeSum = 0L;

            Parallel.ForEach(episodes, //<-- Do this fast
                (e) =>
                {
                    if (e.RunTimeTicks.HasValue)
                    {
                        runtimeSum += e.RunTimeTicks.Value;
                    }
                    else
                    {
                        totalCount -= 1;
                    }
                });
            
            TimeSpan? result = null;
            try
            {
                if (runtimeSum != null) result = TimeSpan.FromTicks(runtimeSum.Value / totalCount);
            }
            catch (Exception)
            {

            }

            return result;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type          = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        private void RepositoryItemSync(ISequenceRepository repository, CancellationToken cancellationToken)
        {
            var titleSequencesQuery = repository.GetBaseTitleSequenceResults(new SequenceResultQuery());
            var titleSequences = titleSequencesQuery.Items.ToList();

            var libraryQuery = LibraryManager.GetItemsResult(new InternalItemsQuery() { Recursive = true, IsVirtualItem = false, IncludeItemTypes = new[] { "Episode" } });
            var libraryItems = libraryQuery.Items.ToList();

            Log.Debug($"Library episodes count:        {libraryItems.Count}");
            Log.Debug($"Title Sequence episodes count: {titleSequences.Count}");
            
            var change = titleSequences.Where(sequence => !libraryItems.Exists(i => i.InternalId == sequence.InternalId)).ToList(); 
            
            if (!change.Any()) return; //No change. Move on.
            
            Log.Debug($"FINGERPRINT: {change.Count} sequence item(s) don't match the library. Syncing items for fingerprinting...");

            foreach (var item in change)
            {
                try
                {
                    repository.Delete(item.InternalId.ToString());
                }
                catch { }
            }
            //foreach (var sequenceChangeGroup in change.GroupBy(s => s.SeasonId))
            //{
            //    var dbResults = repository.GetBaseTitleSequenceResults(new SequenceResultQuery() { SeasonInternalId = sequenceChangeGroup.Key });
            //    foreach (var item in dbResults.Items)
            //    {
            //        try
            //        {
            //            repository.Delete(item.InternalId.ToString());
            //        }
            //        catch { }
            //    }
            //}

            //changedSequenceItems
            //    .AsParallel()
            //    .WithDegreeOfParallelism(5)
            //    .WithCancellation(cancellationToken)
            //    .ForAll(item =>
            //    {
            //        try
            //        {
            //            repository.Delete(item.InternalId.ToString());
            //        }
            //        catch { }
            //    });

            try
            {
                repository.Vacuum();
            }
            catch { }

            titleSequencesQuery = repository.GetBaseTitleSequenceResults(new SequenceResultQuery());
            titleSequences = titleSequencesQuery.Items.ToList();
            Log.Debug("Database Sync finalizing...");
            Log.Debug($"Library episodes count:        {libraryItems.Count}");
            Log.Debug($"Title Sequence episodes count: {titleSequences.Count}");


        }
        public string Name => "Episode Audio Fingerprinting";
        public string Key => "Audio Fingerprint Options";
        public string Description => "Chroma-print audio files for title sequence detection";
        public string Category => "Intro Skip";
        public bool IsHidden => false;
        public bool IsEnabled => true;
        public bool IsLogged => true;
    }
}