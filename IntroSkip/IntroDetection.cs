/*
 * Plugin intro detection code translated from it's original author:
 * https://gist.githubusercontent.com/puzzledsam/c0731702a9eab244afacbcb777c9f5e9/raw/1fd81d1101eebc08d442acfd88742b5e5635f1ab/introDetection.py
 * Intro detection algorithm is derived from VictorBitca/matcher, which was originally written in Go. https://github.com/VictorBitca/matcher *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

// ReSharper disable ComplexConditionExpression
// ReSharper disable once TooManyDependencies

namespace IntroSkip
{
    public class IntroDetection : TitleSequenceDto, IServerEntryPoint
    {
        private string _audio1SavePath = "";
        private string _audio2SavePath = "";

        public IntroDetection(IJsonSerializer json, IFileSystem file, ILogManager logMan, IFfmpegManager f,
            IApplicationPaths applicationPaths)
        {
            JsonSerializer = json;
            FileSystem = file;
            ApplicationPaths = applicationPaths;
            FfmpegManager = f;
            Logger = logMan.GetLogger(Plugin.Instance.Name);
            Instance = this;
        }

        private IJsonSerializer JsonSerializer { get; }
        private IFileSystem FileSystem { get; }
        private IFfmpegManager FfmpegManager { get; }
        private static ILogger Logger { get; set; }
        public static IntroDetection Instance { get; private set; }
        private IApplicationPaths ApplicationPaths { get; }

        private static string EpisodeComparable { get; set; }
        private static string EpisodeToCompare { get; set; }

        public void Dispose()
        {
        }

        public void Run()
        {
            if (!FileSystem.DirectoryExists(ApplicationPaths.PluginConfigurationsPath +
                                            FileSystem.DirectorySeparatorChar + "IntroEncoding"))
                FileSystem.CreateDirectory(ApplicationPaths.PluginConfigurationsPath +
                                           FileSystem.DirectorySeparatorChar + "IntroEncoding");
        }


        // Keep integer in specified range
        private static int Clip(int val, int min, int max)
        {
            if (val < min)
                return min;
            if (val > max)
                return max;
            return val;
        }

        // Calculate Hamming distance between to integers (bit difference)
        private static uint GetHammingDistance(uint n1, uint n2)
        {
            var x = n1 ^ n2;
            uint setBits = 0;
            while (x > 0)
            {
                setBits += x & 1;
                x >>= 1;
            }

            return setBits;
        }

        // Calculate the similarity of two fingerprints
        private static double CompareFingerprints(List<uint> f1, List<uint> f2)
        {
            var dist = 0.0;
            if (f1.Count != f2.Count) return 0;

            foreach (var i in Enumerable.Range(0, f1.Count))
            {
                var hammingDistance = GetHammingDistance(f1[i], f2[i]);
                dist = dist + hammingDistance;
            }

            var score = 1 - dist / (f1.Count * 32);
            return score;
        }

        // Slide fingerprints to find best offset
        private static int GetBestOffset(List<uint> f1, List<uint> f2)
        {
            var length = f1.Count;
            var iterations = length + 1;
            var diff = length / 2 - 1;
            var a = length / 2;
            var b = length - 1;
            var x = 0;
            var y = length / 2 - 1;

            var output = new List<double>();

            // ReSharper disable once UnusedVariable
            foreach (var i in Enumerable.Range(0, iterations))
            {
                var upper = Math.Abs(a - b);
                try
                {
                    output.Add(CompareFingerprints(f1.GetRange(a, upper), f2.GetRange(x, upper)));

                    a = Clip(a - 1, 0, length - 1);
                    if (diff < 0)
                    {
                        b = Clip(b - 1, 0, length - 1);
                        x = Clip(x + 1, 0, length - 1);
                        y = Clip(y, 0, length - 1);
                    }
                    else
                    {
                        b = Clip(b, 0, length - 1);
                        x = Clip(x, 0, length - 1);
                        y = Clip(y + 1, 0, length - 1);
                    }

                    diff = diff - 1;
                }
                catch (Exception ex)
                {
                    Logger.Info("Get Best Offset Error: " + ex.Message);
                    throw new InvalidIntroDetectionException(ex.Message);
                }
            }

            var index = output.IndexOf(output.Max());

            return (iterations - 1) / 2 - index;
        }

        // Align the fingerprints according to the calculated offset
        private static Tuple<List<uint>, List<uint>> GetAlignedFingerprints(int offset, List<uint> f1, List<uint> f2)
        {
            var offsetCorrectedF2 = new List<uint>();
            var offsetCorrectedF1 = new List<uint>();
            if (offset >= 0)
            {
                //offset = offset * -1;
                offsetCorrectedF1.AddRange(f1.GetRange(offset, f1.Count - offset));
                offsetCorrectedF2.AddRange(f2.GetRange(0, f2.Count - offset));
            }
            else
            {
                offset = offset * -1; //ToDo: Possible overflow.
                offsetCorrectedF1.AddRange(f1.GetRange(0, f1.Count - Math.Abs(offset)));
                offsetCorrectedF2.AddRange(f2.GetRange(offset, f2.Count - Math.Abs(offset)));
            }

            return Tuple.Create(offsetCorrectedF1, offsetCorrectedF2);
        }

        // Find the intro region based on Hamming distances
        private static Tuple<int, int> FindContiguousRegion(List<uint> arr, int upperLimit)
        {
            var start = -1;
            var end = -1;
            foreach (var i in Enumerable.Range(0, arr.Count()))
            {
                // Stop the execution after we've been far enough past the found intro region
                if (start != -1 && i - end >= 100) break;
                if (arr[i] < upperLimit && nextOnesAreAlsoSmall(arr, i, upperLimit))
                {
                    if (start == -1) start = i;

                    end = i;
                }
            }

            return Tuple.Create(start, end);
        }

        // Look at next elements in the array and determine if they also fall below the upper limit
        private static bool nextOnesAreAlsoSmall(List<uint> arr, int index, int upperLimit)
        {
            if (index + 3 < arr.Count())
            {
                var v1 = arr[index + 1];
                var v2 = arr[index + 2];
                var v3 = arr[index + 3];
                var average = (v1 + v2 + v3) / 3;
                if (average < upperLimit) return true;

                return false;
            }

            return false;
        }

        // ReSharper disable once InconsistentNaming
        private void ExtractPCMAudio(string input, string audioOut, TimeSpan duration)
        {
            var ffmpegPath = FfmpegManager.FfmpegConfiguration.EncoderPath;

            var procStartInfo = new ProcessStartInfo(ffmpegPath,
                $"-t {duration:c} -i \"{input}\" -ac 1 -acodec pcm_s16le -ar 16000 \"{audioOut}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process {StartInfo = procStartInfo})
            {
                process.Start();

                while (process.StandardError.ReadLine() != null)
                {
                    //Logger.Info(processOutput);
                }
            }
        }

        private string FingerPrintAudio(string inputFileName)
        {
            // Using 600 second length to get a more accurate fingerprint, but it's not required
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            var encodingPath =
                $"{configDir}{FileSystem.DirectorySeparatorChar}IntroEncoding{FileSystem.DirectorySeparatorChar}";
            var @params = $"\"{inputFileName}\" -raw -length 600 -json";
            var fpcalc = OperatingSystem.IsWindows() ? "fpcalc.exe" : "fpcalc";

            var procStartInfo = new ProcessStartInfo($"{encodingPath}{fpcalc}", @params)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process {StartInfo = procStartInfo};

            process.Start();


            string processOutput;
            var json = "";

            while ((processOutput = process.StandardOutput.ReadLine()) != null)
            {
                Logger.Info(processOutput);
                json += processOutput;
            }

            return json;
        }

        public List<EpisodeTitleSequence> SearchAudioFingerPrint(BaseItem episode1Input, BaseItem episode2Input)
        {
            var encodingPath = ApplicationPaths.PluginConfigurationsPath + FileSystem.DirectorySeparatorChar +
                               "IntroEncoding" + FileSystem.DirectorySeparatorChar;
            Logger.Info("Starting episode intro detection process.");
            Logger.Info(
                $" {episode1Input.Parent.Parent.Name} - Season: {episode1Input.Parent.IndexNumber} - Episode Comparable: {episode1Input.IndexNumber}");
            Logger.Info(
                $" {episode2Input.Parent.Parent.Name} - Season: {episode2Input.Parent.IndexNumber} - Episode To Compare: {episode2Input.IndexNumber}");

            //Create the the current episode input key. Season.InternalId + episode.InternalId
            var episode1InputKey = $"{episode1Input.Parent.InternalId}{episode1Input.InternalId}";
            var episode2InputKey = $"{episode2Input.Parent.InternalId}{episode2Input.InternalId}";


            if (!FileSystem.FileExists($"{encodingPath}seasonTheme.wav"))
            {
                if (EpisodeComparable is null || episode1InputKey != EpisodeComparable)
                {
                    EpisodeComparable = episode1InputKey;

                    _audio1SavePath = $"{encodingPath}{EpisodeComparable}.wav";

                    //Check and see if we have encoded this episode before
                    if (!FileSystem.FileExists(_audio1SavePath))
                    {
                        Logger.Info($"Beginning Audio Extraction for Comparable Episode: {episode1Input.Path}");
                        ExtractPCMAudio(episode1Input.Path, _audio1SavePath, TimeSpan.FromMinutes(10));
                    }
                }
            }
            else
            {
                _audio1SavePath = $"{encodingPath}seasonTheme.wav";
            }


            if (EpisodeToCompare is null || episode2InputKey != EpisodeToCompare)
            {
                EpisodeToCompare = episode2InputKey;

                _audio2SavePath = $"{encodingPath}{EpisodeToCompare}.wav";

                if (!FileSystem.FileExists(_audio2SavePath))
                {
                    Logger.Info($"Beginning Audio Extraction for Comparing Episode: {episode2Input.Path}");
                    ExtractPCMAudio(episode2Input.Path, _audio2SavePath, TimeSpan.FromMinutes(10));
                }
            }


            Logger.Info("Audio Extraction Ready.");

            var introDto = AnalyzeAudio();

            introDto[0].InternalId = episode1Input.InternalId;
            introDto[0].IndexNumber = episode1Input.IndexNumber;

            introDto[1].InternalId = episode2Input.InternalId;
            introDto[1].IndexNumber = episode2Input.IndexNumber;

            Logger.Info(
                $"\n{episode1Input.Parent.Parent.Name} - S: {episode1Input.Parent.IndexNumber} - E: {episode1Input.IndexNumber} \nStarts: {introDto[0].IntroStart} \nEnd: {introDto[0].IntroEnd}\n\n");
            Logger.Info(
                $"\n{episode2Input.Parent.Parent.Name} - S: {episode2Input.Parent.IndexNumber} - E: {episode2Input.IndexNumber} \nStarts: {introDto[1].IntroStart} \nEnd: {introDto[1].IntroEnd}\n\n");

            return introDto;
        }

        private List<EpisodeTitleSequence> AnalyzeAudio()
        {
            Logger.Info("Analyzing Audio...");

            Logger.Info(_audio1SavePath);
            Logger.Info(_audio2SavePath);

            var audio1Json = FingerPrintAudio(_audio1SavePath);
            var fingerPrintDataEpisode1 = JsonSerializer.DeserializeFromString<IntroAudioFingerprint>(audio1Json);
            if (fingerPrintDataEpisode1 is null)
            {
                Logger.Info("Trying new episode");
                throw new InvalidIntroDetectionException(
                    $"Episode detection failed to find a fingerprint. {_audio1SavePath}");
            }

            Logger.Info($"Fingerprint 1 Duration {fingerPrintDataEpisode1.Duration}");

            var audio2Json = FingerPrintAudio(_audio2SavePath);
            var fingerPrintDataEpisode2 = JsonSerializer.DeserializeFromString<IntroAudioFingerprint>(audio2Json);
            if (fingerPrintDataEpisode2 is null)
            {
                Logger.Info("Trying new episode");
                throw new InvalidIntroDetectionException(
                    $"Episode detection failed to find a fingerprint. {_audio2SavePath}");
            }

            Logger.Info($"Fingerprint 2 Duration {fingerPrintDataEpisode2.Duration}");

            var fingerprint1 = fingerPrintDataEpisode1.Fingerprint;
            var fingerprint2 = fingerPrintDataEpisode2.Fingerprint;

            Logger.Info("Analyzing Finger Prints..");

            // We'll cut off a bit of the end if the fingerprints have an odd numbered length
            if (fingerprint1.Count % 2 != 0)
            {
                fingerprint1 = fingerprint1.GetRange(0, fingerprint1.Count() - 1);
                fingerprint2 = fingerprint2.GetRange(0, fingerprint2.Count() - 1);
            }

            Logger.Info("Analyzing Offsets..");
            var offset = GetBestOffset(fingerprint1, fingerprint2);

            Logger.Info($"The calculated fingerprint offset is {offset}");

            var _tup_1 = GetAlignedFingerprints(offset, fingerprint1, fingerprint2);
            var f1 = _tup_1.Item1;
            var f2 = _tup_1.Item2;


            //Logger.Info("Calculating Hamming Distances.");
            var hammingDistances = Enumerable.Range(0, f1.Count < f2.Count ? f1.Count : f2.Count)
                .Select(i => GetHammingDistance(f1[i], f2[i])).ToList();
            Logger.Info("Calculate Hamming Distances Done.");

            var _tup_2 = FindContiguousRegion(hammingDistances, 8);
            var start = _tup_2.Item1;
            var end = _tup_2.Item2;

            var secondsPerSample = 600.0 / fingerprint1.Count;

            var offsetInSeconds = offset * secondsPerSample;
            var commonRegionStart = start * secondsPerSample;
            var commonRegionEnd = end * secondsPerSample;

            double firstFileRegionStart;
            double firstFileRegionEnd;
            double secondFileRegionStart;
            double secondFileRegionEnd;

            if (offset >= 0)
            {
                firstFileRegionStart = commonRegionStart + offsetInSeconds;
                firstFileRegionEnd = commonRegionEnd + offsetInSeconds;
                secondFileRegionStart = commonRegionStart;
                secondFileRegionEnd = commonRegionEnd;
            }
            else
            {
                firstFileRegionStart = commonRegionStart;
                firstFileRegionEnd = commonRegionEnd;
                secondFileRegionStart = commonRegionStart - offsetInSeconds;
                secondFileRegionEnd = commonRegionEnd - offsetInSeconds;
            }

            // Check for impossible situation, or if the common region is deemed too short to be considered an intro
            if (start < 0 || end < 0)
            {
                throw new InvalidIntroDetectionException(
                    "Episode detection failed to find a reasonable intro start and end time.");
            }

            if (commonRegionEnd - commonRegionStart < 10)
            {
                throw new InvalidIntroDetectionException(
                    "Episode common region is deemed too short to be considered an intro.");
            }

            if (start == 0 && end == 0)
                throw new InvalidIntroDetectionException("Episode common region are both 00:00:00.");


            Logger.Info("Audio Analysis Complete.");


            return new List<EpisodeTitleSequence>
            {
                new EpisodeTitleSequence
                {
                    HasIntro = true,
                    IntroStart = TimeSpan.FromSeconds(Math.Round(firstFileRegionStart)),
                    IntroEnd = TimeSpan.FromSeconds(Math.Round(firstFileRegionEnd))
                },
                new EpisodeTitleSequence
                {
                    HasIntro = true,
                    IntroStart = TimeSpan.FromSeconds(Math.Round(secondFileRegionStart)),
                    IntroEnd = TimeSpan.FromSeconds(Math.Round(secondFileRegionEnd))
                }
            };
        }
    }
}