using System;
using System.Collections.Generic;

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceDto
    {
        public List<Season> Seasons { get; set; }
    }

    public class Season
    {
        public int? IndexNumber { get; set; }
        public List<Episode> Episodes { get; set; }
    }

    public class Episode
    {
        public int? IndexNumber      { get; set; }
        public long InternalId       { get; set; }
        public bool HasIntro         { get; set; }
        public TimeSpan IntroStart   { get; set; }
        public TimeSpan IntroEnd     { get; set; }
    }
}
