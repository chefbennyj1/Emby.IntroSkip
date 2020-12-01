using System;
using System.Linq;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class TitleSequenceDataService : IService
    {
        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class TitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId     { get; set; }
        }
        

        private IJsonSerializer JsonSerializer { get; set; }
        public TitleSequenceDataService(IJsonSerializer json)
        {
            JsonSerializer = json;
        }

        public string Get(TitleSequenceRequest request)
        {
            var config = Plugin.Instance.Configuration;
            return JsonSerializer.SerializeToString(config.Intros.FirstOrDefault(episode => episode.InternalId == request.InternalId));
        }

    }
}
