using IntroSkip.Chapters;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    //Our chapter service. This will be available to the UI, or anyone else who wants to access this data.
    //It will be in the swagger as well. So it will be possible to test these endpoints.
    public class TitleSequenceChapterInsertionService : IService
    {
        private IJsonSerializer JsonSerializer { get; }
        public TitleSequenceChapterInsertionService(IJsonSerializer json)
        {
            JsonSerializer = json;
        }

        
        [Route("/ChapterErrors", "GET", Summary = "List of chapters that had issues during the ChapterEditScheduledTask. Available for the life of the Server only.")]
        public class ChapterErrorRequest : IReturn<string>
        {
            
        }

        public string Get(ChapterErrorRequest request)
        {
            return JsonSerializer.SerializeToString(ChapterInsertion.Instance.ChapterErrors);
        }
    }
}
