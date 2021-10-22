using System;

namespace IntroSkip.Sequence
{
    public class SequenceInvalidDetectionException : Exception
    {
        public SequenceInvalidDetectionException() : base() { }
        public SequenceInvalidDetectionException(string message) : base(message) { }
        public SequenceInvalidDetectionException(string message, System.Exception inner) : base(message, inner) { }

        protected SequenceInvalidDetectionException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
