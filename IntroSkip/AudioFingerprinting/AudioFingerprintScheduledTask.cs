using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

// ReSharper disable ComplexConditionExpression
// ReSharper disable TooManyChainedReferences

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private IFfmpegManager FfmpegManager   { get; }
        private IFileSystem FileSystem         { get; }
        private IJsonSerializer JsonSerializer { get; }
        private IUserManager UserManager       { get; }
        private ILibraryManager LibraryManager { get; }
        private ILogger Log                    { get; }
      
        // ReSharper disable once TooManyDependencies
        public AudioFingerprintScheduledTask(IFfmpegManager ffmpegManager, IFileSystem fileSystem, IJsonSerializer jsonSerializer, ILogManager logMan, IUserManager userManager, ILibraryManager libraryManager)
        {
            FfmpegManager  = ffmpegManager;
            FileSystem     = fileSystem;
            JsonSerializer = jsonSerializer;
            UserManager    = userManager;
            LibraryManager = libraryManager;
            Log            = logMan.GetLogger(Plugin.Instance.Name);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Starting episode fingerprint task.");

            AudioFingerprintFileManager.Instance.RemoveAllAudioEncodings();

            ValidateSavedFingerprints();

            var config = Plugin.Instance.Configuration;

            var duration = config.EncodingLength;
           
            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            var step = 100.0 / seriesQuery.TotalRecordCount;
            var currentProgress = 0.0;

            Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, (series, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    state.Break();
                    progress.Report(100.0);
                }

                progress.Report((currentProgress += step) - 1);
                
                var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    Parent           = series,
                    Recursive        = true,
                    IncludeItemTypes = new[] { "Season" },
                    User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                    IsVirtualItem    = false
                });

                foreach (var season in seasonQuery.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent           = season,
                        Recursive        = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem    = false
                    });
                   
                    var separator = FileSystem.DirectorySeparatorChar;
                    
                    for (var index = 0; index <= episodeQuery.Items.Count() - 1; index++)
                    {
                        
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        var audioFileName          = $"{season.InternalId}{episodeQuery.Items[index].InternalId}";
                        var audioFilePath          = $"{AudioFingerprintFileManager.Instance.GetEncodingDirectory()}{separator}{audioFileName}.wav";
                        var fingerprintFileName    = AudioFingerprintFileManager.Instance.GetFingerprintFileNameHash(episodeQuery.Items[index]);
                        var fingerprintFolderName  = AudioFingerprintFileManager.Instance.GetFingerprintFolderNameHash(episodeQuery.Items[index]);
                        var fingerprintFilePath    = $"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{separator}{fingerprintFolderName}{separator}{fingerprintFileName}.json";
                        
                        
                        //if (FileSystem.FileExists(fingerprintFileOldPath))
                        //{
                        //    MoveFingerprintToFolder(fingerprintFileOldPath, fingerprintFilePath, fingerprintFolderName);
                        //    continue;
                        //}

                        if (FileSystem.FileExists(fingerprintFilePath))
                        {
                            continue;
                        }

                        ExtractPCMAudio($"{episodeQuery.Items[index].Path}", audioFilePath, duration);

                        try
                        {
                            var printData = FingerPrintAudio(audioFilePath);
                            AudioFingerprintFileManager.Instance.SaveFingerPrintToFile(episodeQuery.Items[index], printData);
                        }
                        catch (Exception ex)
                        {
                            Log.Info(ex.Message);
                        }
                    }

                    AudioFingerprintFileManager.Instance.RemoveAllSeasonAudioEncodings(season.InternalId);
                }
            });
            
            AudioFingerprintFileManager.Instance.RemoveAllAudioEncodings();
            progress.Report(100.0);
            

        }

        //private void MoveFingerprintToFolder(string fingerprintOldPath, string fingerPrintNewFilePath, string folderHash)
        //{
        //    var separator = FileSystem.DirectorySeparatorChar;
        //    var savePath  = $"{ AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{separator}{folderHash}{separator}";

        //    if (!FileSystem.DirectoryExists(savePath))
        //    {
        //        FileSystem.CreateDirectory(savePath);
        //    }

        //    try
        //    {
        //        FileSystem.CopyFile(fingerprintOldPath, fingerPrintNewFilePath, true);
        //        FileSystem.DeleteFile(fingerprintOldPath);

        //    }catch{}
        //}

        private AudioFingerprintDto FingerPrintAudio(string inputFileName)
        {
            var config       = Plugin.Instance.Configuration;
            var duration     = config.EncodingLength * 60;
            var separator    = FileSystem.DirectorySeparatorChar;
            var encodingPath = $"{AudioFingerprintFileManager.Instance.GetEncodingDirectory()}{separator}";
            
            var @params = $"\"{inputFileName}\" -raw -length {duration} -json";
            var fpcalc = (OperatingSystem.IsWindows() ? "fpcalc.exe" : "fpcalc");

            var procStartInfo = new ProcessStartInfo($"{encodingPath}{fpcalc}", @params)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,

            };

            var process = new Process { StartInfo = procStartInfo };

            process.Start();

            string processOutput = null;
            var json = "";

            while ((processOutput = process.StandardOutput.ReadLine()) != null)
            {
                json += (processOutput);
            }

            if (string.IsNullOrEmpty(json)) throw new Exception("Fingerprint Data Null");

            return JsonSerializer.DeserializeFromString<AudioFingerprintDto>(json);
        }

        // ReSharper disable once InconsistentNaming
        private void ExtractPCMAudio(string input, string audioOut, int duration)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath          = ffmpegConfiguration.EncoderPath;

            var procStartInfo = new ProcessStartInfo(ffmpegPath, $"-t 00:{duration}:00 -i \"{input}\" -ac 1 -acodec pcm_s16le -ar 16000 \"{audioOut}\"")
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
                    //Log.Info(processOutput);
                }
            }

        }

        private void ValidateSavedFingerprints()
        {
            // High multi-threading of the fingerprinting process may have some side effects.
            // If it is rushed on slower machines, durations will be shortened.
            // Durations 'must' equals the ffmpeg encoding length for perfect results.
            // The finger print file may also be empty.
            // Remove these error files so they get rescanned.

            //var config           = Plugin.Instance.Configuration;
            //var encodingDuration = config.EncodingLength;

            var separator        = FileSystem.DirectorySeparatorChar;
            var fingerprintFiles = FileSystem.GetFiles($"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{separator}", true).Where(file => file.Extension == ".json").ToList();

            if (!fingerprintFiles.Any()) return;

            foreach (var file in fingerprintFiles)
            {
                var remove = false;
                using (var sr = new StreamReader(file.FullName))
                {
                    var json = sr.ReadToEnd();
                    if (string.IsNullOrEmpty(json))
                    {
                        remove = true;
                    }
                    else
                    {
                        var printData = JsonSerializer.DeserializeFromString<AudioFingerprintDto>(json);
                       
                        //Our minimum encoding is 10 minutes. If there are any fingerprint encodings under 10 minutes, try and re-fingerprint the audio.
                        if (printData.duration < 600 - 2) //Leave room for an encoding error of 2 seconds.
                        {
                            remove = true;
                        }
                    }
                }

                if (!remove) continue;
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }
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

        public string Name        => "Episode Audio Fingerprinting";
        public string Key         => "Audio Fingerprint Options";
        public string Description => "Chroma-print audio files for title sequence detection";
        public string Category    => "Intro Skip";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;
    }
}
