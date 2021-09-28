using IntroSkip.Data;
using IntroSkip.TitleSequence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
            public bool RemoveAll { get; set; }
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
            public TimeSpan TitleSequenceStart { get; set; }
            [ApiMember(Name = "TitleSequenceEnd", Description = "The episode title sequence end time", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public TimeSpan TitleSequenceEnd { get; set; }
            [ApiMember(Name = "HasSequence", Description = "The episode has a sequence", IsRequired = true, DataType = "bool", ParameterType = "query", Verb = "GET")]
            public bool HasSequence { get; set; }
            [ApiMember(Name = "SeasonId", Description = "The season internal Id", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
        }

        [Route("/SeasonalIntroVariance", "GET", Summary = "Episode Title Sequence Variance Data")]
        public class SeasonalIntroVariance : IReturn<string>
        {

        }

        private IJsonSerializer JsonSerializer { get; }
        private ILogger Log { get; }

        //private ILibraryManager LibraryManager { get; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceService(IJsonSerializer json, ILogManager logMan)
        {
            JsonSerializer = json;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            //LibraryManager = libraryManager;
        }

        public string Get(SeasonalIntroVariance request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var variance =  JsonSerializer.SerializeToString(GetSeasonalIntroVariance(repository));
            
            DisposeRepository(repository);
            return variance;
        }

        public string Get(UpdateTitleSequenceRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = dbResults.Items.ToList();


            var titleSequence = titleSequences.FirstOrDefault(item => item.InternalId == request.InternalId);

            // ReSharper disable once PossibleNullReferenceException - It's there, we just requested it from the database in the UI
            titleSequence.TitleSequenceStart = request.TitleSequenceStart;
            titleSequence.TitleSequenceEnd = request.TitleSequenceEnd;
            titleSequence.HasSequence = request.HasSequence;
            titleSequence.Fingerprint = titleSequence.Fingerprint ?? new List<uint>(); //<-- fingerprint might have been removed form the DB, but we have to have something here.

            try
            {
                repository.SaveResult(titleSequence, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
                return "error";
            }

            DisposeRepository(repository);
            return "OK";

        }

        public void Post(ScanSeriesRequest request)
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            TitleSequenceDetectionManager.Instance.Analyze(CancellationToken.None, null, request.InternalIds, repository);
            DisposeRepository(repository);
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
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var seasonResult = repository.GetResults(new TitleSequenceResultQuery() { SeasonInternalId = request.SeasonId });
            var titleSequences = seasonResult.Items.ToList();
            foreach (var item in seasonResult.Items)
            {
                try
                {
                    if (request.RemoveAll)
                    {
                        repository.Delete(item.InternalId.ToString());
                        titleSequences.Remove(item);
                    }
                    else
                    {
                        if (item.Confirmed) continue;
                        repository.Delete(item.InternalId.ToString());
                        titleSequences.Remove(item);
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