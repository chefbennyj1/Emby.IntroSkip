using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

// ReSharper disable once TooManyDependencies
// ReSharper disable three TooManyChainedReferences
// ReSharper disable once ExcessiveIndentation

namespace IntroSkip
{
    public class IntroDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log                    { get; set; }
        private ILibraryManager LibraryManager        { get; }
        private IUserManager UserManager              { get; }
        private IFileSystem FileSystem                { get; }
        private IApplicationPaths ApplicationPaths    { get; }
     
        public long CurrentSeriesEncodingInternalId   { get; set; }
        private static double Step                    { get; set; }
        public static bool QuickScan                  { get; set; } = false;
        
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user, IFileSystem file, IApplicationPaths paths)//, IJsonSerializer jsonSerializer)
        {
            Log              = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager   = libMan;
            UserManager      = user;
            //JsonSerializer   = jsonSerializer;
            FileSystem       = file;
            ApplicationPaths = paths;
        }
        
#pragma warning disable 1998
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#pragma warning restore 1998
        {
            Log.Info("Beginning Intro Task");

            //Remove any rouge wav files in the encoding folder
            RemoveAllPreviousEncodings();

            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            Step = CalculateStep(seriesQuery.TotalRecordCount);

            try
            {
                Parallel.ForEach(seriesQuery.Items, new ParallelOptions() {MaxDegreeOfParallelism = 4}, series =>
                {
                    if (string.IsNullOrEmpty(series.InternalId.ToString())) return;
                    
                    progress.Report((Step += Step) - (Step/2));

                    Log.Info(series.Name);

                    var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent = series,
                        Recursive = true,
                        IncludeItemTypes = new[] {"Season"},
                        User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem = false
                    });

                    try
                    {
                        foreach (var season in seasonQuery.Items)
                        {

                            var titleSequence =
                                IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(series.InternalId,
                                    season.InternalId);

                            var episodeTitleSequences = titleSequence.EpisodeTitleSequences.Distinct().ToList() ??
                                                        new List<EpisodeTitleSequence>();


                            var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                            {
                                Parent = season,
                                Recursive = true,
                                IncludeItemTypes = new[] {"Episode"},
                                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                                IsVirtualItem = false

                            });


                            //QuickScan is set to true when a new episode item has been added to the library and only items that don't already have intro data need to be scanned,
                            //otherwise QuickScan is false, and will scan all items with no intro data, and also try an rescan any items which have been marked with "HasIntro=false"
                            var exceptIds = QuickScan
                                ? new HashSet<long>(episodeTitleSequences.Select(y => y.InternalId).Distinct())
                                : new HashSet<long>(episodeTitleSequences.Where(y => y.HasIntro)
                                    .Select(y => y.InternalId)
                                    .Distinct());

                            var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                            if (!unmatched.Any())
                            {
                                Log.Info($"{season.Parent.Name} S: {season.IndexNumber} OK.");
                                continue;
                            }

                            Log.Info($"\n{season.Parent.Name} S: {season.IndexNumber} has {unmatched.Count()} episodes to scan...\n");

                            try
                            {
                                for (var index = 0; index <= unmatched.Count() - 1; index++)
                                {
                                    Log.Info(
                                        $"Checking Title Sequence data for {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber}");

                                    for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                                    {
                                        //Don't compare the same episode with itself.
                                        if (episodeQuery.Items[episodeComparableIndex].InternalId.Equals(unmatched[index].InternalId))
                                        {
                                            Log.Info(
                                                $"\n Can not compare {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} with itself MoveNext()\n");
                                            continue;
                                        }

                                        var comparableItem = episodeQuery.Items[episodeComparableIndex];
                                        var unmatchedItem  = unmatched[index];


                                        //This scan already match the unmatched item.
                                        if (episodeTitleSequences.Exists(e => e.InternalId == unmatchedItem.InternalId))
                                        {
                                            //This scan has already found an intro for the unmatched item
                                            if (episodeTitleSequences
                                                .FirstOrDefault(item => item.InternalId == unmatchedItem.InternalId)
                                                .HasIntro)
                                            {
                                                //This scan already match the comparable item.
                                                if (episodeTitleSequences.Exists(e =>
                                                    e.InternalId == comparableItem.InternalId))
                                                {
                                                    //This scan has already found an intro for the comparable item
                                                    if (episodeTitleSequences.FirstOrDefault(item =>
                                                        item.InternalId == comparableItem.InternalId).HasIntro)
                                                    {
                                                        Log.Info(
                                                            $"\n{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} OK" +
                                                            $"\n{episodeQuery.Items[episodeComparableIndex].Parent.Parent.Name} S: {episodeQuery.Items[episodeComparableIndex].Parent.IndexNumber} E: {episodeQuery.Items[episodeComparableIndex].IndexNumber} OK");
                                                        continue;
                                                    }
                                                }

                                            }
                                        }

                                        try
                                        {
                                            //The magic!
                                            var data = (IntroDetection.Instance.SearchAudioFingerPrint(
                                                episodeQuery.Items[episodeComparableIndex], unmatched[index]));

                                            foreach (var dataPoint in data)
                                            {

                                                //QuickScan is false
                                                //The item exists and was probably marked as  'HasIntro=false' last scan, but it now has a positive result.
                                                //Remove the old value and replace this new one

                                                //The user may have removed the false intro report in the configuration page while the scan was running.
                                                //This must be wrapped in a try catch to compensate for the possible missing/null object data.

                                                try
                                                {
                                                    episodeTitleSequences.RemoveAll(item => item.InternalId == dataPoint.InternalId);
                                                }
                                                catch { } //Either way the data is gone now.

                                                episodeTitleSequences.Add(dataPoint);

                                            }

                                            Log.Info("Episode Title Sequence Data obtained successfully.");
                                            titleSequence.EpisodeTitleSequences = episodeTitleSequences;
                                            IntroServerEntryPoint.Instance.SaveTitleSequenceJsonToFile(series.InternalId, season.InternalId, titleSequence);
                                            break; 

                                        }
                                        catch (InvalidIntroDetectionException ex)
                                        {
                                            Log.Info(ex.Message);

                                            if (episodeComparableIndex + 1 > episodeQuery.Items.Count() - 1)
                                            {
                                                //We have exhausted all our episode comparing
                                                Log.Info($"\n No title sequence has been found for {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber}.\n");
                                                
                                                //The title sequence data may already exist if there was a false match in a prior scan.
                                                var index1 = index;
                                                if (!titleSequence.EpisodeTitleSequences.Exists(item => item.InternalId == unmatched[index1].InternalId))
                                                {
                                                    episodeTitleSequences.Add(new EpisodeTitleSequence()
                                                    {
                                                        IndexNumber = episodeQuery.Items[index].IndexNumber,
                                                        HasIntro    = false,
                                                        InternalId  = episodeQuery.Items[index].InternalId
                                                    });

                                                    titleSequence.EpisodeTitleSequences = episodeTitleSequences;
                                                    IntroServerEntryPoint.Instance.SaveTitleSequenceJsonToFile(series.InternalId, season.InternalId, titleSequence);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Info($"Episode level exception: {ex.Message}");
                            }

                            RemoveAllPreviousSeasonEncodings(season.InternalId);

                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Info($"Season level exception: {ex.Message}");
                    }
                });
            }
            catch (AggregateException ex)
            {
                Log.Info($"Parallel Series level exception: {ex.Message}");
                Log.Info($"Parallel Series level inner exception: {ex.InnerExceptions[0].Message}"); //Might be none, might be many is it worth show all of them?
            }

            progress.Report(100.0);

        }

        //We could validate intro lengths here.
        private EpisodeTitleSequence ValidateTitleSequenceLength(EpisodeTitleSequence episodeTitleSequence, TimeSpan mode)
        {
            if ((episodeTitleSequence.IntroEnd - episodeTitleSequence.IntroStart) >= mode) return episodeTitleSequence;
            episodeTitleSequence.IntroEnd = episodeTitleSequence.IntroStart + mode;
            return episodeTitleSequence;

        }

        private void RemoveAllPreviousEncodings()
        {
            var configPath         = ApplicationPaths.PluginConfigurationsPath;
            var separator          = FileSystem.DirectorySeparatorChar;
            var introEncodingPath  = $"{configPath}{separator}IntroEncoding{separator}";
            var files              = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();
            
            if (!fileSystemMetadata.Any()) return;
            Log.Info("Removing all encoding files");
            foreach (var file in fileSystemMetadata)
            {
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }           
        }
       
        private void RemoveAllPreviousSeasonEncodings(long internalId)
        {
            var configPath         = ApplicationPaths.PluginConfigurationsPath;
            var separator          = FileSystem.DirectorySeparatorChar;
            var introEncodingPath  = $"{configPath}{separator}IntroEncoding{separator}";
            var files              = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();
            
            if (!fileSystemMetadata.Any()) return;
            
            foreach (var file in fileSystemMetadata)
            {
                if (file.Name.Substring(0, internalId.ToString().Length) != internalId.ToString()) continue;
                Log.Info($"Removing encoding file {file.FullName}");
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }           
        }

        private static double CalculateStep(int seriesTotalRecordCount)
        {
            var step = 100.0 / seriesTotalRecordCount;
            Log.Info($"Scheduled Task step: {step}");
            return step;
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }
        
        public string Name        => "Detect Episode Title Sequence";
        public string Key         => "Intro Skip Options";
        public string Description => "Detect start and finish times of episode title sequences to allow for a 'skip' option";
        public string Category    => "Detect Episode Title Sequence";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;

    }
}

