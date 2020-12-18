using System.Collections.Generic;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprint
    {
        public double duration { get; set; } 
        public List<uint> fingerprint { get; set; } 
    }
}
