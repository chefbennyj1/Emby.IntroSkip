using IntroSkip.Statistics;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using System.IO;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Configuration;
using IntroSkip.Data;
using IntroSkip.Detection;
using IntroSkip.Sequence;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.IO;

namespace IntroSkip.Api
{
    public class SequenceService : IService
    {
        
        [Route("/HasChromaprint", "GET", Summary = "FFMPEG has chromaprint capabilities")]
        public class HasChromaprintRequest : IReturn<bool>
        {

        }

        [Route("/ScanSeries", "POST", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class ScanSeriesRequest : IReturnVoid
        {
            [ApiMember(Name = "InternalIds", Description = "Comma delimited list Internal Ids of the series to scan", IsRequired = true, DataType = "long[]", ParameterType = "query", Verb = "POST")]
            public long[] InternalIds { get; set; }
        }
        
        [Route("/RemoveAll", "DELETE", Summary = "Remove All Episode Title Sequence Data")]
        public class RemoveAllRequest : IReturn<string>
        {

        }

        [Route("/RemoveSeasonData", "DELETE", Summary = "Remove Episode Title Sequences for an entire season Start and End Data")]
        public class RemoveSeasonDataRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeasonId { get; set; }           
        }
        

        [Route("/EpisodeSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class EpisodeTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }
        }

        [Route("/SeasonSequences", "GET", Summary = "All Title Sequence Start and End Data by Season Id")]
        public class SeasonTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
            [ApiMember(Name = "StartIndex", Description = "The Start Index of the query", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "GET")]
            public int StartIndex { get; set; }
            [ApiMember(Name = "Limit", Description = "The Limit of the query", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "GET")]
            public int Limit { get; set; }
        }

        [Route("/GetSeasonStatistics", "GET", Summary = "Get Statics by Season")]
        public class SeasonStatisticsRequest : IReturn<string> 
        {
            //No args to pass - all code is done in the request below
        }

        [Route("/UpdateAllSeasonSequences", "POST", Summary = "Season Title Sequence Update Data")]
        public class UpdateAllSeasonSequencesRequest : IReturn<string>
        {
            public List<UpdateTitleSequenceRequest> TitleSequencesUpdate { get; set; }
        }

        [Route("/UpdateSequence", "POST", Summary = "Episode Title Sequence Update Data")]
        public class UpdateTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public long InternalId { get; set; }
            
            [ApiMember(Name = "TitleSequenceStart", Description = "The episode title sequence start time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public TimeSpan TitleSequenceStart { get; set; }

            [ApiMember(Name = "CreditSequenceStart", Description = "The episode credit sequence start time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public TimeSpan CreditSequenceStart { get; set; }
            
            [ApiMember(Name = "TitleSequenceEnd", Description = "The episode title sequence end time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public TimeSpan TitleSequenceEnd { get; set; }
            
            [ApiMember(Name = "HasTitleSequence", Description = "The episode has a title sequence", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "POST")]
            public bool HasTitleSequence { get; set; }

            [ApiMember(Name = "HasCreditSequence", Description = "The episode has a end credits", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "POST")]
            public bool HasCreditSequence { get; set; }

            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public long SeasonId { get; set; }

            
        }

        
        private IJsonSerializer JsonSerializer { get; }
        private ILogger Log { get; }
        private IHttpResultFactory ResultFactory { get; set; }
        private StatsManager StatsManager { get; set; }
        private IFileSystem FileSystem { get; }
        private IApplicationPaths ApplicationPaths { get; }
        private char Separator { get; }


        // ReSharper disable once TooManyDependencies
        public SequenceService(IJsonSerializer json, ILogManager logMan, ILibraryManager libraryManager, IHttpResultFactory resultFactory, IFfmpegManager ffmpegManager, StatsManager statsManager, IApplicationPaths applicationPaths, IFileSystem fileSystem)
        {
            JsonSerializer = json;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            ResultFactory = resultFactory;
            StatsManager = statsManager;
            FileSystem = fileSystem;
            ApplicationPaths = applicationPaths;
            Separator = FileSystem.DirectorySeparatorChar;
        }

        public bool Get(HasChromaprintRequest request)
        {
            return AudioFingerprintManager.Instance.HasChromaprint();
        }
       
        public void Post(UpdateAllSeasonSequencesRequest request)
        {
            var update = request.TitleSequencesUpdate;
            var seasonId = update.FirstOrDefault()?.SeasonId; //Get the season Id from the first item (they are all from the same season.

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = seasonId });
            var titleSequences = dbResults.Items.ToList();

            foreach (var item in update)
            {
                var titleSequence = titleSequences.FirstOrDefault(s => s.InternalId == item.InternalId);
                titleSequence.TitleSequenceStart = item.TitleSequenceStart;
                titleSequence.TitleSequenceEnd = item.TitleSequenceEnd;
                titleSequence.HasTitleSequence = item.HasTitleSequence;
                titleSequence.CreditSequenceStart = item.CreditSequenceStart;
                titleSequence.HasCreditSequence = item.CreditSequenceStart != TimeSpan.FromSeconds(0); //this was not getting updated when user clicked save
                titleSequence.Confirmed = true;
                titleSequence.TitleSequenceFingerprint = titleSequence.TitleSequenceFingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
                titleSequence.CreditSequenceFingerprint = titleSequence.CreditSequenceFingerprint ?? new List<uint>();
                try
                {
                    repository.SaveResult(titleSequence, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex.Message);
                }

                if (Plugin.Instance.Configuration.ImageCache)
                {
                    SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroStart, titleSequence.TitleSequenceStart.ToString(@"hh\:mm\:ss"));
                    SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroEnd, titleSequence.TitleSequenceEnd.ToString(@"hh\:mm\:ss"));
                    SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.CreditStart, titleSequence.CreditSequenceStart.ToString(@"hh\:mm\:ss"));
                }
            }

            DisposeRepository(repository);
            

        }

        public void Post(UpdateTitleSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = dbResults.Items.ToList();
            
            //We assume this does exist because we have already loaded it in the UI, and we are editing it there.
            var titleSequence = titleSequences.FirstOrDefault(item => item.InternalId == request.InternalId);

            
            titleSequence.TitleSequenceStart = request.TitleSequenceStart;
            titleSequence.TitleSequenceEnd = request.TitleSequenceEnd;
            titleSequence.HasTitleSequence = request.HasTitleSequence;
            titleSequence.CreditSequenceStart = request.CreditSequenceStart;
            titleSequence.HasCreditSequence = titleSequence.CreditSequenceStart != TimeSpan.FromSeconds(0); //this was not getting updated when user clicked save
            titleSequence.Confirmed = true;
            titleSequence.TitleSequenceFingerprint = titleSequence.TitleSequenceFingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
            titleSequence.CreditSequenceFingerprint = titleSequence.CreditSequenceFingerprint ?? new List<uint>();
            try
            {
                repository.SaveResult(titleSequence, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
                //return "error";
            }
            if (Plugin.Instance.Configuration.ImageCache)
            {
                SequenceThumbnailService.Instance.RemoveCacheImages(titleSequence.InternalId);
                SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroStart, titleSequence.TitleSequenceStart.ToString(@"hh\:mm\:ss"));
                SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroEnd, titleSequence.TitleSequenceEnd.ToString(@"hh\:mm\:ss"));
                SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.CreditStart, titleSequence.CreditSequenceStart.ToString(@"hh\:mm\:ss"));
            }
            DisposeRepository(repository);
            //return "OK";

        }

