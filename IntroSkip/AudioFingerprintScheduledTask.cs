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
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

namespace IntroSkip
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
            RemoveAllPreviousEncodings();

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

            Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, series =>
            {
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
                        var fileName        = $"{season.InternalId}{episodeQuery.Items[index].InternalId}";
                        var fingerPrint     = $"{TitleSequenceEncoding.Instance.FingerPrintDir}{separator}{fileName}.json";
                        var fingerPrintHash = $"{TitleSequenceEncoding.Instance.FingerPrintDir}{separator}{TitleSequenceEncoding.Instance.CreateMD5(episodeQuery.Items[index].Path)}.json";
                        var waveFile        = $"{TitleSequenceEncoding.Instance.EncodingDir}{separator}{fileName}.wav";

                        if (FileSystem.FileExists(fingerPrint))
                        {
                            TitleSequenceEncoding.Instance.MigrateCurrentFingerPrints(fingerPrint, episodeQuery.Items[index]);
                            continue;
                        }

                        if (FileSystem.FileExists(fingerPrintHash))
                        {
                            continue;
                        }

                        ExtractPCMAudio($"{episodeQuery.Items[index].Path}", waveFile, duration);

                        try
                        {
                            var printData = FingerPrintAudio(waveFile);
                            SaveFingerPrintToFile(episodeQuery.Items[index], printData);
                        }
                        catch (Exception ex)
                        {
                            Log.Info(ex.Message);
                        }
                    }
                }
            });

            progress.Report(100.0);
            RemoveAllPreviousEncodings();

        }


        private void RemoveAllPreviousEncodings()
        {
            var separator          = FileSystem.DirectorySeparatorChar;
            var introEncodingPath  = $"{TitleSequenceEncoding.Instance.EncodingDir}{separator}";
            var files              = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();

            if (!fileSystemMetadata.Any()) return;
            Log.Info("Removing all encoding files");
            foreach (var file in fileSystemMetadata)
            {
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }
        }

        private void RemoveAllPreviousSeasonEncodings(long internalId)
        {
            var separator          = FileSystem.DirectorySeparatorChar;
            var introEncodingPath  = $"{TitleSequenceEncoding.Instance.EncodingDir}{separator}";
            var files              = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();

            if (!fileSystemMetadata.Any()) return;

            foreach (var file in fileSystemMetadata)
            {
                if (file.Name.Substring(0, internalId.ToString().Length) != internalId.ToString()) continue;
                Log.Info($"Removing encoding file {file.FullName}");
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }
        }

        private void SaveFingerPrintToFile(BaseItem episode, AudioFingerprint audioFingerprint)
        {
            var fingerprintFileName = TitleSequenceEncoding.Instance.CreateMD5(episode.Path);
            var filePath = $"{TitleSequenceEncoding.Instance.FingerPrintDir}{FileSystem.DirectorySeparatorChar}{fingerprintFileName}.json";

            if (audioFingerprint is null)
            {
                Log.Info("Fingerprint was null");
                return;
            }

            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(JsonSerializer.SerializeToString(audioFingerprint));
                sw.Flush();
            }
        }

        private AudioFingerprint FingerPrintAudio(string inputFileName)
        {
            var config       = Plugin.Instance.Configuration;
            var duration     = config.EncodingLength * 60;
            var separator    = FileSystem.DirectorySeparatorChar;
            var encodingPath = $"{TitleSequenceEncoding.Instance.EncodingDir}{separator}";
            var @params      = $"\"{inputFileName}\" -raw -length {duration} -json";
            var fpcalc       = (OperatingSystem.IsWindows() ? "fpcalc.exe" : "fpcalc");

            var procStartInfo = new ProcessStartInfo($"{encodingPath}{fpcalc}", @params)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,

            };

            var process = new Process { StartInfo = procStartInfo };

            process.Start();

            string processOutput = null;
            var json = "";

            while ((processOutput = process.StandardOutput.ReadLine()) != null)
            {
                //Logger.Info(processOutput);
                json += (processOutput);
            }

            if (string.IsNullOrEmpty(json)) throw new Exception("Json Data Null");

            return JsonSerializer.DeserializeFromString<AudioFingerprint>(json);
        }

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
            var config           = Plugin.Instance.Configuration;
            var encodingDuration = config.EncodingLength;
            var separator        = FileSystem.DirectorySeparatorChar;
            var fingerprintFiles = FileSystem.GetFiles($"{TitleSequenceEncoding.Instance.FingerPrintDir}{separator}", true).Where(file => file.Extension == ".json").ToList();

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
                        var printData = JsonSerializer.DeserializeFromString<AudioFingerprint>(json);
                        if (printData.duration < (encodingDuration * 60))
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
            Log.Info("Fingerprint validation complete.");
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
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
