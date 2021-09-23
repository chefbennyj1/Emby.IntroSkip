using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintManager : IServerEntryPoint
    {
        public static AudioFingerprintManager Instance { get; set; }
        private IFileSystem FileSystem { get; }
        private IApplicationPaths ApplicationPaths { get; }
        private char Separator { get; }
        private IFfmpegManager FfmpegManager { get; }
        private ILogger Log { get; }

        public AudioFingerprintManager(IFileSystem file, IFfmpegManager ffmpeg, ILogManager logManager, IApplicationPaths applicationPaths)
        {
            Instance = this;
            FileSystem = file;
            FfmpegManager = ffmpeg;
            ApplicationPaths = applicationPaths;
            Separator = FileSystem.DirectorySeparatorChar;
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }

        public List<uint> GetAudioFingerprint(BaseItem episode, CancellationToken cancellationToken, int duration)
        {
            var separator = FileSystem.DirectorySeparatorChar;
            var fingerprintBinFileName = $"{episode.Parent.InternalId} - {episode.InternalId}.bin";
            var fingerprintBinFilePath = $"{GetEncodingDirectory()}{separator}{fingerprintBinFileName}";

            ExtractFingerprintBinaryData($"{episode.Path}", fingerprintBinFilePath, duration, cancellationToken);

            Task.Delay(300, cancellationToken); //Give enough time for ffmpeg to save the file.

            return SplitByteData($"{fingerprintBinFilePath}", episode);
        }

        private void ExtractFingerprintBinaryData(string input, string output, int duration, CancellationToken cancellationToken, string titleSequenceStart = "00:00:00")
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;

            var args = new[]
            {
                $"-ss {titleSequenceStart}",
                $"-t 00:{duration}:00",
                $"-i \"{input}\"",
                "-ac 1",
                "-acodec pcm_s16le", //What happens if we encode mono AAC?
                "-ar 16000", //11025
                "-c:v nul",
                "-f chromaprint",
                "-fp_format raw",
                $"\"{output}\""
            };

            var procStartInfo = new ProcessStartInfo(ffmpegPath, string.Join(" ", args))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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


                // ReSharper disable once NotAccessedVariable <-- Resharper is incorrect. It is being used
                string processOutput = null;

                while ((processOutput = process.StandardError.ReadLine()) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
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
            RemoveEpisodeFingerprintBinFile(bin, item);
            return fingerprint;
        }

        private void RemoveEpisodeFingerprintBinFile(string path, BaseItem item)
        {
            if (!FileSystem.FileExists(path)) return;
            try
            {
                FileSystem.DeleteFile(path);
                Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: .bin file removed.");
            }
            catch { }
        }

        private string GetEncodingDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding";
        }

        public void Dispose()
        {
            
        }

        public void Run()
        {
            var encodingDir = GetEncodingDirectory();
            if (!FileSystem.DirectoryExists($"{encodingDir}")) FileSystem.CreateDirectory($"{encodingDir}");
        }
    }
}
