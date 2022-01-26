using IntroSkip.Statistics;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Library;
using System.IO;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Configuration;
using IntroSkip.Data;
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

        [Route("/ResetSeasonData", "DELETE", Summary = "Reset Episode Sequence data for an entire season.")]
        public class RemoveSeasonDataRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeasonId { get; set; }  
            
            [ApiMember(Name = "RemoveFingerprintBinaryData", Description = "Remove the fingeprint binary data associated with this season", IsRequired = false, DataType = "bool", ParameterType = "query", Verb = "DELETE")]
            public bool? RemoveFingerprintBinaryData { get; set; } 
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

        [Route("/UpdateSeasonSequences", "POST", Summary = "Season Title Sequence Update Data")]
        public class UpdateSeasonSequencesRequest : IReturn<string>
        {
            public List<UpdateEpisodeSequenceRequest> TitleSequencesUpdate { get; set; }
        }

        [Route("/UpdateEpisodeSequence", "POST", Summary = "Episode Title Sequence Update Data")]
        public class UpdateEpisodeSequenceRequest : IReturn<string>
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


        [Route("/SeriesTitleSequences", "DELETE", Summary = "Reset an entire series title sequence data.")]
        public class DeleteSeriesTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The series internal Id", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long InternalId { get; set; }
        }

        [Route("/SeriesCreditSequences", "DELETE", Summary = "Reset an entire series credit sequence data.")]
        public class DeleteSeriesCreditSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The series internal Id", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long InternalId { get; set; }
        }
        
        private IJsonSerializer JsonSerializer     { get; }
        private ILogger Log                        { get; }
        private ILibraryManager LibraryManager     { get; }
        private IFileSystem FileSystem             { get; }
        private IApplicationPaths ApplicationPaths { get; }
        
        public SequenceService(IJsonSerializer json, ILogManager logMan, IApplicationPaths applicationPaths, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            JsonSerializer = json;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            FileSystem = fileSystem;
            LibraryManager = libraryManager;
            ApplicationPaths = applicationPaths;
            
        }

        public bool Get(HasChromaprintRequest request)
        {
            return AudioFingerprintManager.Instance.HasChromaprint();
        }

        public string Delete(DeleteSeriesTitleSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new SequenceResultQuery());
            var titleSequences = dbResults.Items.Where(item => item.SeriesId == request.InternalId);
            foreach (var sequence in titleSequences)
            {
                sequence.TitleSequenceStart = TimeSpan.Zero;
                sequence.TitleSequenceEnd = TimeSpan.Zero;
                sequence.HasTitleSequence = false;
                sequence.Confirmed = true;
                sequence.TitleSequenceFingerprint = new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
                sequence.CreditSequenceFingerprint = new List<uint>();
                try
                {
                    repository.SaveResult(sequence, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex.Message);
                }

                if (Plugin.Instance.Configuration.ImageCache)
                {
                    SequenceThumbnailService.Instance.RemoveCacheImages(sequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroStart);
                    SequenceThumbnailService.Instance.RemoveCacheImages(sequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroEnd);
                }
            }

            var baseItem = LibraryManager.GetItemById(request.InternalId);
            Log.Info(
                $"\nTitle Sequences Removed: {baseItem.Name}\n" +
                "Save Successful.\n");

            DisposeRepository(repository);
            return "OK";

        }

        public string Delete(DeleteSeriesCreditSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new SequenceResultQuery());
            var titleSequences = dbResults.Items.Where(item => item.SeriesId == request.InternalId);
            
            foreach (var sequence in titleSequences)
            {
                sequence.CreditSequenceStart = TimeSpan.Zero;
                sequence.CreditSequenceEnd = TimeSpan.Zero;
                sequence.HasCreditSequence = false;
                sequence.Confirmed = true;
                sequence.TitleSequenceFingerprint = new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
                sequence.CreditSequenceFingerprint = new List<uint>();
                try
                {
                    repository.SaveResult(sequence, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex.Message);
                }

                if (Plugin.Instance.Configuration.ImageCache)
                {
                    SequenceThumbnailService.Instance.RemoveCacheImages(sequence.InternalId, SequenceThumbnailService.SequenceImageType.CreditStart);
                }
            }

            var baseItem = LibraryManager.GetItemById(request.InternalId);
            Log.Info(
                $"\nCredit Sequences Removed: {baseItem.Name}\n" +
                "Save Successful.\n");

            DisposeRepository(repository);
            return "OK";
        }


        public void Post(UpdateSeasonSequencesRequest request)
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
                titleSequence.HasCreditSequence = item.CreditSequenceStart != TimeSpan.FromSeconds(0); 
                titleSequence.Confirmed = true;
                titleSequence.TitleSequenceFingerprint = new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
                titleSequence.CreditSequenceFingerprint = new List<uint>();
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
                var baseItem = LibraryManager.GetItemById(titleSequence.InternalId);
                Log.Info($"\nSequence Edit: {baseItem.Parent.Parent.Name} {baseItem.Parent.Name} Episode:{baseItem.IndexNumber}\n" +
                         $"Title Sequence Start: {titleSequence.TitleSequenceStart}\n" +
                         $"Title Sequence End: {titleSequence.TitleSequenceEnd}\n" +
                         $"Credit Sequence Start: {titleSequence.CreditSequenceStart}\n" +
                         "Save Successful.\n");
            }

            DisposeRepository(repository);
            

        }

        public void Post(UpdateEpisodeSequenceRequest request)
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
            titleSequence.TitleSequenceFingerprint = new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.
            titleSequence.CreditSequenceFingerprint = new List<uint>();
            
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
                SequenceThumbnailService.Instance.RemoveCacheImages(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroStart);
                SequenceThumbnailService.Instance.RemoveCacheImages(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroEnd);
                SequenceThumbnailService.Instance.RemoveCacheImages(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.CreditStart);

                //SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroStart, titleSequence.TitleSequenceStart.ToString(@"hh\:mm\:ss"));
                //SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.IntroEnd, titleSequence.TitleSequenceEnd.ToString(@"hh\:mm\:ss"));
                //SequenceThumbnailService.Instance.UpdateImageCache(titleSequence.InternalId, SequenceThumbnailService.SequenceImageType.CreditStart, titleSequence.CreditSequenceStart.ToString(@"hh\:mm\:ss"));
            }
            var baseItem = LibraryManager.GetItemById(titleSequence.InternalId);
            Log.Info($"\nSequence Edit: {baseItem.Parent.Parent.Name} {baseItem.Parent.Name} Episode:{baseItem.IndexNumber}\n" +
                      $"Title Sequence Start: {titleSequence.TitleSequenceStart}\n" +
                      $"Title Sequence End: {titleSequence.TitleSequenceEnd}\n" +
                      $"Credit Sequence Start: {titleSequence.CreditSequenceStart}\n" +
                      "Save Successful.\n");
            DisposeRepository(repository);
            //return "OK";

        }

        public void Post(ScanSeriesRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            //SequenceDetectionManager.Instance.Analyze(CancellationToken.None, null, request.InternalIds, repository);
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

                if (request.RemoveFingerprintBinaryData.HasValue)
                {
                    if (request.RemoveFingerprintBinaryData.Value) 
                    {
                        //Try to remove the Binary File for the title sequence
                        if (AudioFingerprintManager.Instance.TitleFingerprintExists(item.InternalId))
                        {
                            var titleSequenceBinFilePath = AudioFingerprintManager.Instance.GetTitleSequenceBinaryFilePath(item.InternalId);
                            try
                            {
                                FileSystem.DeleteFile(titleSequenceBinFilePath);
                                Log.Debug("Removing title sequence binary file successful.");
                            }
                            catch
                            {
                                Log.Warn("unable to remove title sequence fingerprint binary file path.");
                            }
                        }

                        //Try to remove the binary file for the Credit Sequence
                        if (AudioFingerprintManager.Instance.CreditFingerprintExists(item.InternalId))
                        {
                            var creditSequenceBinFilePath = AudioFingerprintManager.Instance.GetCreditSequenceBinaryFilePath(item.InternalId);
                            try
                            {
                                FileSystem.DeleteFile(creditSequenceBinFilePath);
                                Log.Debug("Removing credit sequence binary file successful.");
                            }
                            catch
                            {
                                Log.Warn("unable to remove credit sequence fingerprint binary file path.");
                            }
                        }
                        
                    }
                }
                
                if (!Plugin.Instance.Configuration.ImageCache) continue;
                SequenceThumbnailService.Instance.RemoveCacheImages(item.InternalId, SequenceThumbnailService.SequenceImageType.IntroStart);
                SequenceThumbnailService.Instance.RemoveCacheImages(item.InternalId, SequenceThumbnailService.SequenceImageType.IntroEnd);
                SequenceThumbnailService.Instance.RemoveCacheImages(item.InternalId, SequenceThumbnailService.SequenceImageType.CreditStart);

            }
            var baseItem = LibraryManager.GetItemById(request.SeasonId);
            Log.Info($"{baseItem.Parent.Name} - {baseItem.Name} sequence data was reset.");

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

        
       public string Get(SeasonStatisticsRequest request)
       {
           
            PluginConfiguration config = Plugin.Instance.Configuration;
            List<DetectionStats> statsList = new List<DetectionStats>();

            var configDir = ApplicationPaths.PluginConfigurationsPath;
            Log.Debug("STATISTICS: SERVICE - Getting statistics for UI from Text file");
            string statsFilePath = Path.Combine(configDir, "IntroSkipInfo", "DetectionResults.txt");

            if (!FileSystem.FileExists(statsFilePath))
            {   //OMG this is hilarious :)

                //statsList.Add(new DetectionStats
                //{
                //    HasIssue = true,
                //    TVShowName = "Please Run IntroSkip",
                //    Season = "Statistics Task",
                //    EpisodeCount = 0,
                //    IntroDuration = TimeSpan.Parse("12:11:59"),
                //    PercentDetected = 66.6,
                //    //EndPercentDetected = 66.6,
                //    Comment = "Go Run the STATISTICS TASK",
                    
                //});
            }
            else
            {
                var lines = File.ReadLines(statsFilePath).Skip(1);
                statsList.AddRange(lines.Select(line => line.Split('\t'))
                .Select(line => new DetectionStats()
                {
                    HasIssue = Convert.ToBoolean(line[0]),
                    TVShowName = line[1],
                    SeriesId = Convert.ToInt64(line[2]),
                    Season = line[3],
                    SeasonId = Convert.ToInt64(line[4]),
                    EpisodeCount = Convert.ToInt32(line[5]),
                    IntroDuration = TimeSpan.Parse(line[6]),
                    PercentDetected = Convert.ToDouble(line[7]),
                    EndPercentDetected = line[8] != "NaN" ? Convert.ToDouble(line[8]) : 0,
                }));
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
            var titleSequences      = season.Where(intro => intro.HasTitleSequence);
            var groups              = titleSequences.GroupBy(sequence => sequence.TitleSequenceEnd - sequence.TitleSequenceStart);
            var enumerableSequences = groups.ToList();
            int maxCount            = enumerableSequences.Max(g => g.Count());
            var mode                = enumerableSequences.First(g => g.Count() == maxCount).Key;
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