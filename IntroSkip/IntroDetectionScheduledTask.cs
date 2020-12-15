﻿using System;
using System.Collections.Generic;
using System.IO;
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
        private static ILogger Log                    { get; set; }
        private ILibraryManager LibraryManager        { get; }
        private IUserManager UserManager              { get; }
        private IFileSystem FileSystem                { get; }
        private IApplicationPaths ApplicationPaths    { get; }
        private IJsonSerializer JsonSerializer        { get; }
        public long CurrentSeriesEncodingInternalId   { get; set; }
       
        public static bool QuickScan                  { get; set; } = false;
        
        public IntroDetectionScheduledTask(ILogManager logManager, ILibraryManager libMan, IUserManager user, IFileSystem file, IApplicationPaths paths, IJsonSerializer jsonSerializer)
        {
            Log              = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager   = libMan;
            UserManager      = user;
            JsonSerializer   = jsonSerializer;
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

            ValidateSavedFingerprints();

            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            var step = 100.0 - (100.0 - 100.0 / seriesQuery.TotalRecordCount);
           
            Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = 2 }, series =>
            {
                  progress.Report(step += step);
                 
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

                      var titleSequence = IntroServerEntryPoint.Instance.GetTitleSequenceFromFile(series.InternalId, season.InternalId);


                      List<EpisodeTitleSequence> episodeTitleSequences = null;
                      episodeTitleSequences = titleSequence.EpisodeTitleSequences is null ? new List<EpisodeTitleSequence>() : titleSequence.EpisodeTitleSequences.Distinct().ToList();

                      var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                      {
                          Parent = season,
                          Recursive = true,
                          IncludeItemTypes = new[] { "Episode" },
                          User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                          IsVirtualItem = false

                      });


                    //QuickScan is set to true when a new episode item has been added to the library and only items that don't already have intro data need to be scanned,
                    //otherwise QuickScan is false, and will scan all items with no intro data, and also try an rescan any items which have been marked with "HasIntro=false"


                    var exceptIds = QuickScan
                        ? new HashSet<long>(episodeTitleSequences.Select(y => y.InternalId).Distinct())
                        : new HashSet<long>(episodeTitleSequences.Where(y => y.HasIntro).Select(y => y.InternalId).Distinct());


                    var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                    if (!unmatched.Any())
                      {
                          Log.Info($"{season.Parent.Name} S: {season.IndexNumber} OK.");
                          continue;
                      }

                      Log.Info($"\n{season.Parent.Name} S: {season.IndexNumber} has {unmatched.Count()} episodes to scan...\n");


                      for (var index = 0; index <= unmatched.Count() - 1; index++)
                      {

                          Log.Info($"Searching for: {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber}");

                          //Compare the unmatched baseItem with every other item ion the season.
                          for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                          {

                              var unmatchedItem  = unmatched[index];
                              var comparableItem = episodeQuery.Items[episodeComparableIndex];

                                //Don't compare the same episode with itself.
                                if (comparableItem.InternalId == unmatchedItem.InternalId)
                                {
                                    Log.Info($"\n Can not compare {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} with itself MoveNext()\n");
                                    continue;
                                }
                                
                                //If we have both titles sequences for both items move on
                                // ReSharper disable twice ComplexConditionExpression
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
                                    var data = (IntroDetection.Instance.SearchAudioFingerPrint(episodeQuery.Items[episodeComparableIndex], unmatched[index]));

                                    foreach (var dataPoint in data)
                                    {

                                        //QuickScan is false
                                        //The item exists and was probably marked as  'HasIntro=false' last scan, but it now has a positive result.
                                        //The scan wouldn't have made it this far if the item had title sequence data.
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
                                catch (InvalidTitleSequenceDetectionException ex)
                                {
                                    Log.Info(ex.Message);

                                    if (episodeComparableIndex + 1 > episodeQuery.Items.Count() - 1)
                                    {
                                        //We have exhausted all our episode comparing
                                        Log.Info($"\n {unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} currently has no title sequence.\n");

                                        //The title sequence data may already exist if there was a false match in a prior scan.

                                        if (!episodeTitleSequences.Exists(item => item.InternalId == unmatched[index].InternalId))
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


                      RemoveAllPreviousSeasonEncodings(season.InternalId);

                  }

            });

            progress.Report(100.0);
            RemoveAllPreviousEncodings();

        }


        private void ValidateSavedFingerprints()
        {
            // High multi-threading of the fingerprinting process may have some side effects.
            // If it is rushed on slower machines, durations will be shortened.
            // Durations 'must' equals 600secs for perfect results.
            // The finger print file may also be empty.
            // Remove these error files so they get rescanned.

            var separator        = FileSystem.DirectorySeparatorChar;
            var fingerprintFiles = FileSystem.GetFiles($"{IntroServerEntryPoint.Instance.FingerPrintDir}{separator}", true).Where(file => file.Extension == ".json").ToList();

            if (!fingerprintFiles.Any()) return;

            foreach (var file in fingerprintFiles)
            {
                var remove = false;
                using (var sr = new StreamReader(file.FullName))
                {
                    var json = sr.ReadToEnd();
                    if (string.IsNullOrEmpty(json))
                    {
                        remove = true;
                    }
                    else
                    {
                        var printData = JsonSerializer.DeserializeFromString<AudioFingerprint>(json);
                        if (printData.duration < 600.0)
                        {
                            remove = true;
                        }
                    }
                   
                }

                if (!remove) continue;
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }
            Log.Info("Fingerprint validation complete.");
        }

       
        private void RemoveAllPreviousEncodings()
        {
            var separator          = FileSystem.DirectorySeparatorChar;
            var introEncodingPath  = $"{IntroServerEntryPoint.Instance.EncodingDir}{separator}";
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
            var separator          = FileSystem.DirectorySeparatorChar;
            var introEncodingPath  = $"{IntroServerEntryPoint.Instance.EncodingDir}{separator}";
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

