using IntroSkip.Data;
using IntroSkip.TitleSequence;
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
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MediaBrowser.Model.Querying;

// ReSharper disable TooManyChainedReferences
// ReSharper disable MethodNameNotMeaningful

namespace IntroSkip.Api
{
    public class TitleSequenceService : IService, IHasResultFactory
    {
        
        [Route("/ExtractThumbImage", "GET", Summary = "Image jpg resource frame")]
        public class ExtractThumbImage : IReturn<object>
        {
            [ApiMember(Name = "ImageFrame", Description = "The image frame to extract from the stream", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public string ImageFrame { get; set; }

            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "long[]", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }

            [ApiMember(Name = "IsStart", Description = "Is the Title SequenceStart", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "GET")]
            public bool IsIntroStart { get; set; }
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

        [Route("/RemoveSeasonDataRequest", "DELETE", Summary = "Remove Episode Title Sequences for an entire season Start and End Data")]
        public class RemoveSeasonDataRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeasonId { get; set; }           
        }
        

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

        [Route("/UpdateTitleSequence", "POST", Summary = "Episode Title Sequence Update Data")]
        public class UpdateTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public long InternalId { get; set; }
            
            [ApiMember(Name = "TitleSequenceStart", Description = "The episode title sequence start time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public TimeSpan TitleSequenceStart { get; set; }
            
            [ApiMember(Name = "TitleSequenceEnd", Description = "The episode title sequence end time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            public TimeSpan TitleSequenceEnd { get; set; }
            
            [ApiMember(Name = "HasSequence", Description = "The episode has a sequence", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "POST")]
            public bool HasSequence { get; set; }
            
            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
            
            public long SeasonId { get; set; }

            [ApiMember(Name = "Confirmed", Description = "Confirmed Items", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "POST")]
            public bool Confirmed { get; set; }
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
            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "POST")]
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
            var requestFrame        = TimeSpan.Parse(request.ImageFrame);
            requestFrame            = requestFrame.Add( TimeSpan.FromSeconds(request.IsIntroStart ? 7 : -7)); //<--back track the image frame so it isn't always a black screen.
            var frame               = $"00:{requestFrame.Minutes}:{requestFrame.Seconds}"; 
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
            ITitleSequenceRepository repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            QueryResult<TitleSequenceResult> dbResults = repository.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
            List<TitleSequenceResult> titleSequences = dbResults.Items.ToList();


            foreach (var episode in titleSequences)
            {
                // ReSharper disable once PossibleNullReferenceException - It's there, we just requested it from the database in the UI
                episode.Confirmed = true;
                episode.Fingerprint = episode.Fingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
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
            var dbResults = repository.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = dbResults.Items.ToList();


            var titleSequence = titleSequences.FirstOrDefault(item => item.InternalId == request.InternalId);

            // ReSharper disable once PossibleNullReferenceException - It's there, we just requested it from the database in the UI
            titleSequence.TitleSequenceStart = request.TitleSequenceStart;
            titleSequence.TitleSequenceEnd = request.TitleSequenceEnd;
            titleSequence.HasSequence = request.HasSequence;
            titleSequence.Confirmed = request.Confirmed;
            titleSequence.Fingerprint = titleSequence.Fingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.

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
            TitleSequenceDetectionManager.Instance.Analyze(CancellationToken.None, null, request.InternalIds, repository);
            DisposeRepository(repository);
        }

        

        public string Delete(RemoveSeasonDataRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var seasonResult = repository.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
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
            public List<BaseTitleSequence> TitleSequences { get; set; }
        }

        public string Get(SeasonTitleSequenceRequest request)
        {

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var query = new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId };
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
                return JsonSerializer.SerializeToString(new BaseTitleSequence()); //Empty
            }

        }

        private TimeSpan CalculateCommonTitleSequenceLength(List<BaseTitleSequence> season)
        {
            var titleSequences = season.Where(intro => intro.HasSequence);
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

        private void DisposeRepository(ITitleSequenceRepository repository)
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
        private List<long> GetSeasonalIntroVariance(ITitleSequenceRepository repository)
        {
            var dbResults          = repository.GetBaseTitleSequenceResults(new TitleSequenceResultQuery());
            var baseTitleSequences = dbResults.Items;
            var seasonalGroups     = baseTitleSequences.GroupBy(sequence => sequence.SeasonId);
            var abnormalities      = new List<long>();
            foreach (var group in seasonalGroups)
            {
                if (group.All(item => item.HasSequence || !item.HasSequence)) continue; //If they all have sequence data continue
                if (group.Any(item => item.HasSequence)) //Some of these items have sequence data and some do not.
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