using System.Collections.Generic;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintDto
    {
        public double duration { get; set; } 
        public List<uint> fingerprint { get; set; }
    }
}
