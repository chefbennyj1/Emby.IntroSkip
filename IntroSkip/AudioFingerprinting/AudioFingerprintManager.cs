using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        private IFfmpegManager FfmpegManager           { get; }
        private ILogger Log                            { get; }

        //While the ffmpeg process is being run inside a parallel loop, it is possible that it may not end correctly
        //Keep track of all the ffmpeg processes, and make sure that they have ended correctly.
        /// <summary>
        /// Key: output file name, Value: ffmpeg process ID
        /// </summary>
        private readonly ConcurrentDictionary<string, int> FfmpegProcessMonitor = new ConcurrentDictionary<string, int>();

        public AudioFingerprintManager(IFileSystem file, IFfmpegManager ffmpeg, ILogManager logManager, IApplicationPaths applicationPaths)
        {
            Instance         = this;
            FileSystem       = file;
            FfmpegManager    = ffmpeg;
            ApplicationPaths = applicationPaths;
            Log              = logManager.GetLogger(Plugin.Instance.Name);
        }
        
        private byte[] GetHash(string inputString)
        {
            using (HashAlgorithm algorithm = SHA256.Create())
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        private string GetHashString(string inputString)
        {
            var sb = new StringBuilder();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
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
                FfmpegProcessMonitor.TryAdd(output, process.Id);
                
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
        
        private List<uint> SplitByteData(string bin, BaseItem item)
        {
            var fingerprint = new List<uint>();
            if (!FileSystem.FileExists(bin))
            {
                Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber} .bin file doesn't exist. Ensure FFMPEG can handle Chromprinting Audio.");
                return fingerprint;
            }

            
            try
            {
                using (var fileStream = new FileStream(bin, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var b = new BinaryReader(fileStream))
                    {
                        int length = (int) b.BaseStream.Length / sizeof(uint);
                        for (int i = 0; i < length; i++)
                        {
                            fingerprint.Add(b.ReadUInt32());
                        }
                    }
                    
                }
            }
            catch (IOException)
            {
                
            }

            //RemoveEpisodeFingerprintBinFile(bin, item);
            return fingerprint;
        }

        public void RemoveEpisodeFingerprintBinFiles(long internalId)
        {
            var creditChromaprintBinaryFilePath = Path.Combine(GetEncodingDirectory(), GetHashString($"credit_sequence{internalId}") + ".bin");
            var titleChromaprintBinaryFilePath = Path.Combine(GetEncodingDirectory(), GetHashString($"title_sequence{internalId}") + ".bin");

            if (FileSystem.FileExists(creditChromaprintBinaryFilePath))
            {
                try
                {
                    FileSystem.DeleteFile(creditChromaprintBinaryFilePath);
                    //Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: .bin file removed.");
                    //EnsureFfmpegEol(item.InternalId);
                }
                catch { }
            }
            if (FileSystem.FileExists(titleChromaprintBinaryFilePath))
            {
                try
                {
                    FileSystem.DeleteFile(titleChromaprintBinaryFilePath);
                    //Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: .bin file removed.");
                    //EnsureFfmpegEol(item.InternalId);
                }
                catch { }
            }
            
        }

        

        public List<uint> GetCreditSequenceFingerprint(BaseItem episode, TimeSpan duration, CancellationToken cancellationToken)
        {
            var chromaprintBinaryFilePath = Path.Combine(GetEncodingDirectory(), GetHashString($"credit_sequence{episode.InternalId}") + ".bin");
            
            if (CreditFingerprintExists(episode)) return SplitByteData(chromaprintBinaryFilePath, episode); //<-- return the print is it already exists

            if (!episode.RunTimeTicks.HasValue)
            {
                Log.Warn($"{episode.Parent.Parent.Name} {episode.Parent.Name} Episode {episode.IndexNumber} currently has no runtime value. Can not calculate end credit location...");
                return new List<uint>(); //<--Empty array. We can't calculate the end credits here. We'll have to wait until the runtime value is calculated by emby, and come back to this later
            } 

            var sequenceEncodingStart = TimeSpan.FromTicks(episode.RunTimeTicks.Value) - duration;

            ExtractFingerprintBinaryData(episode, chromaprintBinaryFilePath, duration, cancellationToken, sequenceEncodingStart);
            
            if (!EnsureFfmpegEol(chromaprintBinaryFilePath)) Log.Warn("ffmpeg process key still available in credit sequence process dictionary...OK");
            
            return SplitByteData(chromaprintBinaryFilePath, episode);

        }

        public List<uint> GetTitleSequenceFingerprint(BaseItem episode, TimeSpan duration, CancellationToken cancellationToken)
        {
            var chromaprintBinaryFilePath = Path.Combine(GetEncodingDirectory(), GetHashString($"title_sequence{episode.InternalId}") + ".bin");
            
            if (TitleFingerprintExists(episode)) return SplitByteData(chromaprintBinaryFilePath, episode); //<-- return the print is it already exists
            
            ExtractFingerprintBinaryData(episode, chromaprintBinaryFilePath, duration, cancellationToken, TimeSpan.Zero);

            if (!EnsureFfmpegEol(chromaprintBinaryFilePath)) Log.Warn("ffmpeg process key still available in title sequence process dictionary...OK");
            
            return SplitByteData(chromaprintBinaryFilePath, episode);
        }
        
        private string GetEncodingDirectory() => Path.Combine(ApplicationPaths.PluginsPath, "data", "intro_encoding"); 
        
        public bool CreditFingerprintExists(BaseItem item) => File.Exists(Path.Combine(GetEncodingDirectory(), GetHashString($"credit_sequence{item.InternalId}") + ".bin"));
        
        public bool TitleFingerprintExists(BaseItem item)  => File.Exists(Path.Combine(GetEncodingDirectory(), GetHashString($"title_sequence{item.InternalId}") + ".bin"));
        
        private bool EnsureFfmpegEol(string outputFilePath)
        {
            var process = Process.GetProcesses()
                .Where(p => p.Id == FfmpegProcessMonitor.FirstOrDefault(s => s.Key == outputFilePath).Value).ToList().FirstOrDefault();

            if (process is null)
            {
                return FfmpegProcessMonitor.TryRemove(outputFilePath, out _);
                //Log.Debug("Ffmpeg fingerprint instance exited successfully.");
            }

            Log.Debug("Ffmpeg fingerprint instance exiting...");
            try
            {
                process.Kill();
            }
            catch
            {
                
            }
            return FfmpegProcessMonitor.TryRemove(outputFilePath, out _);
        }

        public bool HasChromaprint()
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
            
            var procStartInfo = new ProcessStartInfo(ffmpegPath, "-version")
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
            if (!FileSystem.DirectoryExists($"{GetEncodingDirectory()}")) FileSystem.CreateDirectory($"{GetEncodingDirectory()}");
            
        }
    }
}
