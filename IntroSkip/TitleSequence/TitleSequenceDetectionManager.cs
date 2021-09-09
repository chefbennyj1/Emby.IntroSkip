using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Data;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Data;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;

// ReSharper disable TooManyChainedReferences

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceDetectionManager : IServerEntryPoint
    {
        private static ILogger Log                           { get; set; }
        private ILibraryManager LibraryManager               { get; }
        private IUserManager UserManager                     { get; }       
        public static TitleSequenceDetectionManager Instance { get; private set; }

       
        public TitleSequenceDetectionManager(ILogManager logManager, ILibraryManager libMan, IUserManager user)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libMan;
            UserManager = user;
            Instance = this;
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, long[] seriesInternalIds, ITitleSequenceRepository repo)
        {
            var config = Plugin.Instance.Configuration;

            var seriesInternalItemQuery = new InternalItemsQuery()
            {
                Recursive        = true,
                ItemIds          = seriesInternalIds,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                OrderBy          = new[] { ItemSortBy.SortName }.Select(i => new ValueTuple<string, SortOrder>(i, SortOrder.Descending)).ToArray()
            };

            if (config.Limit.HasValue)
            {
                seriesInternalItemQuery.Limit = config.Limit.Value;
            }

            var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);


            Analyze(seriesQuery, progress, repo, cancellationToken);
        }

        public void Analyze(CancellationToken cancellationToken, IProgress<double> progress, ITitleSequenceRepository repo)
        {      
            
            var config = Plugin.Instance.Configuration;
            var seriesInternalItemQuery = new InternalItemsQuery()
            {
                Recursive        = true,
                IncludeItemTypes = new[] { "Series" },
                User             = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                OrderBy          = new[] { ItemSortBy.SortName }.Select(i => new ValueTuple<string, SortOrder>(i, SortOrder.Descending)).ToArray()
            };

            if (config.Limit.HasValue)
            {
                seriesInternalItemQuery.Limit = config.Limit.Value;
            }

            var seriesQuery = LibraryManager.QueryItems(seriesInternalItemQuery);
            

            Analyze(seriesQuery, progress, repo, cancellationToken);
        }

        // ReSharper disable once ExcessiveIndentation
        // ReSharper disable once TooManyArguments
      
        private void Analyze(QueryResult<BaseItem> seriesQuery, IProgress<double> progress, ITitleSequenceRepository repo, CancellationToken cancellationToken)
        {
            
            if (cancellationToken.IsCancellationRequested)
            {
                
                progress.Report(100.0);
            }    
            var config = Plugin.Instance.Configuration;
            var currentProgress = 0.2;
            var step = 100.0 / seriesQuery.TotalRecordCount;
            
            //Our database info
            QueryResult<TitleSequenceResult> dbResults = null;
            try
            {
                dbResults = repo.GetResults(new TitleSequenceResultQuery());
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
                
                progress.Report(100.0);
                return;
            }

            Parallel.ForEach(seriesQuery.Items, new ParallelOptions() { MaxDegreeOfParallelism = config.MaxDegreeOfParallelism }, (series, state) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    
                    state.Break();
                    progress.Report(100.0);
                }

                progress?.Report((currentProgress += step) - 1);    
                                            

                var seasonQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                {
                    Parent = series,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Season" },
                    User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                    IsVirtualItem = false

                });               

                foreach (var season in seasonQuery.Items)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }                                        

                    var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
                    {
                        Parent = season,
                        Recursive = true,
                        IncludeItemTypes = new[] { "Episode" },
                        User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                        IsVirtualItem = false
                    });
                                       
                    
                    var dbEpisodes = dbResults.Items.Where(t => t.SeasonId == season.InternalId).ToList();

                    if(!dbEpisodes.Any()) //<-- this should not happen unless the fingerprint task was never run on this season. 
                    {
                        dbEpisodes = new List<TitleSequenceResult>();                        
                    }

                    // An entire season with no title sequence might be the case. However, something else might have caused an entire season to have no results. - Warn the log.
                    if (dbEpisodes.All(item => item.HasSequence == false) && dbEpisodes.All(item => item.Processed)) 
                    {
                        Log.Warn($"{series.Name} {season.Name}: There currently are no title sequences available for this season.\n");
                    }


                    //After processing, the DB entry is marked as 'processed'. if the item has been pocessed already, just move on.
                    if (dbEpisodes.Count() == episodeQuery.TotalRecordCount && dbEpisodes.All(item => item.Processed))
                    { 
                        
                        Log.Info($"{series.Name} S:{season.IndexNumber} have no new episodes to scan.");
                        continue;
                    }
                                 

                    // All our processed episodes with sequences, or user confirmed information
                    var exceptIds = new HashSet<long>(dbEpisodes.Where(e => e.HasSequence || e.Confirmed).Select(y => y.InternalId).Distinct());
                    // A list of episodes with all our episodes containg sequence data removed from it. All that is left is what we need to process.
                    var unmatched = episodeQuery.Items.Where(x => !exceptIds.Contains(x.InternalId)).ToList();


                    if (!unmatched.Any()) //<-- this is redundant because we checked for 'processed' above. keep it here just incase something slips past.
                    {
                        continue;
                    }

                    Log.Info($" will process {unmatched.Count()} episodes for {season.Parent.Name} - {season.Name}.");

                    for (var index = 0; index <= unmatched.Count() - 1; index++)
                    {
                        
                        if (cancellationToken.IsCancellationRequested)
                        {                            
                            break;
                        }
                        
                        //Compare the unmatched episode  with every other episode in the season untill there is a match.
                        for (var episodeComparableIndex = 0; episodeComparableIndex <= episodeQuery.Items.Count() - 1; episodeComparableIndex++)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                break;
                            }

                            
                            var unmatchedItem = unmatched[index];
                            var comparableItem = episodeQuery.Items[episodeComparableIndex];

                            //Don't compare the same episode with itself. The episodes must be different or we'll match the entire encoding.
                            if (comparableItem.InternalId == unmatchedItem.InternalId)
                            {
                                continue;
                            }

                            // If we have valid title sequence data for both items move on
                            if (dbEpisodes.Any(item => item.InternalId == unmatchedItem.InternalId) && dbEpisodes.Any(item => item.InternalId == comparableItem.InternalId))
                            {
                                if (dbEpisodes.FirstOrDefault(i => i.InternalId == unmatchedItem.InternalId).HasSequence && dbEpisodes.FirstOrDefault(i => i.InternalId == comparableItem.InternalId).HasSequence)
                                {
                                    continue;
                                }
                            }
                            
                            try
                            {
                                
                                // The magic!
                                var titleSequenceDetection = TitleSequenceDetection.Instance;                        
                               
                                var stopWatch = new Stopwatch();
                                stopWatch.Start();

                                var sequences = titleSequenceDetection.DetectTitleSequence(episodeQuery.Items[episodeComparableIndex], unmatched[index], dbResults);
                                
                                foreach (var sequence in sequences)
                                {
                                    //Just remove these entries in the episode list (if they exist) and add the new result back. Easier!
                                    if (dbEpisodes.Exists(item => item.IndexNumber == sequence.IndexNumber && item.SeasonId == sequence.SeasonId))
                                    {
                                        dbEpisodes.RemoveAll(item => item.IndexNumber == sequence.IndexNumber && item.SeasonId == sequence.SeasonId);
                                    }

                                    dbEpisodes.Add(sequence);
                                    
                                }

                                stopWatch.Stop();
                                
                                
                                var unmatchedBaseItem = episodeQuery.Items.FirstOrDefault(item => item.InternalId == unmatched[index].InternalId); //<-- For logging purpose only. Should be removed later.
                                Log.Info($"{unmatchedBaseItem.Parent.Parent.Name} - {unmatchedBaseItem.Parent.Name} - Episode {unmatched[index].IndexNumber} detection took {stopWatch.ElapsedMilliseconds /1000} seconds.");
                                
                            }
                            catch (TitleSequenceInvalidDetectionException)
                            {                                
                                //Keep going!
                                if (episodeComparableIndex != episodeQuery.Items.Count() - 1) continue;

                                //We have exhausted all our episode comparing
                                if (dbEpisodes.Exists(item => item.InternalId == unmatchedItem.InternalId)) continue;

                                Log.Info($"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} currently has no title sequence."); //<-- we never get this log entry??

                            }
                            catch (AudioFingerprintMissingException ex)
                            {                               
                                Log.Info($"{unmatched[index].Parent.Parent.Name} S: {unmatched[index].Parent.IndexNumber} E: {unmatched[index].IndexNumber} {ex.Message}");
                            }                            

                        }
                    }

                    foreach(var episode in dbEpisodes)
                    {
                        episode.Processed = true; //<-- now we won't process episodes again over and over

                        repo.SaveResult(episode, cancellationToken);
                        
                        var found = LibraryManager.GetItemById(episode.InternalId); //<-- This will take up time, and could be removed later
                        Log.Info($"{found.Parent.Parent.Name} S: {found.Parent.IndexNumber} E: {found.IndexNumber} title sequence successful.");

                        dbResults = repo.GetResults(new TitleSequenceResultQuery());
                    }            
                    
                    Clean(dbResults.Items.Where(item => item.SeasonId == season.InternalId).ToList(), season, repo, cancellationToken);

                }

            });
            Log.Info("TitleSequence task complete");
            progress.Report(100.0);

        }
        
        private bool IsComplete(BaseItem season)
        {
            var episodeQuery = LibraryManager.GetItemsResult(new InternalItemsQuery()
            { 
                Parent = season,
                IsVirtualItem = true,
                IncludeItemTypes = new[] { "Episode" },
                Recursive = true
                
            });

            if(episodeQuery.Items.Any(item => item.IsVirtualItem || item.IsUnaired)) { return false; }

            return true;
        }

        private void Clean(List<TitleSequenceResult> dbEpisodes, BaseItem season, ITitleSequenceRepository repo, CancellationToken cancellationToken)
        {

            // The DB file gets really big with all the finger print data. If we can remove some, do it here.
            if (dbEpisodes.All(result => result.HasSequence || result.Confirmed))
            {
                if (IsComplete(season))
                {
                    //Remove the fingerprint data for these episodes. The db will be vacuumed at the end of this task.
                    foreach (var result in dbEpisodes)
                    {
                        try
                        {
                            if (!(result.Fingerprint is null))
                            {
                                result.Fingerprint.Clear();                  //Empty fingerprint List                                                                                             
                                repo.SaveResult(result, cancellationToken);  //Save it back to the db
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(ex.Message);
                        }
                    }
                }

                repo.Vacuum();
            }
        }
              

        public void Dispose()
        {

        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {

        }
    }
}
