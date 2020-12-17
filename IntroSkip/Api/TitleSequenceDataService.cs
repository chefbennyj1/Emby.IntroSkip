using System;
using System.Collections.Generic;
using System.Linq;
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

        
        private IJsonSerializer JsonSerializer      { get; }
        private ILogger Log                         { get; }
        private IFileSystem FileSystem              { get; }

       
        public TitleSequenceDataService(IJsonSerializer json, ILogManager logMan, IFileSystem fileSystem)
        {
            JsonSerializer = json;
            FileSystem     = fileSystem;
            Log            = logMan.GetLogger(Plugin.Instance.Name);
        }
        

        public string Delete(RemoveTitleSequenceRequest request)
        {
            try
            {
                var titleSequences = TitleSequenceEncodingDirectoryEntryPoint.Instance.GetTitleSequenceFromFile(request.SeriesId, request.SeasonId);

                if (!titleSequences.EpisodeTitleSequences.Any())
                {
                    return "";
                }

                if (titleSequences.EpisodeTitleSequences.Exists(item => item.InternalId == request.EpisodeId))
                {
                   titleSequences.EpisodeTitleSequences.RemoveAll(item => item.InternalId == request.EpisodeId);
                }

                ////Remove the finger print file
                //if (FileSystem.FileExists($"{TitleSequenceEncodingDirectoryEntryPoint.Instance.FingerPrintDir}{FileSystem.DirectorySeparatorChar}{request.SeasonId}{request.EpisodeId}.json"))
                //{
                //    try
                //    {
                //        FileSystem.DeleteFile($"{TitleSequenceEncodingDirectoryEntryPoint.Instance.FingerPrintDir}{FileSystem.DirectorySeparatorChar}{request.SeasonId}{request.EpisodeId}.json");
                //    }
                //    catch { }
                //}
                
                TitleSequenceEncodingDirectoryEntryPoint.Instance.SaveTitleSequenceJsonToFile(request.SeriesId, request.SeasonId, titleSequences);

                Log.Info("Title sequence finger print file and saved intro data removed.");

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
            public TitleSequenceDto TitleSequences           { get; set; }
        }

        public string Get(SeriesTitleSequenceRequest request)
        {
            
            var titleSequences = TitleSequenceEncodingDirectoryEntryPoint.Instance.GetTitleSequenceFromFile(request.SeriesId, request.SeasonId);
            
            TimeSpan commonDuration;
            try
            {
                commonDuration = CalculateCommonTitleSequenceLength(titleSequences);
            }
            catch
            {
                commonDuration = new TimeSpan(0,0,0);
            }

            
            return JsonSerializer.SerializeToString(new SeriesTitleSequenceResponse()
            {
                CommonEpisodeTitleSequenceLength = commonDuration,
                TitleSequences = titleSequences
            });

        }

        
        public string Get(EpisodeTitleSequenceRequest request)
        {
            try
            {
                var titleSequences = TitleSequenceEncodingDirectoryEntryPoint.Instance.GetTitleSequenceFromFile(request.SeriesId, request.SeasonId);
                if (titleSequences.EpisodeTitleSequences is null) return JsonSerializer.SerializeToString(new List<EpisodeTitleSequence>());

                var episodeTitleSequences = titleSequences.EpisodeTitleSequences;
                if (episodeTitleSequences.Exists(item => item.InternalId == request.InternalId))
                {
                    return JsonSerializer.SerializeToString(episodeTitleSequences?.FirstOrDefault(episode => episode.InternalId == request.InternalId));
                }
            }
            catch
            {
                return JsonSerializer.SerializeToString(new List<EpisodeTitleSequence>());
            }

            return JsonSerializer.SerializeToString(new List<EpisodeTitleSequence>());
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
