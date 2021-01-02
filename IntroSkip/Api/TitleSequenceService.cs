using System;
using System.Linq;
using IntroSkip.AudioFingerprinting;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Entities;
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
        [Route("/RemoveIntro", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class RemoveTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long InternalId { get; set; }
        }

        [Route("/RemoveAll", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class RemoveAllRequest : IReturn<string>
        {
           
        }

        [Route("/RemoveSeasonFingerprints", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class RemoveSeasonFingerprintsRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long SeasonId { get; set; }
        }

        [Route("/RemoveFingerprint", "DELETE", Summary = "Remove Episode Title Sequence Start and End Data")]
        public class RemoveFingerprintRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "DELETE")]
            public long InternalId { get; set; }
        }

        [Route("/EpisodeTitleSequence", "GET", Summary = "Episode Title Sequence Start and End Data")]
        public class EpisodeTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "InternalId", Description = "The Internal Id of the episode", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }
        }

        [Route("/SeasonTitleSequences", "GET", Summary = "All Saved Series Title Sequence Start and End Data by Series Id")]
        public class SeasonTitleSequenceRequest : IReturn<string>
        {
            [ApiMember(Name = "SeasonId", Description = "The Internal Id of the Season", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeasonId { get; set; }
            [ApiMember(Name = "SeriesId", Description = "The Internal Id of the Series", IsRequired = true, DataType = "long", ParameterType = "query", Verb = "GET")]
            public long SeriesId { get; set; }
        }

        
        private IJsonSerializer JsonSerializer      { get; }
        private ILogger Log                         { get; }
        private IFileSystem FileSystem              { get; }
        private ILibraryManager LibraryManager      { get; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceService(IJsonSerializer json, ILogManager logMan, IFileSystem fileSystem, ILibraryManager libraryManager)
        {
            JsonSerializer = json;
            FileSystem     = fileSystem;
            LibraryManager = libraryManager;
            Log            = logMan.GetLogger(Plugin.Instance.Name);
        }

        public string Delete(RemoveAllRequest request)
        {
            try
            {
                var fingerprintFiles = FileSystem
                    .GetFiles(
                        $"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{FileSystem.DirectorySeparatorChar}",
                        true).Where(file => file.Extension == ".json");
                foreach (var file in fingerprintFiles)
                {
                    FileSystem.DeleteFile(file.FullName);
                }
            }catch {}

            try
            {
                var titleSequenceFiles = FileSystem
                    .GetFiles(
                        $"{TitleSequenceFileManager.Instance.GetTitleSequenceDirectory()}{FileSystem.DirectorySeparatorChar}",
                        true).Where(file => file.Extension == ".json");
                foreach (var file in titleSequenceFiles)
                {
                    FileSystem.DeleteFile(file.FullName);
                }
            }catch {}

            return "OK";
        }

        public string Delete(RemoveSeasonFingerprintsRequest request)
        {
            var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
            {
                ParentIds = new []{ request.SeasonId },
                Recursive = true,
                IncludeItemTypes = new[] { "Episode" }
            });

            foreach (var episode in episodeQuery.Items)
            {
                var fingerPrintHash = AudioFingerprintFileManager.Instance.GetFingerprintFileNameHash(episode);
                //Remove the finger print file
                if (!FileSystem.FileExists($"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{FileSystem.DirectorySeparatorChar}{fingerPrintHash}.json")) continue;
                
                try
                {
                    FileSystem.DeleteFile($"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{FileSystem.DirectorySeparatorChar}{fingerPrintHash}.json");
                }
                catch { }
            }

            return "OK";

        }

        public string Delete(RemoveFingerprintRequest request)
        {
            try
            {
                var episode        = LibraryManager.GetItemById(request.InternalId);
                var season         = episode.Parent;
                var series         = episode.Parent.Parent;
                var titleSequences = TitleSequenceFileManager.Instance.GetTitleSequenceFromFile(series);

                if (titleSequences.Seasons is null)
                {
                    return "";
                }

                if (titleSequences.Seasons.Exists(item => item.IndexNumber == season.IndexNumber))
                {
                    if(titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber)
                        .Episodes.Exists(item => item.InternalId == episode.InternalId))
                    {
                        titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber)
                            .Episodes.RemoveAll(item => item.InternalId == request.InternalId);
                    }
                    TitleSequenceFileManager.Instance.SaveTitleSequenceJsonToFile(series, titleSequences);
                }

               
                var fingerPrintHash = AudioFingerprintFileManager.Instance.GetFingerprintFileNameHash(episode);
                //Remove the finger print file
                if (FileSystem.FileExists($"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{FileSystem.DirectorySeparatorChar}{fingerPrintHash}.json"))
                {
                    try
                    {
                        FileSystem.DeleteFile($"{AudioFingerprintFileManager.Instance.GetFingerprintDirectory()}{FileSystem.DirectorySeparatorChar}{fingerPrintHash}.json");
                    }
                    catch { }
                }

                

                Log.Info("Title sequence finger print file removed.");

                return "OK";
            }
            catch
            {
                return "";
            }
        }
        
        public string Delete(RemoveTitleSequenceRequest request)
        {
            try
            {
                var episode        = LibraryManager.GetItemById(request.InternalId);
                var season         = episode.Parent;
                var series         = season.Parent;

                var titleSequences = TitleSequenceFileManager.Instance.GetTitleSequenceFromFile(series);

                if (titleSequences.Seasons is null) return "";

                if (!titleSequences.Seasons.Exists(item => item.IndexNumber == season.IndexNumber)) return "";

                if (titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber)
                    .Episodes.Exists(item => item.InternalId == episode.InternalId))
                {
                    titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber)
                        .Episodes.RemoveAll(item => item.InternalId == request.InternalId);
                }

                TitleSequenceFileManager.Instance.SaveTitleSequenceJsonToFile(series, titleSequences);

                Log.Info("Title sequence saved intro data removed.");


                return "OK";
            }
            catch
            {
                return "";
            }
        }

        private class SeasonTitleSequenceResponse
        {
            public TimeSpan CommonEpisodeTitleSequenceLength  { get; set; }
            public TitleSequenceDto TitleSequences { get; set; }
        }

        
        public string Get(SeasonTitleSequenceRequest request)
        {
            var season = LibraryManager.GetItemById(request.SeasonId);
            var series = season.Parent;
            var titleSequences = TitleSequenceFileManager.Instance.GetTitleSequenceFromFile(series);
            

            TimeSpan commonDuration;
            try
            {
                commonDuration = CalculateCommonTitleSequenceLength(titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber));
            }
            catch
            {
                commonDuration = new TimeSpan(0,0,0);
            }

            
            return JsonSerializer.SerializeToString(new SeasonTitleSequenceResponse()
            {
                CommonEpisodeTitleSequenceLength = commonDuration,
                TitleSequences = titleSequences
            });

        }

        
        public string Get(EpisodeTitleSequenceRequest request)
        {
            try
            {
                var episode        = LibraryManager.GetItemById(request.InternalId);
                var season         = episode.Parent;
                var series         = season.Parent;
                var titleSequences = TitleSequenceFileManager.Instance.GetTitleSequenceFromFile(series);

                if (titleSequences.Seasons is null)
                {
                    return JsonSerializer.SerializeToString(new Episode()); //Empty
                }

                if (titleSequences.Seasons.Exists(item => item.IndexNumber == season.IndexNumber))
                {
                    var s = titleSequences.Seasons.FirstOrDefault(item => item.IndexNumber == season.IndexNumber);
                    
                    if(s.Episodes.Exists(item => item.InternalId == episode.InternalId))
                    {
                        return JsonSerializer.SerializeToString(s.Episodes.FirstOrDefault(e => e.InternalId == request.InternalId));
                    }
                    
                }
                
            }
            catch
            {
                return JsonSerializer.SerializeToString(new Episode()); //Empty
            }

            return JsonSerializer.SerializeToString(new Episode()); //Empty
        }

        private TimeSpan CalculateCommonTitleSequenceLength(Season season)
        {
            var titleSequences      = season.Episodes.Where(intro => intro.HasIntro);
            var groups              = titleSequences.GroupBy(sequence => sequence.IntroEnd - sequence.IntroStart);
            var enumerableSequences = groups.ToList();
            int maxCount            = enumerableSequences.Max(g => g.Count());
            var mode                = enumerableSequences.First(g => g.Count() == maxCount).Key;
            return mode;
        }

    }
}
