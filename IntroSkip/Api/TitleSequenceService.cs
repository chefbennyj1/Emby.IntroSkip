using IntroSkip.Data;
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using IntroSkip.Configuration;
using IntroSkip.Detection;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Querying;

namespace IntroSkip.Api
{
    public class TitleSequenceService : IService, IHasResultFactory
    {
        public enum SequenceImageType
        {
            IntroStart  = 0,
            IntroEnd    = 1,
            CreditStart = 2,
            CreditEnd   = 3
        }
        [Route("/ExtractThumbImage", "GET", Summary = "Image jpg resource frame")]
        public class ExtractThumbImage : IReturn<object>
        {
            [ApiMember(Name = "ImageFrameTimestamp", Description = "The image frame time stamp to extract from the stream", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public string ImageFrameTimestamp { get; set; }

            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "long[]", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }

            [ApiMember(Name = "SequenceImageType", Description = "IntroStart = 0, IntroEnd = 1, CreditStart = 2, CreditEnd = 3", IsRequired = true, DataType = "SequenceImageType", ParameterType = "query", Verb = "GET")]
            public SequenceImageType SequenceImageType { get; set; }
            
            public object Img { get; set; }
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

        }

        [Route("/GetSeasonStatistics", "GET", Summary = "Get Statics by Season")]
        public class SeasonStatisticsRequest : IReturn<string> 
        {
            //No args to pass - all code is done in the request below
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
            
            [ApiMember(Name = "HasTitleSequence", Description = "The episode has a sequence", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "POST")]
            public bool HasTitleSequence { get; set; }
            
            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public long SeasonId { get; set; }

            
        }

        [Route("/SeasonalIntroVariance", "GET", Summary = "Episode Title Sequence Variance Data")]
        public class SeasonalIntroVariance : IReturn<string>
        {

        }

        [Route("/NoTitleSequenceThumbImage", "GET", Summary = "No Title Sequence Thumb Image")]
        public class NoTitleSequenceThumbImageRequest : IReturn<object>
        {

        }

