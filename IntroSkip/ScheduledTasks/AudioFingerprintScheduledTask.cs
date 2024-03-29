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
                progress.Report(0.1);
                var syncStopWatch = new Stopwatch();
                syncStopWatch.Start();
                Log.Info("FINGERPRINT: Syncing Repository Items...");
                RepositoryItemSync(repository, cancellationToken);
                syncStopWatch.Stop();
                Log.Info($"FINGERPRINT: Repository item sync completed. Duration: {syncStopWatch.ElapsedMilliseconds} milliseconds.");
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
            }

            if(!AudioFingerprintManager.Instance.HasChromaprint()) Log.Warn("Ffmpeg does not contain Chromaprint libraries.");

            var step = CalculateProgressStep();

            try
            {
                
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

                //var step = 100.0 / seriesQuery.TotalRecordCount;
                var currentProgress = 0.1;
                
                progress.Report(currentProgress);
                
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
                    Log.Info("FINGERPRINT: Title sequence database is new.");
                    titleSequences = new List<SequenceResult>();
                }
                
               
                var fpMax = config.FingerprintingMaxDegreeOfParallelism;

                Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = fpMax }, (series, state) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Break();
                        progress.Report(100.0);
                    }
                    //progress.Report((currentProgress += step) - 1);
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

                        // ReSharper disable once AccessToModifiedClosure 
                        //var processedEpisodeResults = titleSequences.Where(s => s.SeasonId == seasonQuery.Items[seasonIndex].InternalId); //Items we have fingerprinted.

                        var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                        {
                            Parent = seasonQuery.Items[seasonIndex],
                            Recursive = true,
                            IncludeItemTypes = new[] { "Episode" },
                            User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                            IsVirtualItem = false
                        });

                        
                        //The season has been processed and all episodes have a sequence - move on.                        
                        //if (processedEpisodeResults.Count() == episodeQuery.TotalRecordCount)
                        //{
                        //    Log.Debug($"FINGERPRINT: {series.Name} - {seasonQuery.Items[seasonIndex].Name} chromaprint profile is up to date.");
                        //    continue;
                        //}

                        var averageRuntime = GetSeasonRuntimeAverage(episodeQuery.Items);
                        var duration = GetEncodingDuration(averageRuntime);
                        
                       
                        foreach (var episode in episodeQuery.Items)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }
                            
                            progress.Report((currentProgress += step) - 1);

                            //The episode data exists in the database
                            // ReSharper disable twice AccessToModifiedClosure 
                            if (titleSequences.Exists(result => result.InternalId == episode.InternalId))
                            {
                                var titleSequenceResult = titleSequences.FirstOrDefault(result => result.InternalId == episode.InternalId);

                                // ReSharper disable once PossibleNullReferenceException <-- no it's not null, it was existing right up there...
                                // ReSharper disable once CompareOfFloatsByEqualityOperator
                                if (titleSequenceResult.Duration == duration)
                                {
                                    continue;
                                }
                                else //If new episodes are added to the season it may alter the encoding duration for the fingerprint. The duration for all fingerprints must be the same.
                                {
                                    Log.Info($"FINGERPRINT: Encoding duration has changed for {series.Name} - {seasonQuery.Items[seasonIndex].Name}");
                                    repository.Delete(titleSequenceResult.InternalId.ToString());

                                    dbResults = repository.GetResults(new SequenceResultQuery());

                                    titleSequences = dbResults.Items.ToList();
                                    //processedEpisodeResults = titleSequences.Where(s => s.SeasonId == seasonQuery.Items[seasonIndex].InternalId);
                                }
                            }

                            
                            var stopWatch = new Stopwatch();
                            stopWatch.Start();

                            var dbSequenceResult = titleSequences.FirstOrDefault(t => t.InternalId == episode.InternalId) ?? new SequenceResult {Processed = false};

                            try
                            {
                                if (!AudioFingerprintManager.Instance.TitleFingerprintExists(episode) && !dbSequenceResult.Processed)
                                {
                                    AudioFingerprintManager.Instance.GetTitleSequenceFingerprint(episode, TimeSpan.FromMinutes(duration), cancellationToken);
                                }

                            }
                            catch (Exception ex)
                            {
                                stopWatch.Stop();
                                Log.Warn(ex.Message);
                            }

                            try
                            {
                                if (!AudioFingerprintManager.Instance.CreditFingerprintExists(episode) && !dbSequenceResult.Processed)
                                {
                                    AudioFingerprintManager.Instance.GetCreditSequenceFingerprint(episode, TimeSpan.FromMinutes(3), cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                stopWatch.Stop();
                                Log.Warn(ex.Message);
                            }

                            try
                            {
                                Log.Info($"FINGERPRINT: {series.Name} {seasonQuery.Items[seasonIndex].Name} Episode: {episode.IndexNumber} Credit and Title Sequence Fingerprinting Successful.");

                                repository.SaveResult(new SequenceResult()
                                {
                                    Duration = duration,
                                    TitleSequenceFingerprint = new List<uint>(),
                                    CreditSequenceFingerprint = new List<uint>(),
                                    HasTitleSequence = false,   //Set this to true when we scan the fingerprint data in the other scheduled task
                                    HasCreditSequence = false,  //Set this to true when we scan the fingerprint data in the other scheduled task
                                    IndexNumber = episode.IndexNumber,
                                    InternalId = episode.InternalId,
                                    SeasonId = seasonQuery.Items[seasonIndex].InternalId,
                                    SeriesId = series.InternalId,
                                    TitleSequenceStart = new TimeSpan(),
                                    TitleSequenceEnd = new TimeSpan(),
                                    CreditSequenceStart = new TimeSpan(),
                                    CreditSequenceEnd = episode.RunTimeTicks.HasValue ? TimeSpan.FromTicks(episode.RunTimeTicks.Value) : new TimeSpan(),
                                    Confirmed = false,
                                    Processed = false,
                                    HasRecap = false
                                }, cancellationToken);

                                Log.Info($"FINGERPRINT: {episode.Parent.Parent.Name} - S:{episode.Parent.IndexNumber} - E:{episode.IndexNumber} complete - {stopWatch.ElapsedMilliseconds} ms.");
                            }
                            catch (NullReferenceException)
                            {
                                //These is stream files. We'll just ignore the null exception. yup we are.
                                stopWatch.Stop();
                            }
                            catch (Exception ex)
                            {
                                stopWatch.Stop();
                                Log.Error(ex.Message); //but not all exceptions
                            }

                        }

                    }

                   

                });
            }
            catch (TaskCanceledException)
            {
                progress.Report(100.0);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex.Message, ex);
            }

            Log.Info("FINGERPRINT: Chromaprint Task Complete");

            var repo = repository as IDisposable;
            repo?.Dispose();
            progress.Report(100.0);


        }

        private double CalculateProgressStep()
        {
            var totalEpisodeCount = LibraryManager.GetItemsResult(new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Episode" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                IsVirtualItem = false

            }).TotalRecordCount;

            return 100.0 / totalEpisodeCount;
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
            //Log.Debug($"Title Sequence episodes count: {titleSequences.Count}");


            if (libraryItems.Count >= titleSequences.Count) return; // if we are equal nothing has change, if emby is more we'll pick up the new stuff next.
            
            titleSequences.Where(item => !libraryItems.Select(i => i.InternalId).Contains(item.InternalId))
                .AsParallel()
                .WithDegreeOfParallelism(5)
                .WithCancellation(cancellationToken)
                .ForAll(item =>
                {
                    try
                    {
                        repository.Delete(item.InternalId.ToString());
                    }
                    catch { }
                });

            try
            {
                repository.Vacuum();
            }
            catch { }
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