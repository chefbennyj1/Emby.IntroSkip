using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Api;
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
        private static ILogger Log                  { get; set; }
        private ILibraryManager LibraryManager      { get; }
        private IUserManager UserManager            { get; }
        private IFileSystem FileSystem              { get; }
        private IApplicationPaths ApplicationPaths  { get; set; }
        public long CurrentSeriesEncodingInternalId { get; set; }
        private static double Step                  { get; set; }
        
        //public static Dictionary<string, IntroAudioFingerprint> AudioFingerPrints { get; private set; }
        
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user, IFileSystem file, IApplicationPaths paths)
        {
            Log            = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager    = user;
            FileSystem     = file;
            ApplicationPaths = paths;
        }
      
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Beginning Intro Task");
            var config = Plugin.Instance.Configuration;
            
            if (config.Intros is null)
            {
                config.Intros = new List<IntroDto>();
            }

            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            Step = CalculateStep(seriesQuery.TotalRecordCount, config);

            foreach (var series in seriesQuery.Items)
            {
                Step =+ 0.1;

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

                    RemoveAllPreviousSeasonEncodings();

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
                   
                    var index = 0;
                    
                    //All the episodes which haven't been matched.
                    var exceptIds = new HashSet<long>(config.Intros.Select(y => y.InternalId).Distinct());
                    var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();

                    for(index = 0; index <= unmatched.Count() -1; index++)
                    {
                        Log.Info($"No intro recorded for { unmatched[index].Parent.Parent.Name } S: {unmatched[index].Parent.IndexNumber} E: { unmatched[index].IndexNumber }");
                        
                        for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() -1; episodeComparableIndex ++)
                        {
                            Log.Info($" Comparing: \n{ unmatched[index].Parent.Parent.Name } S: {unmatched[index].Parent.IndexNumber} E: { unmatched[index].IndexNumber }" +
                                     $"\n{ episodeQuery.Items[episodeComparableIndex].Parent.Parent.Name } S: {episodeQuery.Items[episodeComparableIndex].Parent.IndexNumber} E: { episodeQuery.Items[episodeComparableIndex].IndexNumber }");
                           
                            //Don't compare the same episode with itself.
                            if (episodeQuery.Items[episodeComparableIndex].InternalId == unmatched[index].InternalId)
                            {
                                Log.Info($" Can not compare: \n{ unmatched[index].Parent.Parent.Name } S: {unmatched[index].Parent.IndexNumber} E: { unmatched[index].IndexNumber } with itself MoveNext()");
                                continue;
                            }

                            if (config.Intros.Exists(e => e.InternalId == unmatched[index].InternalId) &&
                                config.Intros.Exists(e => e.InternalId == episodeQuery.Items[episodeComparableIndex].InternalId)) continue;

                            try
                            {
                                var data = await Task.FromResult(IntroDetection.Instance.SearchAudioFingerPrint(episodeQuery.Items[episodeComparableIndex],unmatched[index]));

                                foreach (var dataPoint in data)
                                {
                                    if (!config.Intros.Exists(intro => intro.InternalId == dataPoint.InternalId))
                                    {
                                        config.Intros.Add(dataPoint);
                                    }
                                }

                                Plugin.Instance.UpdateConfiguration(config);
                            
                                Log.Info("Episode Intro Data obtained successfully.");

                            }
                            catch (InvalidIntroDetectionException ex)
                            {
                                Log.Info(ex.Message);

                                if (episodeComparableIndex + 1 > episodeQuery.Items.Count() - 1)
                                {
                                    //We have exhausted all our episode comparing
                                    Log.Info($"{ unmatched[index].Parent.Parent.Name } S: {unmatched[index].Parent.IndexNumber} E: { unmatched[index].IndexNumber } has no intro.");
                                    config.Intros.Add(new IntroDto()
                                    {
                                        HasIntro = false,
                                        SeriesInternalId = series.InternalId,
                                        InternalId = episodeQuery.Items[index].InternalId
                                    });
                                
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Info(ex.Message);
                            
                                //We missed a positive scan here. We will try again at another time.
                                //If this is first scan, there is no time to stop now! We're huge!
                                //We'll catch these the next time.
                            }
                        }
                    }
                }
            }
            progress.Report(100.0);
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

        private static double CalculateStep(int seriesTotalRecordCount, PluginConfiguration config)
        {
            
            var step =  100.0 / (seriesTotalRecordCount - config.Intros.GroupBy(savedIntros => savedIntros.SeriesInternalId).Count());
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

