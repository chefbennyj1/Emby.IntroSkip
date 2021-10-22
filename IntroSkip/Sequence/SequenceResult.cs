using System.Collections.Generic;

namespace IntroSkip.Sequence
{
    public class SequenceResult : BaseSequence
    {
        public List<uint> TitleSequenceFingerprint { get; set; }
        public List<uint> CreditSequenceFingerprint { get; set; }
        public double Duration { get; set; }

        
    }
}
