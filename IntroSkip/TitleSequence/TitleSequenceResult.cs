using System.Collections.Generic;

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceResult : BaseTitleSequence
    {
        public List<uint> Fingerprint { get; set; }
        public double Duration { get; set; }
        
        public bool Processed { get; set; } = false;
    }
}
