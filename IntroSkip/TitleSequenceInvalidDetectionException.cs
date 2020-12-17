using System;

namespace IntroSkip
{
    public class TitleSequenceInvalidDetectionException : Exception
    {
        public TitleSequenceInvalidDetectionException() : base() { }
        public TitleSequenceInvalidDetectionException(string message) : base(message) { }
        public TitleSequenceInvalidDetectionException(string message, System.Exception inner) : base(message, inner) { }

        protected TitleSequenceInvalidDetectionException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
