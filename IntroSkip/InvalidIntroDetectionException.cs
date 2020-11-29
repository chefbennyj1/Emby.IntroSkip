namespace IntroSkip
{
    public class InvalidIntroDetectionException : System.Exception
    {
        public InvalidIntroDetectionException() : base() { }
        public InvalidIntroDetectionException(string message) : base(message) { }
        public InvalidIntroDetectionException(string message, System.Exception inner) : base(message, inner) { }

        protected InvalidIntroDetectionException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
