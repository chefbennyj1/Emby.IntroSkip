using System;
using System.Diagnostics;
using System.Threading;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;

namespace IntroSkip.Ffprobe
{
    
    public class VideoDurationManager : IServerEntryPoint
    {
        public static VideoDurationManager Instance {get; private set; }
        private IFfmpegManager FfmpegManager { get; set; }
        private ILogger Log { get; set; }
        public VideoDurationManager(IFfmpegManager ffmpegManager, ILogManager logManager)
        {
            Instance = this;
            FfmpegManager = ffmpegManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }

        public string CalculateRuntime(string input, CancellationToken cancellationToken)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffprobePath = ffmpegConfiguration.EncoderPath.Replace("ffmpeg", "ffprobe");

            var args = new[]
            {
                $"-i \"{input}\"",
                "-hide_banner",
                "-loglevel panic",
                "-show_format_entry duration",
                "2>&1"
            };

            var procStartInfo = new ProcessStartInfo(ffprobePath, string.Join(" ", args))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = new Process {StartInfo = procStartInfo})
            {
                process.Start();

                // ReSharper disable once NotAccessedVariable <-- Resharper is incorrect. It is being used
                string processOutput = null;
                
                while ((processOutput = process.StandardOutput.ReadLine()) != null)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            process.Kill(); //At least try and kill it.
                        }
                        catch { }
                    }

                    Log.Debug(processOutput);

                    if (!processOutput.Contains("duration")) continue;

                    var substrings = processOutput.Split('=');

                    var duration = TimeSpan.FromSeconds(Convert.ToDouble(substrings[1]));

                    Log.Debug($"{duration}");

                }
            }

            return "";
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Run()
        {
            throw new NotImplementedException();
        }
    }
}
