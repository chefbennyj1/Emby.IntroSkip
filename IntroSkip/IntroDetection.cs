/*
 * Plugin intro detection code translated from it's original author:
 * https://gist.githubusercontent.com/puzzledsam/c0731702a9eab244afacbcb777c9f5e9/raw/1fd81d1101eebc08d442acfd88742b5e5635f1ab/introDetection.py
 * Intro detection algorithm is derived from VictorBitca/matcher, which was originally written in Go. https://github.com/VictorBitca/matcher *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private IJsonSerializer JsonSerializer     { get; }
        private IFileSystem FileSystem             { get; }
        private IFfmpegManager FfmpegManager       { get; }
        private static ILogger Logger              { get; set; }
        public static IntroDetection Instance      { get; private set; }
        private IApplicationPaths ApplicationPaths { get; }
       
        public IntroDetection(IJsonSerializer json, IFileSystem file, ILogManager logMan, IFfmpegManager f, IApplicationPaths applicationPaths)
        {
            JsonSerializer   = json;
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            FfmpegManager    = f;
            Logger           = logMan.GetLogger(Plugin.Instance.Name);
            Instance         = this;
        }

        
        // Keep integer in specified range
        private static int clip(int val, int min, int max)
        {
            if (val < min)
            {
                return min;
            }
            else if (val > max)
            {
                return max;
            }
            else
            {
                return val;
            }
        }

        // Calculate Hamming distance between to integers (bit difference)
        private static uint getHammingDistance(uint n1, uint n2)
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
        private static double compareFingerprints(List<uint> f1, List<uint> f2)
        {
            double dist = 0.0;
            if (f1.Count != f2.Count)
            {
                return 0;
            }

            foreach (var i in Enumerable.Range(0, f1.Count))
            {
                var hammingDistance = getHammingDistance(f1[i], f2[i]);
                dist = dist + hammingDistance;
            }

            var score = 1 - dist / (f1.Count * 32);
            return score;
        }

        // Slide fingerprints to find best offset
        private static int getBestOffset(List<uint> f1, List<uint> f2)
        {
            var length     = f1.Count;
            var iterations = length + 1;
            var diff       = length / 2 - 1;
            var a          = length / 2;
            var b          = length - 1;
            var x          = 0;
            var y          = length / 2 - 1;

            var output = new List<double>();

            foreach (var i in Enumerable.Range(0, iterations))
            {
                var upper = Math.Abs(a - b);
                try
                {
                    output.Add(compareFingerprints(f1.GetRange(a, upper), f2.GetRange(x, upper)));
                   
                    a = clip(a - 1, 0, length - 1);
                    if (diff < 0)
                    {
                        b = clip(b - 1, 0, length - 1);
                        x = clip(x + 1, 0, length - 1);
                        y = clip(y, 0, length - 1);
                    }
                    else
                    {
                        b = clip(b, 0, length - 1);
                        x = clip(x, 0, length - 1);
                        y = clip(y + 1, 0, length - 1);
                    }

                    diff = diff - 1;
                }
                catch (Exception ex)
                {
                    Logger.Info("Get Best Offset Error: " + ex.Message);
                    throw new InvalidTitleSequenceDetectionException(ex.Message);
                }
            }

            var index = output.IndexOf(output.Max());

            return (iterations - 1) / 2 - index;
        }

        // Align the fingerprints according to the calculated offset
        private static Tuple<List<uint>, List<uint>> getAlignedFingerprints(int offset, List<uint> f1, List<uint> f2)
        {
            List<uint> offsetCorrectedF2 = new List<uint>();
            List<uint> offsetCorrectedF1 = new List<uint>();
            if (offset >= 0)
            {
                //offset = offset * -1;
                offsetCorrectedF1.AddRange(f1.GetRange(offset, f1.Count - offset));
                offsetCorrectedF2.AddRange(f2.GetRange(0, f2.Count - offset));

            }
            else
            {
                offset = offset * -1;
                offsetCorrectedF1.AddRange(f1.GetRange(0, f1.Count - Math.Abs(offset)));
                offsetCorrectedF2.AddRange(f2.GetRange(offset, f2.Count - Math.Abs(offset)));

            }

            return Tuple.Create(offsetCorrectedF1, offsetCorrectedF2);
        }

        // Find the intro region based on Hamming distances
        private static Tuple<int, int> findContiguousRegion(List<uint> arr, int upperLimit)
        {
            var start = -1;
            var end = -1;
            foreach (var i in Enumerable.Range(0, arr.Count()))
            {
                // Stop the execution after we've been far enough past the found intro region
                if (start != -1 && i - end >= 100) {
                    break;
                }
                if (arr[i] < upperLimit && nextOnesAreAlsoSmall(arr, i, upperLimit))
                {
                    if (start == -1)
                    {
                        start = i;
                    }

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
                if (average < upperLimit)
                {
                    return true;
                }

                return false;
            }

            return false;
        }
        
        private void ExtractPCMAudio(string input, string audioOut, TimeSpan duration)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath          = ffmpegConfiguration.EncoderPath;
            
            var procStartInfo = new ProcessStartInfo(ffmpegPath, $"-t {duration:c} -i \"{input}\" -ac 1 -acodec pcm_s16le -ar 16000 \"{audioOut}\"")
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
                    //Logger.Info(processOutput);
                }
            }
        }

        private AudioFingerprint FingerPrintAudio(string inputFileName)
        {
            // Using 600 second length to get a more accurate fingerprint, but it's not required
            var separator     = FileSystem.DirectorySeparatorChar;
            var encodingPath  = $"{IntroServerEntryPoint.Instance.EncodingDir}{separator}";
            var @params       = $"\"{inputFileName}\" -raw -length 600 -json";
            var fpcalc        = (OperatingSystem.IsWindows() ? "fpcalc.exe" : "fpcalc");

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
            
            return JsonSerializer.DeserializeFromString<AudioFingerprint>(json);
        }

       
        private static string EpisodeComparable { get; set; }
        private static string EpisodeToCompare  { get; set; }
       
        public List<EpisodeTitleSequence> SearchAudioFingerPrint(BaseItem episode1Input, BaseItem episode2Input)
        {
            var separator      = FileSystem.DirectorySeparatorChar;
            var fingerprintDir = $"{IntroServerEntryPoint.Instance.FingerPrintDir}{separator}";
            var encodingPath   = $"{IntroServerEntryPoint.Instance.EncodingDir}{separator}";

            Logger.Info("Starting episode intro detection process.");
            Logger.Info($" {episode1Input.Parent.Parent.Name} - Season: {episode1Input.Parent.IndexNumber} - Episode: {episode1Input.IndexNumber}");
            Logger.Info($" {episode2Input.Parent.Parent.Name} - Season: {episode2Input.Parent.IndexNumber} - Episode: {episode2Input.IndexNumber}");

            //Create the the current episode input key. Season.InternalId + episode.InternalId
            var episode1InputKey = $"{episode1Input.Parent.InternalId}{episode1Input.InternalId}";
            var episode2InputKey = $"{episode2Input.Parent.InternalId}{episode2Input.InternalId}";


            AudioFingerprint fingerPrintDataEpisode1 = null;
            AudioFingerprint fingerPrintDataEpisode2 = null;


            //Create the 10 minute audio encoding for the first episode, there is no fingerprint recorded
            if (!FileSystem.FileExists($"{fingerprintDir}{episode1InputKey}.json"))
            {
                
                if (EpisodeComparable is null || episode1InputKey != EpisodeComparable)
                {
                    EpisodeComparable = episode1InputKey;

                    //Check and see if we have encoded this episode before
                    if (!FileSystem.FileExists($"{encodingPath}{episode1InputKey}.wav"))
                    {
                        Logger.Info($"Beginning Audio Extraction for Comparable Episode: {episode1Input.Path}");
                        ExtractPCMAudio(episode1Input.Path, $"{encodingPath}{episode1InputKey}.wav", TimeSpan.FromMinutes(10));
                    }
                }

                fingerPrintDataEpisode1 = FingerPrintAudio($"{encodingPath}{episode1InputKey}.wav");

                //The finger print duration needs to match the .wav encoding length (600sec => 10min)
                //Threading can have unexpected results in fingerprinting audio with shorter durations.
                if (fingerPrintDataEpisode1.duration < 600.0) 
                {
                    fingerPrintDataEpisode1 = FingerPrintAudio($"{encodingPath}{episode1InputKey}.wav"); //Try to finger print this again it has the wrong duration.
                }

                SaveFingerPrintToFile($"{fingerprintDir}{episode1InputKey}.json", fingerPrintDataEpisode1);

            }
            else
            {
                fingerPrintDataEpisode1 = GetSavedFingerPrintFromFile($"{fingerprintDir}{episode1InputKey}.json"); //Got it already
            }

            
            //Create the 10 minute audio encoding for the second episode, there is no fingerprint recorded
            if (!FileSystem.FileExists($"{fingerprintDir}{episode2InputKey}.json"))
            {
                if (EpisodeToCompare is null || episode2InputKey != EpisodeToCompare)
                {
                    EpisodeToCompare = episode2InputKey;
                    

                    if (!FileSystem.FileExists($"{encodingPath}{episode2InputKey}.wav"))
                    {
                        Logger.Info($"Beginning Audio Extraction for Comparing Episode: {episode2Input.Path}");
                        ExtractPCMAudio(episode2Input.Path, $"{encodingPath}{episode2InputKey}.wav", TimeSpan.FromMinutes(10));

                    }
                }

                fingerPrintDataEpisode2 = FingerPrintAudio($"{encodingPath}{episode2InputKey}.wav");

                //Check the second files finger print duration
                if (fingerPrintDataEpisode2.duration < 600.0)
                {
                    fingerPrintDataEpisode2 = FingerPrintAudio($"{encodingPath}{episode2InputKey}.wav"); //Try to finger print this again it has the wrong duration.
                }

                SaveFingerPrintToFile($"{fingerprintDir}{episode2InputKey}.json", fingerPrintDataEpisode2);
            }
            else
            {
                fingerPrintDataEpisode2 = GetSavedFingerPrintFromFile($"{fingerprintDir}{episode2InputKey}.json"); //Got it already
            }

            
            var introDto =  AnalyzeAudio(fingerPrintDataEpisode1, fingerPrintDataEpisode2);

            introDto[0].InternalId  = episode1Input.InternalId;
            introDto[0].IndexNumber = episode1Input.IndexNumber;

            introDto[1].InternalId  = episode2Input.InternalId;
            introDto[1].IndexNumber = episode2Input.IndexNumber;
           
            Logger.Info($"\n\n{episode1Input.Parent.Parent.Name} - S: {episode1Input.Parent.IndexNumber} - E: {episode1Input.IndexNumber} \nStarts: {introDto[0].IntroStart} \nEnd: {introDto[0].IntroEnd}\n");
            Logger.Info($"\n{episode2Input.Parent.Parent.Name} - S: {episode2Input.Parent.IndexNumber} - E: {episode2Input.IndexNumber} \nStarts: {introDto[1].IntroStart} \nEnd: {introDto[1].IntroEnd}\n\n");
            
            return introDto;
        }

        private AudioFingerprint GetSavedFingerPrintFromFile(string filePath)
        {
            using (var sr = new StreamReader(filePath))
            {
                return JsonSerializer.DeserializeFromString<AudioFingerprint>(sr.ReadToEnd());
            }
        }

        private void SaveFingerPrintToFile(string filePath, AudioFingerprint json)
        {
            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(JsonSerializer.SerializeToString(json));
                sw.Flush();
            }
        }

        
        private List<EpisodeTitleSequence> AnalyzeAudio(AudioFingerprint fingerPrintDataEpisode1, AudioFingerprint fingerPrintDataEpisode2)
        {
            
            Logger.Info("Analyzing Audio...");
            

            var fingerprint1 = fingerPrintDataEpisode1.fingerprint;
            var fingerprint2 = fingerPrintDataEpisode2.fingerprint;
            
            
            Logger.Info("Analyzing Finger Prints..");

            // We'll cut off a bit of the end if the fingerprints have an odd numbered length
            if (fingerprint1.Count % 2 != 0)
            {
                fingerprint1 = fingerprint1.GetRange(0, fingerprint1.Count() - 1);  
                fingerprint2 = fingerprint2.GetRange(0, fingerprint2.Count() - 1);  
            }

            Logger.Info("Analyzing Offsets..");
            int offset = getBestOffset(fingerprint1, fingerprint2);

            Logger.Info($"The calculated fingerprint offset is {offset}");

            var _tup_1 = getAlignedFingerprints(offset, fingerprint1, fingerprint2);
            var f1     = _tup_1.Item1;
            var f2     = _tup_1.Item2;


            //Logger.Info("Calculating Hamming Distances.");
            var hammingDistances = Enumerable.Range(0, (f1.Count < f2.Count ? f1.Count : f2.Count)).Select(i => getHammingDistance(f1[i], f2[i])).ToList();
            Logger.Info("Calculate Hamming Distances Done.");
            
            var _tup_2 = findContiguousRegion(hammingDistances, 8);
            var start  = _tup_2.Item1;
            var end    = _tup_2.Item2;

            double secondsPerSample = 600.0 / fingerprint1.Count;

            var offsetInSeconds   = offset * secondsPerSample;
            var commonRegionStart = start * secondsPerSample;
            var commonRegionEnd   = (end * secondsPerSample);

            var firstFileRegionStart  = 0.0;
            var firstFileRegionEnd    = 0.0;
            var secondFileRegionStart = 0.0;
            var secondFileRegionEnd   = 0.0;

            if (offset >= 0)
            {
                firstFileRegionStart  = commonRegionStart + offsetInSeconds;
                firstFileRegionEnd    = commonRegionEnd + offsetInSeconds;
                secondFileRegionStart = commonRegionStart;
                secondFileRegionEnd   = commonRegionEnd;
            }
            else
            {
                firstFileRegionStart  = commonRegionStart;
                firstFileRegionEnd    = commonRegionEnd;
                secondFileRegionStart = commonRegionStart - offsetInSeconds;
                secondFileRegionEnd   = commonRegionEnd - offsetInSeconds;
            }

            // Check for impossible situation, or if the common region is deemed too short to be considered an intro
            if (start < 0 || end < 0)
            {
                firstFileRegionStart  = 0.0;
                firstFileRegionEnd    = 0.0;
                secondFileRegionStart = 0.0;
                secondFileRegionEnd   = 0.0;
                throw new InvalidTitleSequenceDetectionException("Episode detection failed to find a reasonable intro start and end time.");
            }
            if (commonRegionEnd - commonRegionStart < (Plugin.Instance.Configuration.TitleSequenceThreshold ?? 8.5))
            {
                // -1 means intro does not exists
                firstFileRegionStart  = -1.0;
                firstFileRegionEnd    = -1.0;
                secondFileRegionStart = -1.0;
                secondFileRegionEnd   = -1.0;
                throw new InvalidTitleSequenceDetectionException("Episode common region is deemed too short to be considered an intro.");
                
            } 
            else if (start == 0 && end == 0)
            {
                throw new InvalidTitleSequenceDetectionException("Episode common region are both 00:00:00.");
            }
            
            
            Logger.Info("Audio Analysis Complete.");
            
            
            
            return new List<EpisodeTitleSequence>()
            {
                new EpisodeTitleSequence() //[0]
                {
                    HasIntro   = true,
                    IntroStart = TimeSpan.FromSeconds(Math.Round(firstFileRegionStart)),
                    IntroEnd   = TimeSpan.FromSeconds(Math.Round(firstFileRegionEnd))
                },
                new EpisodeTitleSequence() //[1]
                {
                    HasIntro   = true,
                    IntroStart = TimeSpan.FromSeconds(Math.Round(secondFileRegionStart)),
                    IntroEnd   = TimeSpan.FromSeconds(Math.Round(secondFileRegionEnd))
                }
            };

            
        }

        public void Dispose()
        {
            
        }

        public void Run()
        {
           
        }
    }
}
