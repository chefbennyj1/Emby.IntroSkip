using System;
using System.Runtime.Serialization;

namespace IntroSkip.Detection
{
    public class SequenceInvalidDetectionException : Exception
    {
        public SequenceInvalidDetectionException() { }
        public SequenceInvalidDetectionException(string message) : base(message) { }
        public SequenceInvalidDetectionException(string message, Exception inner) : base(message, inner) { }

        protected SequenceInvalidDetectionException(SerializationInfo info,
            StreamingContext context) : base(info, context) { }
    }
}
