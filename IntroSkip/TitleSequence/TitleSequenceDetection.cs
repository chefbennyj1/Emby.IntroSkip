/*
 * Plugin intro detection code translated from it's original author:
 * https://gist.githubusercontent.com/puzzledsam/c0731702a9eab244afacbcb777c9f5e9/raw/1fd81d1101eebc08d442acfd88742b5e5635f1ab/introDetection.py
 * Intro detection algorithm is derived from VictorBitca/matcher, which was originally written in Go. https://github.com/VictorBitca/matcher *
 */

using IntroSkip.AudioFingerprinting;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable ComplexConditionExpression

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceDetection : TitleSequenceResult, IServerEntryPoint

    {
        public static TitleSequenceDetection Instance { get; private set; }
        //private static ILogger Log { get; set; }

        public TitleSequenceDetection(ILogManager logMan)
        {
            //Log = logMan.GetLogger(Plugin.Instance.Name);
            Instance = this;
        }

        // Keep integer in specified range
        private static int Clip(int val, int min, int max)
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

        private static uint GetHammingDistance2(uint x, uint y)
        {
            var i = x ^ y;
            i -= ((i >> 1) & 0x55555555);
            i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
            i = (i + (i >> 4)) & 0x0f0f0f0f;
            i += (i >> 8);
            i += (i >> 16);
            return i & 0x3f;
        }

        // Calculate the similarity of two fingerprints
        private static double CompareFingerprints(List<uint> f1, List<uint> f2)
        {
            double dist = 0.0;
            if (f1.Count != f2.Count)
            {
                return 0;
            }

            foreach (var i in Enumerable.Range(0, f1.Count))
            {
                var hammingDistance = GetHammingDistance2(f1[i], f2[i]);
                dist += hammingDistance;
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
                    //Logger.Info("Get Best Offset Error: " + ex.Message);
                    throw new TitleSequenceInvalidDetectionException(ex.Message);
                }
            }

            var index = output.IndexOf(output.Max());

            return (iterations - 1) / 2 - index;
        }

        // Align the fingerprints according to the calculated offset
        private static Tuple<List<uint>, List<uint>> GetAlignedFingerprints(int offset, List<uint> fingerprint1, List<uint> fingerprint2)
        {
            List<uint> offsetCorrectedF2 = new List<uint>();
            List<uint> offsetCorrectedF1 = new List<uint>();
            if (offset >= 0)
            {
                //offset = offset * -1;
                offsetCorrectedF1.AddRange(fingerprint1.GetRange(offset, fingerprint1.Count - offset));
                offsetCorrectedF2.AddRange(fingerprint2.GetRange(0, fingerprint2.Count - offset));

            }
            else
            {
                offset = offset * -1;
                offsetCorrectedF1.AddRange(fingerprint1.GetRange(0, fingerprint1.Count - Math.Abs(offset)));
                offsetCorrectedF2.AddRange(fingerprint2.GetRange(offset, fingerprint2.Count - Math.Abs(offset)));

            }

            return Tuple.Create(offsetCorrectedF1, offsetCorrectedF2);
        }

        // Find the intro region based on Hamming distances
        private static Tuple<int, int> FindContiguousRegion(List<uint> hammingDistances, int upperLimit)
        {
            var start = -1;
            var end = -1;
            foreach (var i in Enumerable.Range(0, hammingDistances.Count()))
            {
                // Stop the execution after we've been far enough past the found intro region
                if (start != -1 && i - end >= 100)
                {
                    break;
                }
                if (hammingDistances[i] < upperLimit && nextOnesAreAlsoSmall(hammingDistances, i, upperLimit))
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
        private static bool nextOnesAreAlsoSmall(List<uint> hammingDistances, int index, int upperLimit)
        {
            if (index + 3 < hammingDistances.Count())
            {
                var v1 = hammingDistances[index + 1];
                var v2 = hammingDistances[index + 2];
                var v3 = hammingDistances[index + 3];
                var average = (v1 + v2 + v3) / 3;
                return average < upperLimit;
            }

            return false;
        }

        public List<TitleSequenceResult> DetectTitleSequence(BaseItem episode1Input, BaseItem episode2Input, QueryResult<TitleSequenceResult> result)
        {

            var episode1InputKey = result.Items.FirstOrDefault(r => r.InternalId == episode1Input.InternalId);

            var episode2InputKey = result.Items.FirstOrDefault(r => r.InternalId == episode2Input.InternalId);

            if (episode1InputKey is null)
            {

                throw new AudioFingerprintMissingException($" fingerprint data doesn't currently exist");
            }

            if (episode2InputKey is null)
            {

                throw new AudioFingerprintMissingException($" fingerprint data doesn't currently exist");
            }

            if (episode1InputKey.Duration != episode2InputKey.Duration)
            {

                throw new AudioFingerprintDurationMatchException("Fingerprint encoding durations don't match");
            }


            var introDto = CompareFingerprint(episode1InputKey, episode2InputKey);


            return introDto;

        }


        private List<TitleSequenceResult> CompareFingerprint(TitleSequenceResult episode1, TitleSequenceResult episode2)
        {

            var duration = episode1.Duration * 60; //Both episodes should have the same encoding duration


            var fingerprint1 = episode1.Fingerprint;
            var fingerprint2 = episode2.Fingerprint;


            // We'll cut off a bit of the end if the fingerprints have an odd numbered length
            if (fingerprint1.Count % 2 != 0)
            {
                fingerprint1 = fingerprint1.GetRange(0, fingerprint1.Count() - 1);
                fingerprint2 = fingerprint2.GetRange(0, fingerprint2.Count() - 1);
            }

            int offset = GetBestOffset(fingerprint1, fingerprint2);


            var tup1 = GetAlignedFingerprints(offset, fingerprint1, fingerprint2);
            var f1 = tup1.Item1;
            var f2 = tup1.Item2;

            // ReSharper disable once TooManyChainedReferences
            List<uint> hammingDistances = Enumerable.Range(0, (f1.Count < f2.Count ? f1.Count : f2.Count)).Select(i => GetHammingDistance2(f1[i], f2[i])).ToList();
           

            //Added for Sam to test upper threshold changes
            var config = Plugin.Instance.Configuration;
            var tup2 = FindContiguousRegion(hammingDistances, config.HammingDistanceThreshold); //TODO: Right here we say 8 as an 'upperLimit', what happens if we expect something bigger like 10??

            
            var start  = tup2.Item1;
            var end    = tup2.Item2;


            double secondsPerSample = Convert.ToDouble(duration) / fingerprint1.Count;

            var offsetInSeconds = offset * secondsPerSample;
            var commonRegionStart = start * secondsPerSample;
            var commonRegionEnd = (end * secondsPerSample);

            var firstFileRegionStart = 0.0;
            var firstFileRegionEnd = 0.0;
            var secondFileRegionStart = 0.0;
            var secondFileRegionEnd = 0.0;

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
                
                throw new TitleSequenceInvalidDetectionException("Episode detection failed to find a reasonable intro start and end time.");
            }
            if (commonRegionEnd - commonRegionStart < (Plugin.Instance.Configuration.TitleSequenceLengthThreshold))
            {
                
                throw new TitleSequenceInvalidDetectionException("Episode common region is deemed too short to be considered an intro.");

            }
            else if (start == 0 && end == 0)
            {
                throw new TitleSequenceInvalidDetectionException("Episode common region are both 00:00:00.");
            }


            episode1.HasSequence = true;
            episode1.TitleSequenceStart = TimeSpan.FromSeconds(Math.Floor(firstFileRegionStart));
            episode1.TitleSequenceEnd = TimeSpan.FromSeconds(Math.Ceiling(firstFileRegionEnd));


            episode2.HasSequence = true;
            episode2.TitleSequenceStart = TimeSpan.FromSeconds(Math.Floor(secondFileRegionStart));
            episode2.TitleSequenceEnd = TimeSpan.FromSeconds(Math.Ceiling(secondFileRegionEnd));

            return new List<TitleSequenceResult>()
            {
                episode1,
                episode2
            };


        }

        public void Dispose()
        {

        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {

        }
    }
}