using System;
using System.Collections.Generic;

namespace IntroSkip 
{
    public class IntroData
    {
        public List<SeriesIntro> Series { get; set; }
    }
    public class SeriesIntro
    {
        public long InternalId                  { get; set; }
        public List<EpisodeIntro> EpisodeIntros { get; set; }
    }
    public class EpisodeIntro 
    {
        public long InternalId     { get; set; }
        public bool HasIntro       { get; set; }
        public TimeSpan IntroStart { get; set; }
        public TimeSpan IntroEnd   { get; set; }
    }
}