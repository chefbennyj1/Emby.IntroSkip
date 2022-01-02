using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
        public static AudioFingerprintManager Instance { get; private set; }
        private IFileSystem FileSystem                 { get; }
        private IApplicationPaths ApplicationPaths     { get; }
        private char Separator                         { get; }
        private IFfmpegManager FfmpegManager           { get; }
        private ILogger Log                            { get; }
        //While the ffmpeg process is being run inside a parallel loop, it is possible that it may not end correctly
        //Keep track of all the ffmpeg processes, and make sure that they have ended correctly.
        private ConcurrentDictionary<long, int> FfmpegProcessMonitor = new ConcurrentDictionary<long, int>();

        public AudioFingerprintManager(IFileSystem file, IFfmpegManager ffmpeg, ILogManager logManager, IApplicationPaths applicationPaths)
        {
            Instance         = this;
            FileSystem       = file;
            FfmpegManager    = ffmpeg;
            ApplicationPaths = applicationPaths;
            Separator        = FileSystem.DirectorySeparatorChar;
            Log              = logManager.GetLogger(Plugin.Instance.Name);
        }

        public List<uint> GetAudioFingerprint(BaseItem episode, CancellationToken cancellationToken, TimeSpan duration, bool isTitleSequence = true)
        {
            var separator              = FileSystem.DirectorySeparatorChar;
            var fingerprintBinFileName = $"{(isTitleSequence ? "title_sequence" : "credit_sequence")} {episode.Parent.InternalId} - {episode.InternalId}.bin";
            var fingerprintBinFilePath = $"{GetEncodingDirectory()}{separator}{fingerprintBinFileName}";
            var titleEncodingSequenceStart  = TimeSpan.Zero;
            

            var sequenceEncodingStart = isTitleSequence
                ? titleEncodingSequenceStart
                : TimeSpan.FromTicks(episode.RunTimeTicks.Value) - duration; 

            ExtractFingerprintBinaryData(episode, fingerprintBinFilePath, duration, cancellationToken, sequenceEncodingStart);

            //Task.Delay(300, cancellationToken); //Give enough time for ffmpeg to save the file.

            List<uint> fingerprints = null;
            try
            {
                fingerprints = SplitByteData(fingerprintBinFilePath.AsSpan(), episode);
            }
            catch (Exception) //<--it's logged already
            {
                
            }

            

            return fingerprints;
        }

        private void ExtractFingerprintBinaryData(BaseItem item, string output, TimeSpan duration, CancellationToken cancellationToken, TimeSpan sequenceEncodingStart)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
            var input = item.Path;
            /*
              gausssize, g
              Set the Gaussian filter window size. In range from 3 to 301, must be odd number. Default is 31. 
              Probably the most important parameter of the Dynamic Audio Normalizer is the window size of the Gaussian smoothing filter. 
              The filter’s window size is specified in frames, centered around the current frame. For the sake of simplicity, this must be an odd number. 
              Consequently, the default value of 31 takes into account the current frame, as well as the 15 preceding frames and the 15 subsequent frames. 
              Using a larger window results in a stronger smoothing effect and thus in less gain variation, i.e. slower gain adaptation. 
              Conversely, using a smaller window results in a weaker smoothing effect and thus in more gain variation, i.e. faster gain adaptation. 
              In other words, the more you increase this value, the more the Dynamic Audio Normalizer will behave like a "traditional" normalization filter. 
              On the contrary, the more you decrease this value, the more the Dynamic Audio Normalizer will behave like a dynamic range compressor.

              compress, s
              Set the compress factor. In range from 0.0 to 30.0. Default is 0.0.

              peak, p
              Set the target peak value. This specifies the highest permissible magnitude level for the normalized audio input. 

             maxgain, m
             Set the maximum gain factor. In range from 1.0 to 100.0. Default is 10.0.
             */
            var args = new[]
            {
                $"-ss {sequenceEncodingStart}",
                $"-t {duration}",
                $"-i \"{input}\"",
                "-ac 1",
                "-acodec pcm_s16le", 
                "-ar 16000", //11025
                //"-af \"dynaudnorm=p=0.75:m=10.0:s=8:g=3\"",
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

                //Add the ffmpeg process id to the concurrent dictionary.
                //We have to check later that ffmpeg completed and ended properly.
                FfmpegProcessMonitor.TryAdd(item.InternalId, process.Id);

               
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

               

            }
        }



        private List<uint> SplitByteData(ReadOnlySpan<char> bin, BaseItem item)
        {
            Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: Extracting chunks from binary chroma-print.");
            if (!FileSystem.FileExists(bin.ToString()))
            {
                Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber} .bin file doesn't exist. Ensure FFMPEG can handle Chromprinting Audio.");
                throw new Exception("bin file doesn't exist");
            }
            var fingerprint = new List<uint>();
            using (var b = new BinaryReader(File.Open(bin.ToString(), FileMode.Open)))
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

        private void RemoveEpisodeFingerprintBinFile(ReadOnlySpan<char> path, BaseItem item)
        {

            if (!FileSystem.FileExists(path.ToString())) return;
            try
            {
                FileSystem.DeleteFile(path.ToString());
                Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: .bin file removed.");
                EnsureFfmpegEol(item.InternalId);
                
            }
            catch { }
        }

        private string GetEncodingDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding";
        }

        private bool EnsureFfmpegEol(long internalId)
        {
            var process = Process.GetProcesses()
                .Where(p => p.Id == FfmpegProcessMonitor.FirstOrDefault(s => s.Key == internalId).Value).ToList().FirstOrDefault();

            if (process is null)
            {
                //Log.Debug("Ffmpeg fingerprint instance exited successfully.");
            }
            else
            {
                Log.Debug("Ffmpeg fingerprint instance exiting...");
                try
                {
                    process.Kill();
                }
                catch
                {
                
                }
            }
            return FfmpegProcessMonitor.TryRemove(internalId, out int instance);
        }

        public bool HasChromaprint()
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
            var args = new[]
            {
                "-version",
               
            };

            var procStartInfo = new ProcessStartInfo(ffmpegPath, string.Join(" ", args))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = new Process {StartInfo = procStartInfo})
            {
                process.Start();
                string processOutput = null;

                while ((processOutput = process.StandardOutput.ReadLine()) != null)
                {
                    if (processOutput.Contains("chromaprint"))
                    {
                        return true;
                    }
                }
            }
            return false;
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
