using System;
using System.Collections.Generic;

namespace IntroSkip 
{
    public class EpisodeTitleSequence
    {
        public int? IndexNumber { get; set; }
        public long InternalId       { get; set; }
        public bool HasIntro         { get; set; }
        public TimeSpan IntroStart   { get; set; }
        public TimeSpan IntroEnd     { get; set; }
    }
    
    public class TitleSequenceDto
    {
        public List<EpisodeTitleSequence> EpisodeTitleSequences { get; set; }
    }
}