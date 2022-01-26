using IntroSkip.Configuration;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{

    public class LocalizationService : IService
    {
        [Route("/MessageLanguages", "GET", Summary = "Available languages for Auto Skip")]
        public class MessageLanguagesRequest : IReturn<string>
        {

        }
        private IJsonSerializer JsonSerializer { get; }
        public IHttpResultFactory ResultFactory { get; set; }
        public LocalizationService(IJsonSerializer json)
        {
            JsonSerializer = json;
        }
        public string Get(MessageLanguagesRequest request)
        {
            return JsonSerializer.SerializeToString(Localization.IntroSkipLanguages.Keys);
        }
    }
}
