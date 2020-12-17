using System;

namespace IntroSkip
{
    public class AudioFingerprintMissingException : Exception
    {
        public AudioFingerprintMissingException() : base() { }
        public AudioFingerprintMissingException(string message) : base(message) { }
        public AudioFingerprintMissingException(string message, System.Exception inner) : base(message, inner) { }

        protected AudioFingerprintMissingException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
