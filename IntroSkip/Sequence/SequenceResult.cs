using IntroSkip.Sequence;
using System.Collections.Generic;

namespace IntroSkip.Sequence
{
    public class SequenceResult : BaseSequence
    {        
        public List<uint> TitleSequenceFingerprint { get; set; }
        public List<uint> EndCreditFingerprint { get; set; }
        public double Duration { get; set; }
        public bool Processed { get; set; } = false;
    }
}
