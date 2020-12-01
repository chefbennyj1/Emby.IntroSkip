using System;

namespace IntroSkip.Api {
    public class IntroDto {
        public long SeriesInternalId { get; set; }
        public long InternalId       { get; set; }
        public bool HasIntro         { get; set; }
        public TimeSpan IntroStart   { get; set; }
        public TimeSpan IntroEnd     { get; set; }
    }
}