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
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;

// ReSharper disable once TooManyDependencies
// ReSharper disable three TooManyChainedReferences
// ReSharper disable once ExcessiveIndentation

namespace IntroSkip
{
    public class IntroDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log                  { get; set; }
        private ILibraryManager LibraryManager      { get; }
        private IUserManager UserManager            { get; }
        private IFileSystem FileSystem              { get; }
        private IApplicationPaths ApplicationPaths  { get; set; }
        private IJsonSerializer JsonSerializer      { get; }
        public long CurrentSeriesEncodingInternalId { get; set; }
        private static double Step                  { get; set; }
        
        //public static Dictionary<string, IntroAudioFingerprint> AudioFingerPrints { get; private set; }
        
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user, IFileSystem file, IApplicationPaths paths, IJsonSerializer jsonSerializer)
        {
            Log              = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager   = libMan;
            UserManager      = user;
            JsonSerializer   = jsonSerializer;
            FileSystem       = file;
            ApplicationPaths = paths;
        }
        
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Beginning Intro Task");
            progress.Report(0.1);

            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            Step = CalculateStep(seriesQuery.TotalRecordCount);

            foreach (var series in seriesQuery.Items)
            {
                if (string.IsNullOrEmpty(series.InternalId.ToString())) continue;

                Step =+ 0.01;
                progress.Report(Step);

                Log.Info(series.Name);
               
                var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    Parent           = series,
                    Recursive        = true,
                    IncludeItemTypes = new[] { "Season" },
                    User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                    IsVirtualItem    = false
                });

                foreach (var season in seasonQuery.Items)
                {
                    Step =+ 0.01;
                    progress.Report(Step);

                    RemoveAllPreviousSeasonEncodings();

                    Log.Info("File clean up complete");
                    
                    var titleSequence = IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(series.InternalId, season.InternalId);
                    
                    var episodeTitleSequences = titleSequence.EpisodeTitleSequences ?? new List<EpisodeTitleSequence>();
                    
                    //Only keep finger print data for an individual seasons
                    //AudioFingerPrints = new Dictionary<string, IntroAudioFingerprint>();

                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent           = season,
                        Recursive        = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem    = false

                    });
                    
                    //All the episodes which haven't been matched.
                    
                    var exceptIds = new HashSet<long>(episodeTitleSequences.Select(y => y.InternalId).Distinct());
                    var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();

                    if (!unmatched.Any())
                    {
                        Log.Info($"{season.Parent.Name} S: {season.IndexNumber} OK.");
                    }


                    for(var index = 0; index <= unmatched.Count() -1; index++)
                    {
                        Log.Info(
                            $"Checking Title Sequence recorded for {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber}");

                        for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                        {

                            //Don't compare the same episode with itself.
                            if (episodeQuery.Items[episodeComparableIndex].InternalId == unmatched[index].InternalId)
                            {
                                Log.Info(
                                    $" Can not compare {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} with itself MoveNext()");

                                continue;
                            }

                            var comparableIndex = episodeComparableIndex;
                            if (episodeTitleSequences.Exists(e => e.InternalId == unmatched[index].InternalId) &&
                                episodeTitleSequences.Exists(e => e.InternalId == episodeQuery.Items[comparableIndex].InternalId))
                            {
                                Log.Info($"\n{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} OK" +
                                    $"\n{episodeQuery.Items[episodeComparableIndex].Parent.Parent.Name} S: {episodeQuery.Items[episodeComparableIndex].Parent.IndexNumber} E: {episodeQuery.Items[episodeComparableIndex].IndexNumber} OK");
                                continue;
                            }

                            try
                            {
                                var data = await (IntroDetection.Instance.SearchAudioFingerPrint(episodeQuery.Items[episodeComparableIndex], unmatched[index]));

                                foreach (var dataPoint in data)
                                {
                                    if (!episodeTitleSequences.Exists(intro => intro.InternalId == dataPoint.InternalId))
                                    {
                                        episodeTitleSequences.Add(dataPoint);
                                    }
                                }

                                titleSequence.EpisodeTitleSequences = episodeTitleSequences;
                    
                                IntroServerEntryPoint.Instance.SaveTitleSequenceJsonToFile(series.InternalId, season.InternalId, titleSequence);
                                //Plugin.Instance.UpdateConfiguration(config);
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
                                    episodeTitleSequences.Add(new EpisodeTitleSequence()
                                    {
                                        IndexNumber = episodeQuery.Items[index].IndexNumber,
                                        HasIntro = false,
                                        InternalId = episodeQuery.Items[index].InternalId
                                    });

                                    titleSequence.EpisodeTitleSequences = episodeTitleSequences;
                                    IntroServerEntryPoint.Instance.SaveTitleSequenceJsonToFile(series.InternalId,
                                        season.InternalId, titleSequence);

                                }
                            }
                            //catch (Exception ex)
                            //{
                            //    Log.Info(ex.Message);

                            //    //We missed a positive scan here. We will try again at another time.
                            //    //If this is first scan, there is no time to stop now! We're huge!
                            //    //We'll catch these the next time.
                            //}
                        }
                    }

                    
                }
            }
            progress.Report(100.0);
        }


        private EpisodeTitleSequence ValidateTitleSequenceLength(EpisodeTitleSequence episodeTitleSequence, TimeSpan mode)
        {
            if ((episodeTitleSequence.IntroEnd - episodeTitleSequence.IntroStart) >= mode) return episodeTitleSequence;
            episodeTitleSequence.IntroEnd = episodeTitleSequence.IntroStart + mode;
            return episodeTitleSequence;

        }

        private TimeSpan CalculateTitleSequenceMode(List<EpisodeTitleSequence> sequences)
        {
            var groups   = sequences.GroupBy(sequence => sequence.IntroEnd - sequence.IntroStart);
            int maxCount = groups.Max(g => g.Count());
            var mode     = groups.First(g => g.Count() == maxCount).Key;
            return mode;
        }

        private void RemoveAllPreviousSeasonEncodings()
        {
            var introEncodingPath = ApplicationPaths.PluginConfigurationsPath + FileSystem.DirectorySeparatorChar + "IntroEncoding" + FileSystem.DirectorySeparatorChar;
            
            var files = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            if (!files.Any()) return;
            
            foreach (var file in files)
            {
                Log.Info($"Removing encoding file {file.FullName}");
                FileSystem.DeleteFile(file.FullName);
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

