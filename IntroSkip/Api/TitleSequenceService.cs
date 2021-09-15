using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

// ReSharper disable TooManyChainedReferences
// ReSharper disable MethodNameNotMeaningful

namespace IntroSkip.Api
{
    public class TitleSequenceService : IService
    {
        
        [Route("/ScanSeries", "POST", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class ScanSeriesRequest : IReturnVoid
        {
            [ApiMember(Name = "InternalIds", Description = "Comma delimited list Internal Ids of the series to scan", IsRequired = true, DataType = "long[]", ParameterType = "query", Verb = "POST")]
            public long[] InternalIds { get; set; }
        }

        //[Route("/RemoveIntro", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        //public class RemoveTitleSequenceRequest : IReturn<string>
        //{
        //    [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
        //    public long InternalId { get; set; }
        //}

        [Route("/RemoveAll", "DELETE", Summary = "Remove All Episode Title Sequence Data")]
        public class RemoveAllRequest : IReturn<string>
        {
           
        }

        [Route("/RemoveSeasonDataRequest", "DELETE", Summary = "Remove Episode Title Sequences for an entire season Start and End Data")]
        public class RemoveSeasonDataRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeasonId { get; set; }
        }

        //[Route("/RemoveEpisodeTitleSequenceData", "DELETE", Summary = "Remove Episode Title Sequence data")]
        //public class RemoveTitleSequenceDataRequest : IReturn<string>
        //{
        //    [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode title sequence data to remove", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
        //    public long InternalId { get; set; }
        //}

        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class EpisodeTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }
        }

