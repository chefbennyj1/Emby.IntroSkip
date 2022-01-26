
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;

namespace IntroSkip.VideoBlackDetect
{
    public class VideoBlackDetectionManager : IServerEntryPoint
    {
        public static VideoBlackDetectionManager Instance { get; private set; }

        private IFfmpegManager FfmpegManager { get; }
        private ILibraryManager LibraryManager { get; }

        private readonly ConcurrentDictionary<long, int> FfmpegProcessMonitor = new ConcurrentDictionary<long, int>();

        public VideoBlackDetectionManager(IFfmpegManager ffmpeg, ILibraryManager libraryManager)
        {
            Instance = this;
            FfmpegManager = ffmpeg;
            LibraryManager = libraryManager;
        }
        
        public List<TimeSpan> Analyze(long internalId, CancellationToken cancellationToken)
        {
            var episode = LibraryManager.GetItemById(internalId);
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;

            if (!episode.RunTimeTicks.HasValue) //<-- this may not ever happen at this point.
            {
                return new List<TimeSpan>(); 
            }

            var runtime = TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            var input = episode.Path;

            var args = new[]
            {
                $"-sseof -{TimeSpan.FromMinutes(3)}",
                $"-i \"{input}\"",
                "-vf \"blackdetect=d=1:pix_th=0.0\"",
                "-an -f null -"
            };

            var procStartInfo = new ProcessStartInfo(ffmpegPath, string.Join(" ", args))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,

            };

            var blackDetections = new List<TimeSpan>();

            using (var process = new Process { StartInfo = procStartInfo })
            {
                process.Start();
                process.PriorityClass = ProcessPriorityClass.BelowNormal; 
                FfmpegProcessMonitor.TryAdd(internalId, process.Id);
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

                    if (!processOutput.Contains("blackdetect")) continue;

                    var substrings = processOutput.Split(':');

                    var blackFrameStart = TimeSpan.FromSeconds(Convert.ToDouble(substrings[1].Split(' ')[0]));

                    var blackFrameEnd = TimeSpan.FromSeconds(Convert.ToDouble(substrings[2].Split(' ')[0]));

                    var blackFrameDuration = TimeSpan.FromSeconds(Convert.ToDouble(substrings[3].Split(' ')[0]));

                    var blackScreenResult = runtime - TimeSpan.FromMinutes(3) + blackFrameStart;

                    blackDetections.Add(blackScreenResult);
                    

                }

            }

            EnsureFfmpegEol(internalId);
            return blackDetections;

        }

        private bool EnsureFfmpegEol(long internalId)
        {
            var process = Process.GetProcesses()
                .Where(p => p.Id == FfmpegProcessMonitor.FirstOrDefault(s => s.Key == internalId).Value).ToList().FirstOrDefault();

            if (process is null)
            {
                return FfmpegProcessMonitor.TryRemove(internalId, out _);
            }
            
            try
            {
                process.Kill();
            }
            catch
            {
                
            }
            
            return FfmpegProcessMonitor.TryRemove(internalId, out _);
        }
        public void Dispose()
        {

        }

        public void Run()
        {

        }
    }
}