        [Route("/ConfirmAllSeasonIntros", "POST", Summary = "Confirms All Episodes in the Season are correct")]
        public class ConfirmAllSeasonIntrosRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "POST")]
            public long SeasonId { get; set; }
        }

        private IJsonSerializer JsonSerializer { get; }
        private ILogger Log { get; }

        private ILibraryManager LibraryManager { get; }

        public IHttpResultFactory ResultFactory { get; set; }

        private IFfmpegManager FfmpegManager { get; set; }

        public IRequest Request { get; set; }
      
       

        // ReSharper disable once TooManyDependencies
        public TitleSequenceService(IJsonSerializer json, ILogManager logMan, ILibraryManager libraryManager, IHttpResultFactory resultFactory, IFfmpegManager ffmpegManager)
        {
            JsonSerializer = json;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            LibraryManager = libraryManager;
            ResultFactory = resultFactory;
            FfmpegManager = ffmpegManager;
        }

        public async Task<object> Get(ExtractThumbImage request)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath          = ffmpegConfiguration.EncoderPath;
            var item                = LibraryManager.GetItemById(request.InternalId);
            var requestFrame        = TimeSpan.Parse(request.ImageFrameTimestamp);
            switch(request.SequenceImageType)
            {
                case SequenceImageType.CreditStart:
                case SequenceImageType.IntroStart:
                    requestFrame += TimeSpan.FromSeconds(7); //<--push the image frame so it isn't always a black screen.
                    break;
                case SequenceImageType.CreditEnd:
                case SequenceImageType.IntroEnd:
                    requestFrame -= TimeSpan.FromSeconds(7); //<--back up the image frame so it isn't always a black screen.
                    break;
            }
            
            var frame               = $"{requestFrame.Hours}:{requestFrame.Minutes}:{requestFrame.Seconds}"; 
            var args                = $"-accurate_seek -ss {frame} -i \"{ item.Path }\" -vcodec mjpeg -vframes 1 -an -f rawvideo -s 175x100 -";
            var procStartInfo       = new ProcessStartInfo(ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            FileStream output;

            using (var process = new Process { StartInfo = procStartInfo })
            {
                process.Start();
                output = await Task.Factory.StartNew(() => process.StandardOutput.BaseStream as FileStream);
            }

            return ResultFactory.GetResult(Request, output, "image/bmp");

        }

        public async Task<object> Get(NoTitleSequenceThumbImageRequest request) =>
            await Task<object>.Factory.StartNew(() => GetEmbeddedResourceStream("no_intro.png", "image/png"));

        public void Post(ConfirmAllSeasonIntrosRequest request)
        {
            ISequenceRepository repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            QueryResult<SequenceResult> dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = request.SeasonId });
            List<SequenceResult> titleSequences = dbResults.Items.ToList();


            foreach (var episode in titleSequences)
            {
                // ReSharper disable once PossibleNullReferenceException - It's there, we just requested it from the database in the UI
                episode.Confirmed = true;
                episode.TitleSequenceFingerprint = episode.TitleSequenceFingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
                try
                {
                    repository.SaveResult(episode, CancellationToken.None);

                }
                catch (Exception ex)
                {
                    Log.Warn(ex.Message);
                    //return "error";
                }
            }
            DisposeRepository(repository);
            //return "OK";

        }

        public string Get(SeasonalIntroVariance request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var variance =  JsonSerializer.SerializeToString(GetSeasonalIntroVariance(repository));
            
            DisposeRepository(repository);
            return variance;
        }

        public void Post(UpdateTitleSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new SequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = dbResults.Items.ToList();
            
            var titleSequence = titleSequences.FirstOrDefault(item => item.InternalId == request.InternalId);

            
            titleSequence.TitleSequenceStart = request.TitleSequenceStart;
            titleSequence.TitleSequenceEnd = request.TitleSequenceEnd;
            titleSequence.HasTitleSequence = request.HasTitleSequence;
            titleSequence.CreditSequenceStart = request.CreditSequenceStart;
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
        }

        public string Get(SeasonTitleSequenceRequest request)
        {


            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var query = new SequenceResultQuery() { SeasonInternalId = request.SeasonId };
            var dbResults = repository.GetBaseTitleSequenceResults(query);

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

            DisposeRepository(repository);

            return JsonSerializer.SerializeToString(new SeasonTitleSequenceResponse()
            {
                CommonEpisodeTitleSequenceLength = commonDuration,
                TitleSequences = titleSequences
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

       public string Get(SeasonStatisticsRequest request)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            ReturnedDetectionStatsList.Clear();
            GetDetectionStatistics();
            if (!config.EnableFullStatistics)
            {
                ReturnedDetectionStatsList.RemoveAll(x => !x.HasIssue);
                ReturnedDetectionStatsList.Sort((x, y) => string.CompareOrdinal(x.TVShowName, y.TVShowName));
            }
            else
            {
                ReturnedDetectionStatsList.Sort((x, y) => string.CompareOrdinal(x.TVShowName, y.TVShowName));
            }

            foreach (var stat in ReturnedDetectionStatsList)
            {
                Log.Info("STATISTICS: DETECTIONS STATISTICS have started for {0}: {1}", stat.TVShowName, stat.Season);
                Log.Debug("STATISTICS: Season ID: {0}", stat.SeasonId.ToString());
                Log.Debug("STATISTICS: No of Episodes:{0}", stat.EpisodeCount.ToString());
                Log.Debug("STATISTICS: No of detected Episodes:{0}", stat.HasSeqCount.ToString());
                Log.Info("STATISTICS: DETECTION SUCCESS = {0}%", stat.PercentDetected.ToString(CultureInfo.InvariantCulture));
                Log.Info("STATISTICS: HAS ISSUE = {0}", stat.HasIssue.ToString());
            }
            return JsonSerializer.SerializeToString(ReturnedDetectionStatsList);
        }
       

        public static List<DetectionStats> ReturnedDetectionStatsList = new List<DetectionStats>();
        
        public void GetDetectionStatistics()
        {
            var seriesList = new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                IsVirtualItem = false,
            };

            var seriesItems = LibraryManager.GetItemList(seriesList);
            //var seriesItemsCount = seriesItems.Count();
            
            Log.Info("STATISTICS: Series Count = {0}", seriesItems.Length.ToString());
            List<long> seasonIds = new List<long>();
            foreach (var season in seriesItems)
            {
                var seasonInternalItemQuery = new InternalItemsQuery()
                {
                    Parent = season,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Season" },
                    IsVirtualItem = false,
                };
                BaseItem[] seasonItems = LibraryManager.GetItemList(seasonInternalItemQuery);

                foreach (var id in seasonItems)
                {
                    seasonIds.Add(id.InternalId);
                    
                }
            }
            Log.Info("STATISTICS: No of Seasons to process = {0}", seasonIds.Count.ToString());

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            try
            {
                foreach (var season in seasonIds)
                {
                    var query = new SequenceResultQuery() { SeasonInternalId = season };
                    var dbResults = repository.GetBaseTitleSequenceResults(query);

                    var seasonItem = LibraryManager.GetItemById(season);
                    var detectedSequences = dbResults.Items.ToList();

                    TimeSpan commonDuration;
                    try
                    {
                        commonDuration = CalculateCommonTitleSequenceLength(detectedSequences);
                    }
                    catch
                    {
                        commonDuration = new TimeSpan(0, 0, 0);
                    }


                    int hasIntroCount = 0;
                    int totalEpisodeCount = 0;

                    foreach (var episode in detectedSequences)
                    {
                        totalEpisodeCount++;

                        if (episode.HasTitleSequence)
                        {
                            hasIntroCount++;
                        }
                        else
                        {
                            hasIntroCount += 0;
                        }
                    }

                    //Hoping not using this will increase performance massively.
                    if (totalEpisodeCount == hasIntroCount || hasIntroCount == 0)
                    {
                        ReturnedDetectionStatsList.Add(new DetectionStats
                        {
                            Date = DateTime.Now,
                            SeasonId = seasonItem.InternalId,
                            TVShowName = seasonItem.Parent.Name,
                            Season = seasonItem.Name,
                            EpisodeCount = totalEpisodeCount,
                            HasSeqCount = hasIntroCount,
                            PercentDetected = 100,
                            IntroDuration = commonDuration,
                            Comment = "Looks Good",
                            HasIssue = false
                        });
                    }

                    else
                    {
                        int x = hasIntroCount;
                        int y = totalEpisodeCount;
                        double percentage = Math.Round((double)x / y * 100);
                        
                        ReturnedDetectionStatsList.Add(new DetectionStats
                        {
                            Date = DateTime.Now,
                            SeasonId = seasonItem.InternalId,
                            TVShowName = seasonItem.Parent.Name,
                            Season = seasonItem.Name,
                            EpisodeCount = totalEpisodeCount,
                            HasSeqCount = hasIntroCount,
                            PercentDetected = percentage,
                            IntroDuration = commonDuration,
                            Comment = "Needs Attention",
                            HasIssue = true
                        });
                    }
                }

                DisposeRepository(repository);
            }
            catch(Exception e)
            {
                Log.Warn("STATISTICS: ******* ISSUE CREATING STATS FOR INTROSKIP *********");
            }
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

        private object GetEmbeddedResourceStream(string resourceName, string contentType)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceName));

            return ResultFactory.GetResult(Request, GetType().Assembly.GetManifestResourceStream(name), contentType);
        }

        private void DisposeRepository(ISequenceRepository repository)
        {
            // ReSharper disable once UsePatternMatching
            var repo = repository as IDisposable;
            repo?.Dispose();
        }

        /// <summary>
        /// List of seasons where some episodes have intros and some do not.
        /// </summary>
        /// <param name="repository"></param>
        /// <returns></returns>
        private List<long> GetSeasonalIntroVariance(ISequenceRepository repository)
        {
            var dbResults          = repository.GetBaseTitleSequenceResults(new SequenceResultQuery());
            var baseTitleSequences = dbResults.Items;
            var seasonalGroups     = baseTitleSequences.GroupBy(sequence => sequence.SeasonId);
            var abnormalities      = new List<long>();
            foreach (var group in seasonalGroups)
            {
                if (group.All(item => item.HasTitleSequence || !item.HasTitleSequence)) continue; //If they all have sequence data continue
                if (group.Any(item => item.HasTitleSequence)) //Some of these items have sequence data and some do not.
                {
                    abnormalities.Add(group.Key);
                }
            }
            var repo = repository as IDisposable;
            repo.Dispose();
            return abnormalities;
        }
    }
}