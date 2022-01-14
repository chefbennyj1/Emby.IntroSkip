using System;
using System.Runtime.Serialization;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintDurationMatchException : Exception
    {
        public AudioFingerprintDurationMatchException() { }
        public AudioFingerprintDurationMatchException(string message) : base(message) { }
        public AudioFingerprintDurationMatchException(string message, Exception inner) : base(message, inner) { }

        protected AudioFingerprintDurationMatchException(SerializationInfo info,
            StreamingContext context) : base(info, context) { }
    }
}