        public void Post(ScanSeriesRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            SequenceDetectionManager.Instance.Analyze(CancellationToken.None, null, request.InternalIds, repository);
            DisposeRepository(repository);
        }

        

        public string Delete(RemoveSeasonDataRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var seasonResult = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = seasonResult.Items.ToList();
            foreach (var item in seasonResult.Items)
            {
                try
                {

                    repository.Delete(item.InternalId.ToString());
                    titleSequences.Remove(item);
                    
                    if (Plugin.Instance.Configuration.ImageCache)
                    {
                        SequenceThumbnailService.Instance.RemoveCacheImages(item.InternalId);
                    }
                }
                catch { }
            }

            DisposeRepository(repository);

            return JsonSerializer.SerializeToString(titleSequences);

        }



        private class SeasonTitleSequenceResponse
        {
            // ReSharper disable twice UnusedAutoPropertyAccessor.Local
            public TimeSpan CommonEpisodeTitleSequenceLength { get; set; }
            public List<BaseSequence> TitleSequences { get; set; }
            public int TotalRecordCount { get; set; }
        }

        public string Get(SeasonTitleSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            
            var query = new SequenceResultQuery() { SeasonInternalId = request.SeasonId};
            var dbResults = repository.GetBaseTitleSequenceResults(query);

            var titleSequences = dbResults.Items.ToList();
            
            //titleSequences.Sort((x, y) => x.IndexNumber.Value.CompareTo(y.IndexNumber.Value));
            
            TimeSpan commonDuration;
            try
            {
                commonDuration = CalculateCommonTitleSequenceLength(titleSequences);
            }
            catch
            {
                commonDuration = new TimeSpan(0, 0, 0);
            }

            DisposeRepository(repository);

            var recordEnd = request.Limit;
            if (request.StartIndex + request.Limit >= titleSequences.Count)
            {
                recordEnd = titleSequences.Count - request.StartIndex;
            }
            
            return JsonSerializer.SerializeToString(new SeasonTitleSequenceResponse()
            {
                CommonEpisodeTitleSequenceLength = commonDuration,
                TitleSequences = titleSequences.GetRange(request.StartIndex, recordEnd),
                TotalRecordCount = titleSequences.Count
            });

        }

