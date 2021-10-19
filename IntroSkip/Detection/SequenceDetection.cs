/*
 * Plugin intro detection code translated from it's original author:
 * https://gist.githubusercontent.com/puzzledsam/c0731702a9eab244afacbcb777c9f5e9/raw/1fd81d1101eebc08d442acfd88742b5e5635f1ab/introDetection.py
 * Intro detection algorithm is derived from VictorBitca/matcher, which was originally written in Go. https://github.com/VictorBitca/matcher *
 */

using IntroSkip.AudioFingerprinting;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable ComplexConditionExpression

namespace IntroSkip.Detection
{
    public class SequenceDetection : SequenceResult, IServerEntryPoint

    {
        public static SequenceDetection Instance { get; private set; }

        public SequenceDetection(ILogManager logMan)
        {
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
        //private static double GetHammingDistance(uint n1, uint n2)
        //{
        //    var x = n1 ^ n2;
        //    uint setBits = 0;
        //    while (x > 0)
        //    {
        //        setBits += x & 1;
        //        x >>= 1;
        //    }

        //    return setBits;
        //}

        private static double GetHammingDistance(uint x, uint y)
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
                var hammingDistance = GetHammingDistance(f1[i], f2[i]);
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

                    diff -= 1;
                }
                catch (Exception ex)
                {
                    //Logger.Info("Get Best Offset Error: " + ex.Message);
                    throw new SequenceInvalidDetectionException(ex.Message);
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
        private static Tuple<int, int> FindContiguousRegion(List<double> hammingDistances, int upperLimit)
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
        private static bool nextOnesAreAlsoSmall(IReadOnlyList<double> hammingDistances, int index, int upperLimit)
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

        public Tuple<SequenceResult, SequenceResult> DetectTitleSequence(SequenceResult episode1InputKey, SequenceResult episode2InputKey)
        {            

            if (episode1InputKey.TitleSequenceFingerprint is null)
            {
                throw new AudioFingerprintMissingException($" fingerprint data doesn't currently exist");
            }

            if (episode2InputKey.TitleSequenceFingerprint is null)
            {
                throw new AudioFingerprintMissingException($" fingerprint data doesn't currently exist");
            }

            if (episode1InputKey.Duration != episode2InputKey.Duration)
            {
                throw new AudioFingerprintDurationMatchException("Fingerprint encoding durations don't match");
            }

            //Find intro
            var introFingerprint1     = episode1InputKey.TitleSequenceFingerprint;
            var introFingerprint2     = episode2InputKey.TitleSequenceFingerprint;
            var introEncodingDuration = episode1InputKey.Duration * 60; //Both episodes should have the same encoding duration in seconds

            var intros = CompareFingerprint(introFingerprint1, introFingerprint2, introEncodingDuration);

            episode1InputKey.HasTitleSequence   = true;
            episode1InputKey.TitleSequenceStart = intros[0];
            episode1InputKey.TitleSequenceEnd   = intros[1];

            episode2InputKey.HasTitleSequence   = true;
            episode2InputKey.TitleSequenceStart = intros[2];
            episode2InputKey.TitleSequenceEnd   = intros[3];

            return Tuple.Create(episode1InputKey, episode2InputKey);

        }

        public Tuple<SequenceResult, SequenceResult> DetectEndCreditSequence(SequenceResult episode1InputKey, SequenceResult episode2InputKey, long episode1Runtime, long episode2Runtime)
        {
            
            if (episode1InputKey.EndCreditFingerprint is null)
            {
                throw new AudioFingerprintMissingException($" fingerprint data doesn't currently exist");
            }

            if (episode2InputKey.EndCreditFingerprint is null)
            {
                throw new AudioFingerprintMissingException($" fingerprint data doesn't currently exist");
            }

            if (episode1InputKey.Duration != episode2InputKey.Duration) //Both episodes should have the same encoding duration in seconds
            {
                throw new AudioFingerprintDurationMatchException("Fingerprint encoding durations don't match");
            }

            //Find credits
            var endCreditFingerprint1     = episode1InputKey.EndCreditFingerprint;
            var endCreditFingerprint2     = episode2InputKey.EndCreditFingerprint;
            var endCreditEncodingDuration = 3 * 60; //End credit encoding duration in seconds (3 minutes of the end of the show).

            var credits = CompareFingerprint(endCreditFingerprint1, endCreditFingerprint2, endCreditEncodingDuration);

            episode1InputKey.HasEndCreditSequence   = true;
            episode1InputKey.EndCreditSequenceStart = TimeSpan.FromTicks(episode1Runtime) - TimeSpan.FromSeconds(endCreditEncodingDuration) + credits[0];
            episode1InputKey.EndCreditSequenceEnd   = TimeSpan.FromTicks(episode1Runtime) - TimeSpan.FromSeconds(endCreditEncodingDuration) + credits[1];

            episode2InputKey.HasEndCreditSequence   = true;
            episode2InputKey.EndCreditSequenceStart = TimeSpan.FromTicks(episode2Runtime) - TimeSpan.FromSeconds(endCreditEncodingDuration) + credits[2];
            episode2InputKey.EndCreditSequenceEnd   = TimeSpan.FromTicks(episode2Runtime) - TimeSpan.FromSeconds(endCreditEncodingDuration) + credits[3];


            return Tuple.Create(episode1InputKey, episode2InputKey);

        }

        private List<TimeSpan> CompareFingerprint(List<uint> fingerprint1, List<uint> fingerprint2, double duration)
        {
            // We'll cut off a bit of the end if the fingerprints have an odd numbered length
            if (fingerprint1.Count % 2 != 0)
            {
                fingerprint1 = fingerprint1.GetRange(0, fingerprint1.Count() - 1);
                fingerprint2 = fingerprint2.GetRange(0, fingerprint2.Count() - 1);
            }

            int offset = GetBestOffset(fingerprint1, fingerprint2);
            

            var (f1, f2) = GetAlignedFingerprints(offset, fingerprint1, fingerprint2);

            // ReSharper disable once TooManyChainedReferences
            List<double> hammingDistances = Enumerable.Range(0, (f1.Count < f2.Count ? f1.Count : f2.Count)).Select(i => GetHammingDistance(f1[i], f2[i])).ToList();
           

            var (start, end) = FindContiguousRegion(hammingDistances, 8);


            double secondsPerSample = Convert.ToDouble(duration) / fingerprint1.Count;

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
                throw new SequenceInvalidDetectionException("Episode detection failed to find a reasonable start and end time.");
            }

            if (commonRegionEnd - commonRegionStart < (Plugin.Instance.Configuration.TitleSequenceLengthThreshold))
            {                
                throw new SequenceInvalidDetectionException("Episode common region is deemed too short to be considered a sequence.");

            }
            else if (start == 0 && end == 0)
            {
                throw new SequenceInvalidDetectionException("Episode common region are both 00:00:00.");
            }
            
            return new List<TimeSpan>()
            {
                TimeSpan.FromSeconds(Math.Floor(firstFileRegionStart)),             //<--Episode 1 sequence start
                TimeSpan.FromSeconds(Math.Ceiling(firstFileRegionEnd)),             //<--Episode 1 sequence end
                TimeSpan.FromSeconds(Math.Floor(secondFileRegionStart)),            //<--Episode 2 sequence start
                TimeSpan.FromSeconds(Math.Ceiling(secondFileRegionEnd))             //<--Episode 2 sequence end              
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