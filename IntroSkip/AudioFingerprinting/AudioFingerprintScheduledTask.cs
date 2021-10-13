using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Data;

// ReSharper disable ComplexConditionExpression
// ReSharper disable TooManyChainedReferences

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private IFfmpegManager FfmpegManager { get; }
        private IFileSystem FileSystem { get; }
        private IUserManager UserManager { get; }
        private ILibraryManager LibraryManager { get; }
        private ILogger Log { get; }
        private ITaskManager TaskManager { get; set; }

        //private IDtoService DtoService { get; set; }
        // ReSharper disable once TooManyDependencies
        public AudioFingerprintScheduledTask(IFfmpegManager ffmpegManager, IFileSystem fileSystem, ILogManager logMan, IUserManager userManager, ILibraryManager libraryManager, ITaskManager taskManager)
        {
            FfmpegManager = ffmpegManager;
            FileSystem = fileSystem;
            UserManager = userManager;
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var tasks = TaskManager.ScheduledTasks.ToList();
            if (tasks.FirstOrDefault(task => task.Name == "Episode Title Sequence Detection").State == TaskState.Running)
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
                RepositoryItemSync(repository);
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

                if (config.IgnoredList.Count > 0)
                {
                    seriesInternalItemQuery.ExcludeItemIds = config.IgnoredList.ToArray();
                }


                var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);

                var step = 100.0 / seriesQuery.TotalRecordCount;
                var currentProgress = 0.1;

                //Our database info
                QueryResult<TitleSequenceResult> dbResults = null;
                List<TitleSequenceResult> titleSequences = null;
                try
                {
                    dbResults = repository.GetResults(new TitleSequenceResultQuery());
                    titleSequences = dbResults.Items.ToList();
                    Log.Info($"FINGERPRINT: Chroma-print database contains {dbResults.TotalRecordCount} items.");
                }
                catch (Exception)
                {
                    Log.Info("Title sequence database is new.");
                    titleSequences = new List<TitleSequenceResult>();
                }

                progress.Report((currentProgress += step) - 1); //Give the user some kind of progress to show the task has started

                //We divide by two because we are going to split up the parallel function for both series and episodes.
                var fpMax = config.FingerprintingMaxDegreeOfParallelism / 2;

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
                         var processedEpisodeResults =
                             titleSequences.Where(s =>
                                 s.SeasonId == seasonQuery.Items[seasonIndex].InternalId); //Items we have fingerprinted.

                         var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                         {
                             Parent = seasonQuery.Items[seasonIndex],
                             Recursive = true,
                             IncludeItemTypes = new[] { "Episode" },
                             User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                             IsVirtualItem = false
                         });

                        //The season has been processed and all episodes have a sequence - move on.                        
                        if (processedEpisodeResults.Count() == episodeQuery.TotalRecordCount)
                         {
                             Log.Debug(
                                 $"FINGERPRINT: {series.Name} - {seasonQuery.Items[seasonIndex].Name} chromaprint profile is up to date.");
                             continue;
                         }

                         var averageRuntime = GetSeasonRuntimeAverage(episodeQuery.Items);
                         var duration = GetEncodingDuration(averageRuntime);


                         Parallel.ForEach(episodeQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = (int)Math.Round((double)fpMax / 2, MidpointRounding.AwayFromZero) }, (episode, st) =>
                           {
                               if (cancellationToken.IsCancellationRequested)
                               {
                                   st.Break();
                               }

                               //The episode data exists in the database
                               // ReSharper disable twice AccessToModifiedClosure <-- no again, it's right there!
                               if (titleSequences.Exists(result => result.InternalId == episode.InternalId))
                               {
                                   var titleSequenceResult =
                                     titleSequences.FirstOrDefault(result =>
                                         result.InternalId == episode.InternalId);

                                  // ReSharper disable once PossibleNullReferenceException <-- no it's not null, it was existing right up there...
                                  if (titleSequenceResult.Duration == duration)
                                   {
                                       return;
                                   }
                                   else //If new episodes are added to the season it may alter the encoding duration for the fingerprint. The duration for all fingerprints must be the same.
                                   {
                                      Log.Info(
                                          $"Encoding duration has changed for {series.Name} - {seasonQuery.Items[seasonIndex].Name}");
                                      repository.Delete(titleSequenceResult.InternalId.ToString());

                                      dbResults = repository.GetResults(new TitleSequenceResultQuery());

                                      titleSequences = dbResults.Items.ToList();
                                      processedEpisodeResults = titleSequences.Where(s =>
                                          s.SeasonId == seasonQuery.Items[seasonIndex].InternalId);
                                   }
                               }

                               var stopWatch = new Stopwatch();
                               stopWatch.Start();

                               List<uint> fingerPrintData = null;

                               try
                               {
                                   fingerPrintData =
                                     AudioFingerprintManager.Instance.GetAudioFingerprint(episode,
                                         cancellationToken,
                                         duration);
                               }
                               catch (Exception ex)
                               {
                                   stopWatch.Stop();
                                   Log.Warn(ex.Message);
                                   return;
                               }


                               try
                               {
                                   Log.Info(
                                     $"{series.Name} - S:{seasonQuery.Items[seasonIndex].IndexNumber} - E:{episode.IndexNumber}: Saving.");
                                   repository.SaveResult(new TitleSequenceResult()
                                   {
                                       Duration = duration,
                                       Fingerprint = fingerPrintData,
                                       HasSequence = false, //Set this to true when we scan the fingerprint data in the other scheduled task
                                       IndexNumber = episode.IndexNumber,
                                       InternalId = episode.InternalId,
                                       SeasonId = seasonQuery.Items[seasonIndex].InternalId,
                                       SeriesId = series.InternalId,
                                       TitleSequenceStart = new TimeSpan(),
                                       TitleSequenceEnd = new TimeSpan(),
                                       Confirmed = false,
                                       Processed = false

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
            if (averageRuntime is null) return 15;

            if (averageRuntime >= TimeSpan.FromMinutes(40))
            {
                return 20;
            }

            if (averageRuntime <= TimeSpan.FromMinutes(13))
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
                result = TimeSpan.FromTicks(runtimeSum.Value / totalCount);
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

        private void RepositoryItemSync(ITitleSequenceRepository repository)
        {
            var titleSequencesQuery = repository.GetBaseTitleSequenceResults(new TitleSequenceResultQuery());
            var titleSequences = titleSequencesQuery.Items.ToList();

            var libraryQuery = LibraryManager.GetItemsResult(new InternalItemsQuery() { Recursive = true, IsVirtualItem = false, IncludeItemTypes = new[] { "Episode" } });
            var libraryItems = libraryQuery.Items.ToList();

            Log.Debug($"Library episodes count:        {libraryItems.Count}");
            Log.Debug($"Title Sequence episodes count: {titleSequences.Count}");
            if (libraryItems.Count >= titleSequences.Count) return; // if we are equal nothing has change, if emby is more we'll pick up the new stuff next.

            titleSequences.Where(item => !libraryItems.Select(i => i.InternalId).Contains(item.InternalId))
                .AsParallel()
                .WithDegreeOfParallelism(5)
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