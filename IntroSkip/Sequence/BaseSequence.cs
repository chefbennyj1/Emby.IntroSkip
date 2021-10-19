using System;

namespace IntroSkip.Sequence
{
    public class BaseSequence
    {
        public int? IndexNumber                      { get; set; }
        public long InternalId                       { get; set; }
        public bool HasTitleSequence                 { get; set; } = false;
        public bool HasEndCreditSequence             { get; set; } = false;
        public TimeSpan TitleSequenceStart           { get; set; }
        public TimeSpan TitleSequenceEnd             { get; set; }
        public TimeSpan EndCreditSequenceStart       { get; set; }
        public TimeSpan EndCreditSequenceEnd         { get; set; }
        public long SeriesId                         { get; set; }
        public long SeasonId                         { get; set; }
        public bool Confirmed                        { get; set; }

    }
}
