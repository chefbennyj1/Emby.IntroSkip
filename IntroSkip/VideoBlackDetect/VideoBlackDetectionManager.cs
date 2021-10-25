
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
 using System.Threading;
 using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;

 namespace IntroSkip.VideoBlackDetect
{
    public class VideoBlackDetectionManager : IServerEntryPoint
    {
        public static VideoBlackDetectionManager Instance { get; private set; }
       
        private IFfmpegManager FfmpegManager           { get; }
        private ILibraryManager LibraryManager         { get; }
        public VideoBlackDetectionManager(IFfmpegManager ffmpeg, ILibraryManager libraryManager)
        {
            Instance         = this;
            FfmpegManager    = ffmpeg;
            LibraryManager   = libraryManager;
        }

        public List<TimeSpan> Analyze(long internalId, CancellationToken cancellationToken)
        {
            var episode             = LibraryManager.GetItemById(internalId);
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath          = ffmpegConfiguration.EncoderPath;
            //TODO: If runtime is null handle it. Library scan needs to run.
            var runtime             = TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            var input               = episode.Path;
            
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

                    var blackFrameStart    = TimeSpan.FromSeconds(Convert.ToDouble(substrings[1].Split(' ')[0]));

                    var blackFrameEnd      = TimeSpan.FromSeconds(Convert.ToDouble(substrings[2].Split(' ')[0]));

                    var blackFrameDuration = TimeSpan.FromSeconds(Convert.ToDouble(substrings[3].Split(' ')[0]));

                    var blackScreenResult = runtime - TimeSpan.FromMinutes(3) + blackFrameStart;

                    blackDetections.Add(blackScreenResult);
                   


                }

            }
            
            return blackDetections;

        }

        public void Dispose()
        {
            
        }

        public void Run()
        {
            
        }
    }
}
