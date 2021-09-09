using System;
using System.Collections.Generic;

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceResult
    {
        public int? IndexNumber            { get; set; }
        public long InternalId             { get; set; }
        public bool HasSequence            { get; set; } = false;
        public TimeSpan TitleSequenceStart { get; set; }
        public TimeSpan TitleSequenceEnd   { get; set; }
        public List<uint> Fingerprint      {get; set;}
        public double Duration             { get; set; }
        public long SeriesId               { get; set; }
        public long SeasonId               { get; set; }
        public bool Confirmed              { get; set; }         
        public bool Processed              { get; set; } = false;
    }
}
