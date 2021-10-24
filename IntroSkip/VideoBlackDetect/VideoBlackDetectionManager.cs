
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using IntroSkip.Sequence;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;

namespace IntroSkip.VideoBlackDetect
{
    public class VideoBlackDetectionManager : IServerEntryPoint
    {
        public static VideoBlackDetectionManager Instance { get; private set; }
        private IFileSystem FileSystem                 { get; }
        private IApplicationPaths ApplicationPaths     { get; }
        private char Separator                         { get; }
        private IFfmpegManager FfmpegManager           { get; }
        private ILogger Log                            { get; }
        private ILibraryManager LibraryManager         { get; }
        public VideoBlackDetectionManager(IFileSystem file, IFfmpegManager ffmpeg, ILogManager logManager, IApplicationPaths applicationPaths, ILibraryManager libraryManager)
        {
            Instance         = this;
            FileSystem       = file;
            FfmpegManager    = ffmpeg;
            ApplicationPaths = applicationPaths;
            LibraryManager   = libraryManager;
            Separator        = FileSystem.DirectorySeparatorChar;
            Log              = logManager.GetLogger(Plugin.Instance.Name);
        }

        public List<TimeSpan> Analyze(long internalId, CancellationToken cancellationToken)
        {
            var episode             = LibraryManager.GetItemById(internalId);
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath          = ffmpegConfiguration.EncoderPath;
            var config              = Plugin.Instance.Configuration;
            //TODO: If runtime is null handle it. Library scan needs to run.
            var runtime             = TimeSpan.FromTicks(episode.RunTimeTicks.Value);
            var start               = runtime - TimeSpan.FromMinutes(3);
            var input               = episode.Path;
            
            var args = new[]
            {
                $"-accurate_seek -ss {start}",
                $"-i \"{input}\"",
                $"-vf \"blackdetect=d={config.BlackDetectionSecondIntervals}:pix_th=0.0\"",
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
                    
                    if (blackFrameDuration > TimeSpan.FromSeconds(1.4))
                    {
                        var blackScreenResult  = runtime - TimeSpan.FromMinutes(3) + blackFrameStart;

                        blackDetections.Add(blackScreenResult);
                    }
                    

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
