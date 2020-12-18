using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

// ReSharper disable once TooManyDependencies
// ReSharper disable three TooManyChainedReferences
// ReSharper disable once ExcessiveIndentation
// ReSharper disable twice ComplexConditionExpression

namespace IntroSkip
{
    public class TitleSequenceDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log                  { get; set; }
        private ILibraryManager LibraryManager      { get; }
        private IUserManager UserManager            { get; }
        public long CurrentSeriesEncodingInternalId { get; set; }


        public TitleSequenceDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user)
        {
            Log              = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager   = libMan;
            UserManager      = user;
        }

#pragma warning disable 1998
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#pragma warning restore 1998
        {
            Log.Info("Beginning Title Sequence Task");
            var config = Plugin.Instance.Configuration;
            
            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            var step            = 100.0 / seriesQuery.TotalRecordCount;
            var currentProgress = 0.0;

            Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, series =>
            {
                progress.Report((currentProgress += step) - 1);

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

                    var titleSequence = FileManager.Instance.GetTitleSequenceFromFile($"{series.InternalId}{season.InternalId}");

                    List<EpisodeTitleSequence> episodeTitleSequences = null;
                    episodeTitleSequences = titleSequence.EpisodeTitleSequences is null ? new List<EpisodeTitleSequence>() : titleSequence.EpisodeTitleSequences.ToList();

                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent           = season,
                        Recursive        = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem    = false

                    });


                    var exceptIds = new HashSet<long>(episodeTitleSequences.Select(y => y.InternalId).Distinct());

                    var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();

                    if (!unmatched.Any())
                    {
                        Log.Info($"{season.Parent.Name} S: {season.IndexNumber} OK.");
                        continue;
                    }

                    Log.Info($"{season.Parent.Name} S: {season.IndexNumber} has {unmatched.Count()} episodes to scan...\n");
                    

                    for (var index = 0; index <= unmatched.Count() - 1; index++)
                    {
                       
                        //Compare the unmatched baseItem with every other item ion the season.
                        for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                        {
                            var unmatchedItem  = unmatched[index];
                            var comparableItem = episodeQuery.Items[episodeComparableIndex];

                            //Don't compare the same episode with itself.
                            if (comparableItem.InternalId == unmatchedItem.InternalId)
                            {
                               
                                ////We can't compare an episode with itself, however it is the final episode in the list, and it doesn't already exist, save the false intro
                                if (episodeComparableIndex == episodeQuery.Items.Count() - 1)
                                {
                                    if (!episodeTitleSequences.Exists(item => item.InternalId == unmatchedItem.InternalId))
                                    {
                                        episodeTitleSequences.Add(new EpisodeTitleSequence()
                                        {
                                            IndexNumber = episodeQuery.Items[episodeComparableIndex].IndexNumber,
                                            HasIntro    = false,
                                            InternalId  = episodeQuery.Items[episodeComparableIndex].InternalId
                                        });

                                        break;
                                    }
                                }
                                continue;
                            }

                            //If we have valid titles sequences data for both items move on
                            if (episodeTitleSequences.Any(item => item.InternalId == unmatchedItem.InternalId) && episodeTitleSequences.Any(item => item.InternalId == comparableItem.InternalId))
                            {
                                if (episodeTitleSequences.FirstOrDefault(i => i.InternalId == unmatchedItem.InternalId).HasIntro && episodeTitleSequences.FirstOrDefault(i => i.InternalId == comparableItem.InternalId).HasIntro)
                                {
                                    continue;
                                }
                            }

                            
                            try
                            {
                                //The magic!
                                var data = (TitleSequenceDetection.Instance.SearchAudioFingerPrint(episodeQuery.Items[episodeComparableIndex], unmatched[index]));

                                foreach (var dataPoint in data)
                                {
                                    if (episodeTitleSequences.Exists(item => item.IndexNumber == dataPoint.IndexNumber))
                                    {
                                        episodeTitleSequences.RemoveAll(item => item.IndexNumber == dataPoint.IndexNumber);
                                    }

                                    episodeTitleSequences.Add(dataPoint);
                                    var found = episodeQuery.Items.FirstOrDefault(item => item.InternalId == dataPoint.InternalId);
                                    Log.Info($"{found.Parent.Parent.Name} S: {found.Parent.IndexNumber} E: {found.IndexNumber} title sequence obtained successfully.");
                                }

                            }
                            catch (TitleSequenceInvalidDetectionException)
                            {
                                if (episodeComparableIndex != episodeQuery.Items.Count() - 1) continue;

                                //We have exhausted all our episode comparing
                                
                                if (episodeTitleSequences.Exists(item => item.InternalId == unmatchedItem.InternalId)) continue;

                                Log.Info($"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} currently has no title sequence.");

                                episodeTitleSequences.Add(new EpisodeTitleSequence()
                                {
                                    IndexNumber = unmatchedItem.IndexNumber,
                                    HasIntro    = false,
                                    InternalId  = unmatchedItem.InternalId
                                });
                            }
                            catch (AudioFingerprintMissingException ex)
                            {
                                Log.Info($"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} {ex}\n ID: {unmatched[index].Parent.InternalId}{unmatched[index].InternalId}");
                            }
                        }

                    }

                    titleSequence.EpisodeTitleSequences = episodeTitleSequences;
                    FileManager.Instance.SaveTitleSequenceJsonToFile($"{series.InternalId}{season.InternalId}", titleSequence);

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
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public string Name        => "Detect Episode Title Sequence";
        public string Key         => "Intro Skip Options";
        public string Description => "Detect start and finish times of episode title sequences to allow for a 'skip' option";
        public string Category    => "Intro Skip";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;

    }
}

