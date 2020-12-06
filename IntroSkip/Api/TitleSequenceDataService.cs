using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class TitleSequenceDataService : IService
    {
        [Route("/RemoveIntro", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class RemoveTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }
        }

        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class TitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }
        }

        [Route("/SeriesTitleSequences", "GET", Summary = "All Saved Series Title Sequence Start and End Data by Series Id")]
        public class SeriesTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "SeriesInternalId", Description = "The Internal Id of the series", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeriesInternalId { get; set; }
        }

        private ILibraryManager LibraryManager { get; }
        private IJsonSerializer JsonSerializer { get; }
        private IUserManager UserManager       { get; }

        public TitleSequenceDataService(IJsonSerializer json, ILibraryManager libraryManager, IUserManager user)
        {
            JsonSerializer = json;
            LibraryManager = libraryManager;
            UserManager    = user;
        }

        public string Delete(RemoveTitleSequenceRequest request)
        {
            var config = Plugin.Instance.Configuration;
            config.Intros = config.Intros.Where(item => item.InternalId != request.InternalId).ToList();
            Plugin.Instance.UpdateConfiguration(config);
            return "OK";
        }

        public string Get(SeriesTitleSequenceRequest request)
        {
            var config = Plugin.Instance.Configuration;
            return JsonSerializer.SerializeToString(config.Intros.Where(intro => intro.SeriesInternalId == request.SeriesInternalId).OrderBy(item => LibraryManager.GetItemById(item.InternalId).IndexNumber));
        }
        
        public string Get(TitleSequenceRequest request)
        {
            var config = Plugin.Instance.Configuration;
            return JsonSerializer.SerializeToString(config.Intros.FirstOrDefault(episode => episode.InternalId == request.InternalId));
        }

    }
}
