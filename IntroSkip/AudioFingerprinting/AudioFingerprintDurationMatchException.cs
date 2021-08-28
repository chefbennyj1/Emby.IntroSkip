using System;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintDurationMatchException : Exception
    {
        public AudioFingerprintDurationMatchException() : base() { }
        public AudioFingerprintDurationMatchException(string message) : base(message) { }
        public AudioFingerprintDurationMatchException(string message, System.Exception inner) : base(message, inner) { }

        protected AudioFingerprintDurationMatchException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
