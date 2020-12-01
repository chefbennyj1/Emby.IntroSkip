using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Api;
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
        public long CurrentSeriesEncodingInternalId { get; set; }
        private static double Step                  { get; set; }
    
        
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user, IFileSystem file)
        {
            Log            = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager    = user;
            FileSystem     = file;
            Step           = CalculateStep();
        }
      
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Info("Beginning Intro Task");
            var config = Plugin.Instance.Configuration;
            
            IntroFileDirectory.Instance.MaintainIntroEncodingDirectory();

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

            foreach (var series in seriesQuery.Items)
            {
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
                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent           = season,
                        Recursive        = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem    = false

                    });

                    var episodeComparableIndex = 1;
                    var episodeToCompareIndex  = 0;

                    for(episodeToCompareIndex = 0; episodeToCompareIndex <= episodeQuery.Items.Count() - 1; episodeToCompareIndex++)
                    {
                        Step =+ Step;
                        progress.Report(Step);

                        if (config.Intros.Exists(e => e.InternalId == episodeQuery.Items[episodeToCompareIndex].InternalId)) continue;
                        
                        //Don't compare the same episode with it's self
                        if (episodeToCompareIndex == episodeComparableIndex) episodeComparableIndex++;
                        
                        try
                        {
                            var data = await Task.FromResult(
                                IntroDetection.Instance.SearchAudioFingerPrint(
                                    episodeQuery.Items[episodeComparableIndex],
                                    episodeQuery.Items[episodeToCompareIndex]));

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
                            Log.Info(ex.InnerException?.Message);

                            if (episodeComparableIndex <= episodeQuery.Items.Count() - 2)
                            {
                                episodeComparableIndex++;
                                episodeToCompareIndex--;
                            }
                            else
                            {
                                config.Intros.Add(new IntroDto()
                                {
                                    HasIntro = false,
                                    SeriesInternalId = series.InternalId,
                                    InternalId = episodeQuery.Items[episodeToCompareIndex].InternalId
                                });

                                episodeToCompareIndex = 1;
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
            progress.Report(100.0);
        }

        private double CalculateStep()
        {
            var episodeQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                IncludeItemTypes = new[] {"Episode"},
                IsVirtualItem    = false,
                Recursive        = true
            });

            return 100.0 / episodeQuery.TotalRecordCount;
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

