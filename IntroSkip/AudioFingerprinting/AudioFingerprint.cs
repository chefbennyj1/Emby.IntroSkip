using System.Collections.Generic;
using System.IO;
using MediaBrowser.Controller.Entities;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprint
    {
        public double duration { get; set; } 
        public List<uint> fingerprint { get; set; }
    }
}
