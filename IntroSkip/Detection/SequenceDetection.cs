/*
 * Plugin intro detection code translated from it's original author:
 * https://gist.githubusercontent.com/puzzledsam/c0731702a9eab244afacbcb777c9f5e9/raw/1fd81d1101eebc08d442acfd88742b5e5635f1ab/introDetection.py
 * Intro detection algorithm is derived from VictorBitca/matcher, which was originally written in Go. https://github.com/VictorBitca/matcher *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

// ReSharper disable ComplexConditionExpression

namespace IntroSkip.Detection
{
    public class SequenceDetection : SequenceResult, IServerEntryPoint
    {
        public static SequenceDetection Instance { get; private set; }
        private static ILogger Log { get; set; }

        public SequenceDetection(ILogManager logMan)
        {
            Log = logMan.GetLogger(Plugin.Instance.Name);
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

        private static double GetFastHammingDistance(uint x, uint y)
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
                var hammingDistance = GetFastHammingDistance(f1[i], f2[i]);
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
        private static bool nextOnesAreAlsoSmall(List<double> hammingDistances, int index, int upperLimit)
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

        public List<SequenceResult> DetectSequences(BaseItem episode1Input, BaseItem episode2Input, QueryResult<SequenceResult> result, Stopwatch stopWatch)
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


            //Scan for Title Sequence, then for credit sequences
            var introDto  = new List<SequenceResult>(); //<-- Default empty
            var creditDto = new List<SequenceResult>(); //<-- Default empty
            try
            {
                introDto = CompareFingerprint(episode1InputKey, episode2InputKey, episode1Input, episode2Input, isTitleSequence: true);  //<--we'll change here
                //Log.Info($"{episode1Input.Parent.Parent.Name} {episode1Input.Parent.Name} Episode: {episode1Input.IndexNumber} matching Episode {episode2Input.IndexNumber} title sequence detection took {stopWatch.Elapsed.Seconds} seconds.");
            }
            catch { }

            try
            {
                creditDto = CompareFingerprint(episode1InputKey, episode2InputKey, episode1Input, episode2Input, isTitleSequence: false); //<-- we'll change here
                //Log.Info($"{episode1Input.Parent.Parent.Name} {episode1Input.Parent.Name} Episode: {episode1Input.IndexNumber} matching Episode {episode2Input.IndexNumber} credit sequence detection took {stopWatch.ElapsedMilliseconds} milliseconds.");
            }
            catch { }

            if (!introDto.Any() && creditDto.Any()) throw new SequenceInvalidDetectionException();
            

            if (creditDto.Any(item => item.HasCreditSequence))
            {
                episode1InputKey.HasCreditSequence   = creditDto[0].HasCreditSequence;
                episode1InputKey.CreditSequenceStart = creditDto[0].CreditSequenceStart;
                episode1InputKey.CreditSequenceEnd   = TimeSpan.FromTicks(episode1Input.RunTimeTicks.Value);

                episode2InputKey.HasCreditSequence   = creditDto[1].HasCreditSequence;
                episode2InputKey.CreditSequenceStart = creditDto[1].CreditSequenceStart;
                episode2InputKey.CreditSequenceEnd   = TimeSpan.FromTicks(episode2Input.RunTimeTicks.Value);
            }
            if (introDto.Any(item => item.HasTitleSequence))
            {
                episode1InputKey.HasTitleSequence   = introDto[0].HasTitleSequence;
                episode1InputKey.TitleSequenceStart = introDto[0].TitleSequenceStart;
                episode1InputKey.TitleSequenceEnd   = introDto[0].TitleSequenceEnd;

                episode2InputKey.HasTitleSequence   = introDto[1].HasTitleSequence;
                episode2InputKey.TitleSequenceStart = introDto[1].TitleSequenceStart;
                episode2InputKey.TitleSequenceEnd   = introDto[1].TitleSequenceEnd;
            }

            return new List<SequenceResult>()
            {
                episode1InputKey,
                episode2InputKey
            };

        }


        private List<SequenceResult> CompareFingerprint(SequenceResult episode1, SequenceResult episode2, BaseItem episode1Input, BaseItem episode2Input, bool isTitleSequence)
        {
            //var creditEncodingDuration = TimeSpan.FromTicks(episode1Input.RunTimeTicks.Value) > TimeSpan.FromMinutes(35) ? 3 : 1.5;

            var duration = isTitleSequence ? episode1.Duration * 60 : 3 * 60; //Both episodes should have the same encoding duration
            

            var fingerprint1 = isTitleSequence ? episode1.TitleSequenceFingerprint : episode1.CreditSequenceFingerprint;
            var fingerprint2 = isTitleSequence ? episode2.TitleSequenceFingerprint : episode2.CreditSequenceFingerprint;


            // We'll cut off a bit of the end if the fingerprints have an odd numbered length
            if (fingerprint1.Count % 2 != 0)
            {
                fingerprint1 = fingerprint1.GetRange(0, fingerprint1.Count() - 1);
                fingerprint2 = fingerprint2.GetRange(0, fingerprint2.Count() - 1);
            }

            int offset = GetBestOffset(fingerprint1, fingerprint2);

            var (f1, f2) = GetAlignedFingerprints(offset, fingerprint1, fingerprint2);

            // ReSharper disable once TooManyChainedReferences
            List<double> hammingDistances = Enumerable.Range(0, (f1.Count < f2.Count ? f1.Count : f2.Count)).Select(i => GetFastHammingDistance(f1[i], f2[i])).ToList();
           

            //Added for Sam to test upper threshold changes
            var config = Plugin.Instance.Configuration;
            var (start, end) = FindContiguousRegion(hammingDistances, 8);


            double secondsPerSample = Convert.ToDouble(duration) / fingerprint1.Count;

            var offsetInSeconds       = offset * secondsPerSample;
            var commonRegionStart     = start * secondsPerSample;
            var commonRegionEnd       = (end * secondsPerSample);

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
                throw new SequenceInvalidDetectionException("Episode detection failed to find a reasonable intro start and end time.");
            }
            if (commonRegionEnd - commonRegionStart < (Plugin.Instance.Configuration.TitleSequenceLengthThreshold))
            {
                
                throw new SequenceInvalidDetectionException("Episode common region is deemed too short to be considered an intro.");

            }
            else if (start == 0 && end == 0)
            {
                throw new SequenceInvalidDetectionException("Episode common region are both 00:00:00.");
            }

            if (isTitleSequence)
            {
                episode1.HasTitleSequence   = true;
                episode1.TitleSequenceStart = TimeSpan.FromSeconds(Math.Floor(firstFileRegionStart));
                episode1.TitleSequenceEnd   = TimeSpan.FromSeconds(Math.Ceiling(firstFileRegionEnd));


                episode2.HasTitleSequence   = true;
                episode2.TitleSequenceStart = TimeSpan.FromSeconds(Math.Floor(secondFileRegionStart));
                episode2.TitleSequenceEnd   = TimeSpan.FromSeconds(Math.Ceiling(secondFileRegionEnd));
            }
            else
            {
                episode1.HasCreditSequence   = true;
                episode1.CreditSequenceStart = TimeSpan.FromTicks(episode1Input.RunTimeTicks.Value) - TimeSpan.FromSeconds(duration) + TimeSpan.FromSeconds(Math.Round(firstFileRegionStart));

                episode2.HasCreditSequence   = true;
                episode2.CreditSequenceStart = TimeSpan.FromTicks(episode2Input.RunTimeTicks.Value) - TimeSpan.FromSeconds(duration) + TimeSpan.FromSeconds(Math.Round(secondFileRegionStart));
            }

            return new List<SequenceResult>()
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