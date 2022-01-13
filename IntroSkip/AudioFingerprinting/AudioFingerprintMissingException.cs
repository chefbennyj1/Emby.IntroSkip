using System;
using System.Runtime.Serialization;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintMissingException : Exception
    {
        public AudioFingerprintMissingException() { }
        public AudioFingerprintMissingException(string message) : base(message) { }
        public AudioFingerprintMissingException(string message, Exception inner) : base(message, inner) { }

        protected AudioFingerprintMissingException(SerializationInfo info,
            StreamingContext context) : base(info, context) { }
    }
}
