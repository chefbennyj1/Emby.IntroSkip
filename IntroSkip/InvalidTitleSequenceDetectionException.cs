namespace IntroSkip
{
    public class InvalidTitleSequenceDetectionException : System.Exception
    {
        public InvalidTitleSequenceDetectionException() : base() { }
        public InvalidTitleSequenceDetectionException(string message) : base(message) { }
        public InvalidTitleSequenceDetectionException(string message, System.Exception inner) : base(message, inner) { }

        protected InvalidTitleSequenceDetectionException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
