using System;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class IntroDataService : IService
    {
        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class EpisodeTitleSequence : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId     { get; set; }
            public bool HasIntro       { get; set; }
            public TimeSpan IntroStart { get; set; }
            public TimeSpan IntroEnd   { get; set; }
        }

        public IntroDataService()
        {

        }

        public string Get(EpisodeTitleSequence request)
        {
            var config = Plugin.Instance.Configuration;
            return "";
        }

    }
}
