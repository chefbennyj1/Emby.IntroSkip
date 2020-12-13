using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user,
            IFileSystem file, IApplicationPaths paths) //, IJsonSerializer jsonSerializer)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager = user;
            //JsonSerializer   = jsonSerializer;
            FileSystem = file;
            ApplicationPaths = paths;
        }

        private static ILogger Log { get; set; }
        private ILibraryManager LibraryManager { get; }
        private IUserManager UserManager { get; }
        private IFileSystem FileSystem { get; }

        private IApplicationPaths ApplicationPaths { get; }

        //private IJsonSerializer JsonSerializer      { get; }
        public long CurrentSeriesEncodingInternalId { get; set; }
        private static double Step { get; set; }
        public bool IsHidden => false;
        public bool IsEnabled => true;
        public bool IsLogged => true;

#pragma warning disable 1998
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#pragma warning restore 1998
        {
            Log.Info("Beginning Intro Task");

            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery
            {
                Recursive = true,
                IncludeItemTypes = new[] {"Series"},
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            Step = CalculateStep(seriesQuery.TotalRecordCount);
            progress.Report(Step);
            var maxDegreeOfParallelism = 2; //ToDo: Config option?
            Parallel.ForEach(seriesQuery.Items, new ParallelOptions {MaxDegreeOfParallelism = maxDegreeOfParallelism},
                series =>
                {
                    //if (string.IsNullOrEmpty(series.InternalId.ToString())) continue;

                    Step += Step;
                    progress.Report(Step);

                    Log.Info(series.Name);

                    var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery
                    {
                        Parent = series,
                        Recursive = true,
                        IncludeItemTypes = new[] {"Season"},
                        User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem = false
                    });

                    foreach (var season in seasonQuery.Items)
                    {
                        Log.Info("File clean up complete");

                        var titleSequence =
                            IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(series.InternalId,
                                season.InternalId);

                        var episodeTitleSequences =
                            titleSequence.EpisodeTitleSequences ?? new List<EpisodeTitleSequence>();


                        var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery
                        {
                            Parent = season,
                            Recursive = true,
                            IncludeItemTypes = new[] {"Episode"},
                            User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                            IsVirtualItem = false
                        });

                        //All the episodes which haven't been matched.
                        var exceptIds = new HashSet<long>(episodeTitleSequences.Select(y => y.InternalId)
                            .Distinct());
                        var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId))
                            .ToList();

                        //TODO: Add HasIntro "false" to unmatched - as a secondary check.

                        if (!unmatched.Any()) Log.Info($"{season.Parent.Name} S: {season.IndexNumber} OK.");


                        for (var index = 0; index <= unmatched.Count() - 1; index++)
                        {
                            Log.Info(
                                $"Checking Title Sequence recorded for {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber}");

                            for (var episodeComparableIndex = 0;
                                episodeComparableIndex <= episodeQuery.Items.Count() - 1;
                                episodeComparableIndex++)
                            {
                                //Don't compare the same episode with itself.
                                if (episodeQuery.Items[episodeComparableIndex]
                                    .InternalId == unmatched[index]
                                    .InternalId)
                                {
                                    Log.Info(
                                        $" Can not compare {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} with itself MoveNext()");
                                    continue;
                                }

                                var comparableItem = episodeQuery.Items[episodeComparableIndex];
                                var unmatchedItem = unmatched[index];
                                if (episodeTitleSequences.Exists(e => e.InternalId == unmatchedItem.InternalId))
                                    if (episodeTitleSequences.Exists(e => e.InternalId == comparableItem.InternalId))
                                    {
                                        Log.Info(
                                            $"\n{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} OK" +
                                            $"\n{episodeQuery.Items[episodeComparableIndex].Parent.Parent.Name} S: {episodeQuery.Items[episodeComparableIndex].Parent.IndexNumber} E: {episodeQuery.Items[episodeComparableIndex].IndexNumber} OK");
                                        continue;
                                    }

                                try
                                {
                                    var data = IntroDetection.Instance.SearchAudioFingerPrint(
                                        episodeQuery.Items[episodeComparableIndex], unmatched[index]);

                                    foreach (var dataPoint in data)
                                        if (!episodeTitleSequences.Exists(intro =>
                                            intro.InternalId == dataPoint.InternalId))
                                            episodeTitleSequences.Add(dataPoint);

                                    Log.Info("Episode Intro Data obtained successfully.");
                                    episodeComparableIndex = episodeQuery.Items.Count() - 1; //Exit out of this loop
                                }
                                catch (InvalidIntroDetectionException ex)
                                {
                                    Log.Info(ex.Message);

                                    if (episodeComparableIndex + 1 > episodeQuery.Items.Count() - 1)
                                    {
                                        //We have exhausted all our episode comparing
                                        Log.Info(
                                            $"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} has no intro.");

                                        episodeTitleSequences.Add(new EpisodeTitleSequence
                                        {
                                            IndexNumber = episodeQuery.Items[index]
                                                .IndexNumber,
                                            HasIntro = false,
                                            InternalId = episodeQuery.Items[index]
                                                .InternalId
                                        });
                                    }
                                }
                            }
                        }

                        RemoveAllPreviousSeasonEncodings(season.InternalId);

                        titleSequence.EpisodeTitleSequences = episodeTitleSequences;
                        IntroServerEntryPoint.Instance.SaveTitleSequenceJsonToFile(series.InternalId, season.InternalId,
                            titleSequence);
                    }
                });
            progress.Report(100.0);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24)
                        .Ticks
                }
            };
        }

        public string Name => "Detect Episode Title Sequence";
        public string Key => "Intro Skip Options";

        public string Description =>
            "Detect start and finish times of episode title sequences to allow for a 'skip' option";

        public string Category => "Detect Episode Title Sequence";

        //We could validate intro lengths here.
        // ReSharper disable once UnusedMember.Local
        private EpisodeTitleSequence ValidateTitleSequenceLength(EpisodeTitleSequence episodeTitleSequence,
            TimeSpan mode)
        {
            if (episodeTitleSequence.IntroEnd - episodeTitleSequence.IntroStart >= mode) return episodeTitleSequence;
            episodeTitleSequence.IntroEnd = episodeTitleSequence.IntroStart + mode;
            return episodeTitleSequence;
        }


        private void RemoveAllPreviousSeasonEncodings(long internalId)
        {
            var configPath = ApplicationPaths.PluginConfigurationsPath;
            var separator = FileSystem.DirectorySeparatorChar;
            var introEncodingPath = $"{configPath}{separator}IntroEncoding{separator}";
            var files = FileSystem.GetFiles(introEncodingPath, true)
                .Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();

            if (!fileSystemMetadata.Any()) return;

            foreach (var file in fileSystemMetadata)
            {
                if (file.Name.Substring(0, internalId.ToString()
                    .Length) != internalId.ToString())
                    continue;
                Log.Info($"Removing encoding file {file.FullName}");
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static double CalculateStep(int seriesTotalRecordCount)
        {
            var step = 100.0 / seriesTotalRecordCount;
            Log.Info($"Scheduled Task step: {step}");
            return step;
        }
    }
}