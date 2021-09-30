using System;
using IntroSkip.Chapters;
using MediaBrowser.Controller.Library;
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

        [Route("/UpdateChapter", "POST", Summary = "Process the chapter data on an item")]
        public class UpdateChapterRequest : IReturnVoid
        {
            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public long InternalId { get; set; }
        }

        public string Get(ChapterErrorRequest request)
        {
            return JsonSerializer.SerializeToString(ChapterInsertion.Instance.ChapterErrors);
        }

        public void Post(UpdateChapterRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var titleSequence = repository.GetResult(request.InternalId.ToString());
            
            ChapterInsertion.Instance.InsertIntroChapters(request.InternalId, titleSequence);
            
            var repo = repository as IDisposable;
            repo.Dispose();
        }
    }
}
