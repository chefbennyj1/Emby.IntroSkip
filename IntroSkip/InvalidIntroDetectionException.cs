using System;
using System.Runtime.Serialization;

namespace IntroSkip
{
    public class InvalidIntroDetectionException : Exception
    {
        public InvalidIntroDetectionException()
        {
        }

        public InvalidIntroDetectionException(string message) : base(message)
        {
        }

        public InvalidIntroDetectionException(string message, Exception inner) : base(message, inner)
        {
        }

        protected InvalidIntroDetectionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}