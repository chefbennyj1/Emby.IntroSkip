using System;
using System.Linq;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class TitleSequenceDataService : IService
    {
        [Route("/AverageTitleSequenceLength", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class AverageTitleSequenceLengthRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
            [ApiMember(Name = "SeriesId", Description = "The Internal Id of the Series", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeriesId { get; set; }
        }

        [Route("/RemoveIntro", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class RemoveTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "EpisodeId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long EpisodeId { get; set; }
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeasonId { get; set; }
            [ApiMember(Name = "SeriesId", Description = "The Internal Id of the Series", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeriesId { get; set; }
        }

        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class EpisodeTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
            [ApiMember(Name = "SeriesId", Description = "The Internal Id of the Series", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeriesId { get; set; }
        }

        [Route("/SeriesTitleSequences", "GET", Summary = "All Saved Series Title Sequence Start and End Data by Series Id")]
        public class SeriesTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
            [ApiMember(Name = "SeriesId", Description = "The Internal Id of the Series", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeriesId { get; set; }
        }

        private ILibraryManager LibraryManager      { get; }
        private IJsonSerializer JsonSerializer      { get; }
        private IUserManager UserManager            { get; }
        private ILogger Log                         { get; }
        private IFileSystem FileSystem { get; }
        // ReSharper disable once TooManyDependencies
        public TitleSequenceDataService(IJsonSerializer json, ILibraryManager libraryManager, IUserManager user, ILogManager logMan, IFileSystem fileSystem)
        {
            JsonSerializer = json;
            LibraryManager = libraryManager;
            UserManager    = user;
            FileSystem = fileSystem;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }
        

        public string Delete(RemoveTitleSequenceRequest request)
        {
            Log.Info("Delete Title Sequence");
            Log.Info(request.EpisodeId.ToString());
            Log.Info(request.SeasonId.ToString());
            Log.Info(request.SeriesId.ToString());

            try
            {
                var titleSequences = IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(request.SeriesId, request.SeasonId);

                if (!titleSequences.EpisodeTitleSequences.Any())
                {
                    return "";
                }

                if (titleSequences.EpisodeTitleSequences.Exists(item => item.InternalId == request.EpisodeId))
                {
                   titleSequences.EpisodeTitleSequences.RemoveAll(item => item.InternalId == request.EpisodeId);
                }

                //Remove the finger print file
                if (FileSystem.FileExists($"{IntroServerEntryPoint.Instance.FingerPrintDir}{FileSystem.DirectorySeparatorChar}{request.SeasonId}{request.EpisodeId}.json"))
                {
                    try
                    {
                        FileSystem.DeleteFile($"{IntroServerEntryPoint.Instance.FingerPrintDir}{FileSystem.DirectorySeparatorChar}{request.SeasonId}{request.EpisodeId}.json");
                    }
                    catch { }
                }

                Log.Info("Title sequence finger print file and saved intro data removed.");

                //We'll have to double check this!
                IntroServerEntryPoint.Instance.SaveTitleSequenceJsonToFile(request.SeriesId, request.SeasonId, titleSequences);

                return "OK";
            }
            catch
            {
                return "";
            }
        }

        private class SeriesTitleSequenceResponse
        {
            public TimeSpan CommonEpisodeTitleSequenceLength { get; set; }
            public TitleSequenceDto TitleSequences { get; set; }
        }

        public string Get(SeriesTitleSequenceRequest request)
        {
            try
            {
                var titleSequences = IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(request.SeriesId, request.SeasonId);
                if (titleSequences.EpisodeTitleSequences is null) return "";

                var episodeTitleSequences = titleSequences.EpisodeTitleSequences.OrderBy(item => item.IndexNumber).ToList();
                titleSequences.EpisodeTitleSequences = episodeTitleSequences;

                return JsonSerializer.SerializeToString(new SeriesTitleSequenceResponse()
                {
                    CommonEpisodeTitleSequenceLength = CalculateCommonTitleSequenceLength(titleSequences),
                    TitleSequences = titleSequences
                });
            }
            catch
            {
                return "";
            }
        }

        
        public string Get(EpisodeTitleSequenceRequest request)
        {
            try
            {
                var titleSequences = IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(request.SeriesId, request.SeasonId);
                if (titleSequences.EpisodeTitleSequences is null) return "";

                var episodeTitleSequences = titleSequences.EpisodeTitleSequences;
                if (episodeTitleSequences.Exists(item => item.InternalId == request.InternalId))
                {
                    return JsonSerializer.SerializeToString(episodeTitleSequences?.FirstOrDefault(episode => episode.InternalId == request.InternalId));
                }
            }
            catch
            {
                return "";
            }

            return "";
        }

        private TimeSpan CalculateCommonTitleSequenceLength(TitleSequenceDto titleSequenceDto)
        {
            var titleSequences      = titleSequenceDto.EpisodeTitleSequences.Where(intro => intro.HasIntro);
            var groups              = titleSequences.GroupBy(sequence => sequence.IntroEnd - sequence.IntroStart);
            var enumerableSequences = groups.ToList();
            int maxCount            = enumerableSequences.Max(g => g.Count());
            var mode                = enumerableSequences.First(g => g.Count() == maxCount).Key;
            return mode;
        }

    }
}
