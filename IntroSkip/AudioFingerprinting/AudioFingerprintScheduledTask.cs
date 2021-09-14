using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Data;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

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

        private IDtoService DtoService { get; set; }
        // ReSharper disable once TooManyDependencies
        public AudioFingerprintScheduledTask(IFfmpegManager ffmpegManager, IFileSystem fileSystem, ILogManager logMan, IUserManager userManager, ILibraryManager libraryManager, ITaskManager taskManager, IDtoService dtoService)
        {
            FfmpegManager = ffmpegManager;
            FileSystem = fileSystem;
            UserManager = userManager;
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            DtoService = dtoService;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var tasks = TaskManager.ScheduledTasks.ToList();
            if(tasks.FirstOrDefault(task => task.Name == "Episode Title Sequence Detection").State == TaskState.Running)
            {
                Log.Info("Chroma-printing task will wait until title sequence task has finished.");
                progress.Report(100.0);
                return;
            }
            if (cancellationToken.IsCancellationRequested)
            {
                progress.Report(100.0);
            }
            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
           
            try
            {
                Log.Info("Starting episode fingerprint task.");

                

                var config = Plugin.Instance.Configuration;
                               
                var seriesInternalItemQuery = new InternalItemsQuery()
                {
                    Recursive        = true,
                    IncludeItemTypes = new[] { "Series" },
                    User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                    OrderBy          = new[] { ItemSortBy.SortName }.Select(i => new ValueTuple<string, SortOrder>(i, SortOrder.Descending)).ToArray()
                };

                

                var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);

                

                var step = 100.0 / seriesQuery.TotalRecordCount;
                var currentProgress = 0.1;
               

                //Our database info
                QueryResult<TitleSequenceResult> dbResults = null;
                List<TitleSequenceResult> titleSequences = null;
                try
                {
                    dbResults = repo.GetResults(new TitleSequenceResultQuery());
                    titleSequences = dbResults.Items.ToList();
                    Log.Info($"Chroma-print database contains {dbResults.TotalRecordCount} items.");
                }
                catch(Exception)
                {
                    Log.Info("Title sequence datbase is new.");
                    titleSequences = new List<TitleSequenceResult>();
                }

                progress.Report((currentProgress += step) - 1); //Give the user some kind of progress to show the task has started
                                

                Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = config.FingerprintingMaxDegreeOfParallelism }, (series, state) =>
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

                        var processedEpisodeResults = titleSequences.Where(s => s.SeasonId == seasonQuery.Items[seasonIndex].InternalId);

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
                            
                            Log.Info($"{series.Name} - {seasonQuery.Items[seasonIndex].Name} chromaprint profile is up to date.");
                            continue;
                        }

                        var averageRuntime = GetSeasonRuntimeAverage(episodeQuery.Items);
                        var duration = GetEncodingDuration(averageRuntime);
                                               
                        for (var index = 0; index <= episodeQuery.Items.Count() - 1; index++)
                        {

                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            //The episode data exists in the database
                            if (titleSequences.Exists(result => result.InternalId == episodeQuery.Items[index].InternalId))
                            {
                                var titleSequenceResult = titleSequences.FirstOrDefault(result => result.InternalId == episodeQuery.Items[index].InternalId);
                                                               
                                if (titleSequenceResult.Duration == duration)
                                {
                                    continue;
                                }
                                else  //If new episodes are added to the season it may alter the encoding duration for the fingerprint. The duration for all fingerprints must be the same.
                                {
                                    Log.Info($"Encoding duration has changed for {series.Name} - {seasonQuery.Items[seasonIndex].Name}");
                                    repo.Delete(titleSequenceResult.InternalId.ToString());

                                    dbResults = repo.GetResults(new TitleSequenceResultQuery());

                                    titleSequences = dbResults.Items.ToList();
                                    processedEpisodeResults = titleSequences.Where(s => s.SeasonId == seasonQuery.Items[seasonIndex].InternalId);
                                }
                            }


                            var separator = FileSystem.DirectorySeparatorChar;
                            var fingerprintBinFileName = $"{seasonQuery.Items[seasonIndex].InternalId} - {episodeQuery.Items[index].InternalId}.bin";
                            var fingerprintBinFilePath = $"{AudioFingerprintFileManager.Instance.GetEncodingDirectory()}{separator}{fingerprintBinFileName}";

                            //Log.Info($"{episodeQuery.Items[index].Parent.Parent.Name} - S:{episodeQuery.Items[index].Parent.IndexNumber} - E:{episodeQuery.Items[index].IndexNumber}: encoding chromaprint.");
                            var stopWatch = new Stopwatch();
                            stopWatch.Start();

                            ExtractFingerprintBinaryData($"{episodeQuery.Items[index].Path}", fingerprintBinFilePath, duration, cancellationToken);

                            Task.Delay(300); //Give enough time for ffmpeg to save the file.
                                               

                            List<uint> fingerPrintData = null;

                            try
                            {
                                fingerPrintData = SplitByteData($"{fingerprintBinFilePath}", episodeQuery.Items[index] );
                            }
                            catch (Exception ex)
                            {
                                stopWatch.Stop();
                                Log.Warn(ex.Message);
                                continue;
                            }

                            try
                            {
                                Log.Info($"{series.Name} - S:{seasonQuery.Items[seasonIndex].IndexNumber} - E:{episodeQuery.Items[index].IndexNumber}: Saving.");
                                repo.SaveResult(new TitleSequenceResult()
                                {
                                    Duration           = duration,
                                    Fingerprint        = fingerPrintData,
                                    HasSequence        = false, //Set this to true when we scan the fingerprint data in the other scheduled task
                                    IndexNumber        = episodeQuery.Items[index].IndexNumber,
                                    InternalId         = episodeQuery.Items[index].InternalId,
                                    SeasonId           = seasonQuery.Items[seasonIndex].InternalId,
                                    SeriesId           = series.InternalId,
                                    TitleSequenceStart = new TimeSpan(),
                                    TitleSequenceEnd   = new TimeSpan(),
                                    Confirmed          = false,
                                    Processed          = false
                                }, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                stopWatch.Stop();
                                Log.Warn(ex.Message);
                            }                            
                            
                            Log.Info($"{episodeQuery.Items[index].Parent.Parent.Name} - S:{episodeQuery.Items[index].Parent.IndexNumber} - E:{episodeQuery.Items[index].IndexNumber} complete - {stopWatch.ElapsedMilliseconds / 1000} seconds.");
                            
                        }
                                     
                    }
                    progress.Report((currentProgress += step) - 1);
                });
            }
            catch (TaskCanceledException)
            {
                progress.Report(100.0);
            }
            Log.Info("Chromaprint Task Complete");
            //repo.Vacuum();
            progress.Report(100.0);


        }
               

        private void ExtractFingerprintBinaryData(string input, string output, int duration, CancellationToken cancelationToken, string titleSequenceStart = "00:00:00")
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;

            var args = new[]
            {
                $"-ss {titleSequenceStart}",
                $"-t 00:{duration}:00",
                $"-i \"{input}\"",
                "-ac 1",
                "-acodec pcm_s16le",
                "-ar 16000", //11025
                "-c:v nul",
                "-f chromaprint",
                "-fp_format raw",
                $"\"{output}\""
            };

            var procStartInfo = new ProcessStartInfo(ffmpegPath, string.Join(" ", args))
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using (var process = new Process { StartInfo = procStartInfo })
            {
                process.Start();

                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //
                // Very Important!
                // Leave the standardError.ReadLine in place for this process,
                // do not remove it,
                // or the process will kickoff, and the task will apparently complete before any of the encodings are finished,
                // resulting in no fingerprinted audio!!
                //
                //////////////////////////////////////////////////////////////////////////////////////////////////////////////////


                string processOutput = null;

                while ((processOutput = process.StandardError.ReadLine()) != null)
                {
                    if (cancelationToken.IsCancellationRequested)
                    {
                        try 
                        {
                            process.Kill(); //At least try and kill ffmpeg.
                        }
                        catch { }
                    }
                    //Log.Info(processOutput);
                }

                //Log.Info($"Chroma-print binary extraction successful: { input }");

            }
        }

        private List<uint> SplitByteData(string bin, BaseItem item)
        {
            Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: Extracting chunks from binary chroma-print.");
            if (!FileSystem.FileExists(bin))
            {
                Log.Warn($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber} .bin file doesn't exist.");
                throw new Exception("bin file doesn't exist");
            }
            var fingerprint = new List<uint>();
            using (BinaryReader b = new BinaryReader(File.Open(bin, FileMode.Open)))
            {
                int length = (int)b.BaseStream.Length / sizeof(uint);
                for (int i = 0; i < length; i++)
                {
                    fingerprint.Add(b.ReadUInt32());
                }
            }
            AudioFingerprintFileManager.Instance.RemoveEpisodeFingerprintBinFile(bin, item);
            return fingerprint;
        }
               

        private int GetEncodingDuration(TimeSpan? averageRuntime)
        {
            if(averageRuntime is null) return 15;

            if (averageRuntime >= TimeSpan.FromMinutes(40))
            {
               return 20;
            }
                        
            return 15;
            
        }
        private TimeSpan? GetSeasonRuntimeAverage(BaseItem[] episodes)
        {
            var totalCount = episodes.Count();
            long? runtimeSum = 0L;
            Parallel.ForEach(episodes, (e) => {
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
            catch(Exception)
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

        public string Name => "Episode Audio Fingerprinting";
        public string Key => "Audio Fingerprint Options";
        public string Description => "Chroma-print audio files for title sequence detection";
        public string Category => "Intro Skip";
        public bool IsHidden => false;
        public bool IsEnabled => true;
        public bool IsLogged => true;
    }
}