        [Route("/SeasonTitleSequences", "GET", Summary = "All Title Sequence Start and End Data by Season Id")]
        public class SeasonTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
            
        }

        [Route("/UpdateTitleSequence", "GET", Summary = "Episode Title Sequence Update Data")]
        public class UpdateTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }           
            [ApiMember(Name = "TitleSequenceStart", Description = "The episode title sequence start time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public TimeSpan TitleSequenceStart {get; set;}
            [ApiMember(Name = "TitleSequenceEnd", Description = "The episode title sequence end time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public TimeSpan TitleSequenceEnd {get; set;}
            [ApiMember(Name = "HasSequence", Description = "The episode has a sequence", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "GET")]
            public bool HasSequence { get; set; }
            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }                     
        }
        
        private IJsonSerializer JsonSerializer      { get; }
        private ILogger Log                         { get; }  
        
        private ILibraryManager LibraryManager { get; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceService(IJsonSerializer json, ILogManager logMan, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            JsonSerializer = json;
            Log            = logMan.GetLogger(Plugin.Instance.Name);
            LibraryManager = libraryManager;
        }
                
       
        public string Get(UpdateTitleSequenceRequest request)
        {
            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var dbResults = repo.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = dbResults.Items.ToList();


            var titleSequence = titleSequences.FirstOrDefault(item => item.InternalId == request.InternalId);
            
            // ReSharper disable once PossibleNullReferenceException - It's there, we just requested it from the database in the UI
            titleSequence.TitleSequenceStart = request.TitleSequenceStart;
            titleSequence.TitleSequenceEnd   = request.TitleSequenceEnd;
            titleSequence.HasSequence        = request.HasSequence;
            titleSequence.Fingerprint        = titleSequence.Fingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
            
            try
            {                
                repo.SaveResult(titleSequence, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
                return "error";
            }
                          
            return "OK";

        }

        public void Post(ScanSeriesRequest request)
        {
            TitleSequenceDetectionManager.Instance.Analyze(CancellationToken.None, null, request.InternalIds, IntroSkipPluginEntryPoint.Instance.Repository);
        }

        //public string Delete(RemoveAllRequest request)
        //{
        //    try
        //    {
        //        var fingerprintFiles = FileSystem
        //            .GetFiles(
        //                $"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{FileSystem.DirectorySeparatorChar}",
        //                true).Where(file => file.Extension == ".json");
        //        foreach (var file in fingerprintFiles)
        //        {
        //            FileSystem.DeleteFile(file.FullName);
        //        }
        //    }catch {}

        //    try
        //    {
        //        var titleSequenceFiles = FileSystem
        //            .GetFiles(
        //                $"{TitleSequenceManager.Instance.GetTitleSequenceDirectory()}{FileSystem.DirectorySeparatorChar}",
        //                true).Where(file => file.Extension == ".json");
        //        foreach (var file in titleSequenceFiles)
        //        {
        //            FileSystem.DeleteFile(file.FullName);
        //        }
        //    }catch {}

        //    return "OK";
        //}

        public string Delete(RemoveSeasonDataRequest request)
        {
            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var seasonResult = repo.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
            foreach (var item in seasonResult.Items)
            {
                try
                {
                    repo.Delete(item.InternalId.ToString());
                }
                catch { }
            }

            return "OK";

        }

        //public string Delete(RemoveTitleSequenceDataRequest request)
        //{
        //    try
        //    {
        //        var repo = IntroSkipPluginEntryPoint.Instance.Repository;
        //        repo.Delete(request.InternalId.ToString());

        //        Log.Info("Title sequence fingerprint file removed.");

        //        return "OK";
        //    }
        //    catch
        //    {
        //        return "";
        //    }
        //}

        //public string Delete(RemoveTitleSequenceRequest request)
        //{
        //    try
        //    {
        //        var episode        = LibraryManager.GetItemById(request.InternalId);
        //        var season         = episode.Parent;
        //        var series         = season.Parent;

        //        var titleSequences = TitleSequenceFileManager.Instance.GetTitleSequenceFromFile(series);

        //        if (titleSequences.Seasons is null) return "";

        //        if (!titleSequences.Seasons.Exists(item => item.IndexNumber == season.IndexNumber)) return "";

        //        if (titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber)
        //            .Episodes.Exists(item => item.InternalId == episode.InternalId))
        //        {
        //            titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber)
        //                .Episodes.RemoveAll(item => item.InternalId == request.InternalId);
        //        }

        //        TitleSequenceFileManager.Instance.SaveTitleSequenceJsonToFile(series, titleSequences);

        //        Log.Info("Title sequence saved intro data removed.");


        //        return "OK";
        //    }
        //    catch
        //    {
        //        return "";
        //    }
        //}

        private class SeasonTitleSequenceResponse
        {
            // ReSharper disable twice UnusedAutoPropertyAccessor.Local
            public TimeSpan CommonEpisodeTitleSequenceLength  { get; set; }
            public List<BaseTitleSequence> TitleSequences   { get; set; }
        }

        public string Get(SeasonTitleSequenceRequest request)
        {

            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var query = new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId };
            var dbResults = repo.GetBaseTitleSequenceResults(query);

            var titleSequences = dbResults.Items.ToList();

            TimeSpan commonDuration;
            try
            {
                commonDuration = CalculateCommonTitleSequenceLength(titleSequences);
            }
            catch
            {
                commonDuration = new TimeSpan(0, 0, 0);
            }

            return JsonSerializer.SerializeToString(new SeasonTitleSequenceResponse()
            {
                CommonEpisodeTitleSequenceLength = commonDuration,
                TitleSequences = titleSequences
            });

        }

        public string Get(EpisodeTitleSequenceRequest request)
        {
            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
            
            try
            {
                return JsonSerializer.SerializeToString(repo.GetBaseTitleSequence(request.InternalId.ToString()));

            }
            catch
            {
                return JsonSerializer.SerializeToString(new BaseTitleSequence()); //Empty
            }

        }

        private TimeSpan CalculateCommonTitleSequenceLength(List<BaseTitleSequence> season)
        {
            var titleSequences      = season.Where(intro => intro.HasSequence);
            var groups              = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd - sequence.TitleSequenceStart);
            var enumerableSequences = groups.ToList();
            int maxCount            = enumerableSequences.Max(g => g.Count());
            var mode                = enumerableSequences.First(g => g.Count() == maxCount).Key;
            return mode;
        }

    }
}