        public string Get(EpisodeTitleSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();

            try
            {
                var data = repository.GetBaseTitleSequence(request.InternalId.ToString());
                DisposeRepository(repository);
                return JsonSerializer.SerializeToString(data);

            }
            catch
            {
                DisposeRepository(repository);
                return JsonSerializer.SerializeToString(new BaseSequence()); //Empty
            }

        }

        public class UIStats
        {
            public bool HasIssue { get; set; }
            public string TVShowName { get; set; }
            public string Season { get; set; }
            public int EpisodeCount { get; set; }
            public TimeSpan IntroDuration { get; set; }
            public double PercentDetected { get; set; }
            public double EndPercentDetected { get; set; }
            public string Comment { get; set; } 
            public DateTime Date { get; set; }
        }

       public string Get(SeasonStatisticsRequest request)
       {
           
            PluginConfiguration config = Plugin.Instance.Configuration;
            List<UIStats> statsList = new List<UIStats>();

            var configDir = ApplicationPaths.PluginConfigurationsPath;
            Log.Debug("STATISTICS: SERVICE - Getting statistics for UI from Text file");
            string statsFilePath = $"{configDir}{Separator}IntroSkipInfo{Separator}DetectionResults.txt";

            if (FileSystem.FileExists(statsFilePath) == false)
            {   //OMG this is hilarious :)

                statsList.Add(new UIStats
                {
                    HasIssue = true,
                    TVShowName = "Please Run IntroSkip",
                    Season = "Statistics Task",
                    EpisodeCount = 0,
                    IntroDuration = TimeSpan.Parse("12:11:59"),
                    PercentDetected = 66.6,
                    //EndPercentDetected = 66.6,
                    Comment = "Go Run the STATISTICS TASK",
                    Date = Convert.ToDateTime(DateTime.Now)
                });
            }
            else
            {
                var lines = File.ReadLines(statsFilePath).Skip(1);
                foreach (string line in lines)
                {
                    Log.Info("STATISTICS: LINE = {0}", line);

                    var tempLine = line.Split('\t');
                    statsList.Add(new UIStats
                    {
                        HasIssue = Convert.ToBoolean(tempLine[0]),
                        TVShowName = tempLine[1],
                        Season = tempLine[2],
                        EpisodeCount = Convert.ToInt32(tempLine[3]),
                        IntroDuration = TimeSpan.Parse(tempLine[4]),
                        PercentDetected = Convert.ToDouble(tempLine[5]),
                        //EndPercentDetected = Convert.ToDouble(tempLine[6]),
                        Comment = tempLine[7],
                        Date = Convert.ToDateTime(tempLine[8])
                    });
                }
            }
            if (!config.EnableFullStatistics)
            {
                statsList.RemoveAll(x => !x.HasIssue);
            }
            Log.Info("STATISTICS: DETECTIONS STATISTICS LIST COUNT = {0}", statsList.Count.ToString());

            return JsonSerializer.SerializeToString(statsList);
       }

       private TimeSpan CalculateCommonTitleSequenceLength(List<BaseSequence> season)
        {
            var titleSequences = season.Where(intro => intro.HasTitleSequence);
            var groups = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd - sequence.TitleSequenceStart);
            var enumerableSequences = groups.ToList();
            int maxCount = enumerableSequences.Max(g => g.Count());
            var mode = enumerableSequences.First(g => g.Count() == maxCount).Key;
            return mode;
        }
        
        private void DisposeRepository(ISequenceRepository repository)
        {
            // ReSharper disable once UsePatternMatching
            var repo = repository as IDisposable;
            repo?.Dispose();
        }

        
    }
}