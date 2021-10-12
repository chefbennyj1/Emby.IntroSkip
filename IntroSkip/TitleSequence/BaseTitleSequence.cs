using System;

namespace IntroSkip.TitleSequence
{
    public class BaseTitleSequence
    {
        public int? IndexNumber { get; set; }
        public long InternalId { get; set; }
        public bool HasSequence { get; set; } = false;
        public TimeSpan TitleSequenceStart { get; set; }
        public TimeSpan TitleSequenceEnd { get; set; }
        public long SeriesId { get; set; }
        public long SeasonId { get; set; }
        public bool Confirmed { get; set; }

    }
}
