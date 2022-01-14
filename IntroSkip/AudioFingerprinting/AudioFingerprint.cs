using System.Collections.Generic;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprint
    {
        public long InternalId { get; set; }
        public List<uint> TitleSequenceFingerprint { get; set; }
        public List<uint> CreditSequenceFingerprint { get; set; }
        public double Duration { get; set; }
    }
}
