using System;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class TitleSequenceDataService : IService
    {
        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class TitleSequenceRequest : IReturn<EpisodeIntroDto>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId     { get; set; }
        }

        public class EpisodeIntroDto
        {
            public long SeriesInternalId { get; set; }
            public long InternalId       { get; set; }
            public bool HasIntro         { get; set; }
            public TimeSpan IntroStart   { get; set; }
            public TimeSpan IntroEnd     { get; set; }
        }

        public TitleSequenceDataService()
        {

        }

        public EpisodeIntroDto Get(TitleSequenceRequest request)
        {
            var config = Plugin.Instance.Configuration;
            return new EpisodeIntroDto();
        }

    }
}
