using System.Collections.Generic;
using IntroSkip.Api;

namespace IntroSkip 
{
    public class IntroData : IntroDataService
    {
        public List<SeriesIntro> Series { get; set; }
    }
    public class SeriesIntro : IntroData
    {
        public long InternalId                                  { get; set; }
        public List<EpisodeTitleSequence> EpisodeTitleSequences { get; set; }
    }
}