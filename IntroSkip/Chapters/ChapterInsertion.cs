using System;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Sequence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;

namespace IntroSkip.Chapters
{
    public class ChapterInsertion : IServerEntryPoint
    {
        public static ChapterInsertion Instance            { get; set; }
        private ITaskManager TaskManager                   { get; }
        private ILogger Log                                { get; }
        private IItemRepository ItemRepository             { get; }
        private IProviderManager ProviderManager           { get; }
        private ILibraryManager LibraryManager             { get; }
        private readonly IFileSystem _fileSystem;

        public List<ChapterError> ChapterErrors = new List<ChapterError>();
        public List<ChapterInfo> PluginChapters { get; set; }
        public List<ChapterInfo> GetChapters { get; set; }


        public ChapterInsertion(ILogManager logManager, IItemRepository itemRepo, ILibraryManager libraryManager, ITaskManager taskManager, IProviderManager providerManager, IFileSystem fileSystem)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name+"-Chapters");
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            ProviderManager = providerManager;
            _fileSystem = fileSystem;
            ItemRepository = itemRepo;
            Instance = this;
        }
        

        public async Task InsertIntroChapters(long id, SequenceResult sequence)
        {
            //Log.Debug("CHAPTER INSERT: PASSED ID from TASK = {0}", id);
            ChapterErrors.Clear();

            try
            {
                //We have to add the try/catch here in case there is an "ID" but weirdly no item for it (CBERS issue)
                var item = LibraryManager.GetItemById(id);
                
                var tvShowName = item.Parent.Parent.Name;
                var seasonName = item.Parent.Name;
                var episodeNo = item.IndexNumber;

                const string introStartChapterName = "Intro";

                Log.Debug("TV Show: {0} - {1}", tvShowName, seasonName);
                Log.Debug("Getting Chapter Info for Episode {0}: {1}", episodeNo, item.Name);

                var config = Plugin.Instance.Configuration;

                GetChapters = ItemRepository.GetChapters(item);
                PluginChapters = new List<ChapterInfo>();

                if (config.EnableChapterInsertion)
                {
                    //Lets get the existing chapters and put them in a new list so we can insert the new Intro Chapter
                    foreach (var chap in GetChapters)
                    {
                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = chap.Name,
                            StartPositionTicks = chap.StartPositionTicks,
                            MarkerType = chap.MarkerType

                        });

                        Log.Debug("Existing Core Chapters: {0} Starts at {1} with MarkerType = {2}", chap.Name, ConvertTicksToTime(chap.StartPositionTicks), chap.MarkerType);
                        
                    }



                    try
                    {
                       
                        
                        
                        var chapterNames = PluginChapters.Select(c => c.Name).ToList();
                        var duplicatedChapters = chapterNames.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                        foreach (var chapterName in duplicatedChapters)
                        {
                            var chapter = PluginChapters.FirstOrDefault(c => c.Name == chapterName);
                            if (chapter != null)
                            {
                                Log.Debug("Duplicates Chapter Names Found: {0}", chapter.Name);
                                //PluginChapters.Remove(chapter);
                                //ItemRepository.SaveChapters(id, PluginChapters);
                            }
                        }
                        
                        var introEndList = PluginChapters.Where(x => x.MarkerType == MarkerType.IntroEnd).ToList();
                        if (introEndList.Count >= 2)
                        {
                            foreach (var chapter in introEndList)
                            {
                                Log.Debug("Duplicates Chapter Markers Found: {0}", chapter.MarkerType.ToString());
                                //PluginChapters.Remove(chapter);
                                //ItemRepository.SaveChapters(id, PluginChapters);
                                //Log.Info("Successfully Saved Chapters for {0} - {1} - {2}: {3}", tvShowName, seasonName, episodeNo, item.Name);
                            }

                            
                            /*Log.Warn("Refreshing Item: {0}", item.Name);


                            var options = new MetadataRefreshOptions(_fileSystem)
                            {
                                ReplaceAllMetadata = false,
                                //MetadataRefreshMode = MetadataRefreshMode.ValidationOnly,
                                ReplaceAllImages = false,
                                ForceSave = false,

                            };

                            ProviderManager.OnRefreshStart(item);

                            Log.Info("options: ForceSave= {0},ReplaceAllMetadata= {1}",
                                options.ForceSave.ToString(),
                                options.ReplaceAllMetadata.ToString());
                            await item.RefreshMetadata(options, CancellationToken.None);

                            Log.Warn("Refresh Completed");
                            Log.Warn("Refresh Single Item Completed successfully");

                            ProviderManager.OnRefreshComplete(item);

                            await RunMetadataScanTask();*/

                            //await RunThumbTask();

                            /*ChapterErrors.Add(new ChapterError()
                            {
                                Id = item.InternalId, //<-- use the internalId, they are shorter, less data to send to the UI
                                Date = DateTime.Now, //<-- Give them a date, so they know when this happened.
                                ChapterCount = enumerableDupesList.Count,
                                FilePathString = item.Path
                            });*/
                            
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("Duplicate Refresh Error: {0}", e);
                    }

                    //This wil check if the Intro start chapter point is already in the list
                    if (PluginChapters.Exists(chapterPoint => chapterPoint.Name == introStartChapterName) || PluginChapters.Exists(chapterPoint => chapterPoint.Name == "Title Sequence") || PluginChapters.Exists(coreChapter => coreChapter.MarkerType == MarkerType.IntroStart) && PluginChapters.Exists(coreChapter => coreChapter.MarkerType == MarkerType.CreditsStart))
                    {
                        Log.Info("Core already has the intro Start info and End Credit info - skipping Plugin Chapter Insertion");
                        return;
                    }

                    await RunChapterInsertionTask(id, sequence);
                }
            }
            catch
            {
                Log.Info("CHAPTER INSERTION ERROR: THIS ID DOES NOT CONTAIN ANY INFORMATION AND CANNOT BE PROCESSED {0}", id);
            }

        }
        
        private async Task RunChapterInsertionTask(long id, SequenceResult sequence)
        {
            var item = LibraryManager.GetItemById(id);

            var tvShowName = item.Parent.Parent.Name;
            var seasonName = item.Parent.Name;
            var episodeNo = item.IndexNumber;

            var titleSequenceStart = TimeSpan.FromTicks(sequence.TitleSequenceStart.Ticks);
            var titleSequenceEnd = TimeSpan.FromTicks(sequence.TitleSequenceEnd.Ticks);
            var creditSequenceStart = TimeSpan.FromTicks(sequence.CreditSequenceStart.Ticks);

            const string introStartChapterName = "Intro";
            const string introEndChapterName = "Intro End";
            const string endCreditChapterName = "End Credits";

            int lastIndex = PluginChapters.Count - 1;

            try
            {
                if (PluginChapters.Count == 0)
                {
                    Log.Warn("CHAPTER: {0} has no chapters available for sequence insertion.", item.Name);

                    ChapterErrors.Add(new ChapterError()
                    {
                        Id = item.InternalId, //<-- use the internalId, they are shorter, less data to send to the UI
                        Date = DateTime.Now, //<-- Give them a date, so they know when this happened.
                        ChapterCount = PluginChapters.Count,
                        FilePathString = item.Path
                    });

                }
                else
                {
                    if (sequence.HasCreditSequence && !PluginChapters.Exists(chapterPoint => chapterPoint.Name == endCreditChapterName))
                    {
                        Log.Debug("Adding End Credit Chapter Point for {0}: {1}, Episode{2}: {3} at {4}",
                            tvShowName, seasonName, episodeNo.ToString(), item.Name, (creditSequenceStart));

                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = endCreditChapterName,
                            StartPositionTicks = creditSequenceStart.Ticks,
                            MarkerType = MarkerType.Chapter
                        });
                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = "CreditStartMarker",
                            StartPositionTicks = creditSequenceStart.Ticks,
                            MarkerType = MarkerType.CreditsStart
                        });

                        PluginChapters.Sort(CompareStartTimes);

                    }

                    //add the entry and lets arrange the chapter list
                    //if the Title sequence DOESN'T start at 0, insert it
                    if (sequence.HasTitleSequence && titleSequenceStart != TimeSpan.Zero)
                    {
                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = introStartChapterName,
                            StartPositionTicks = titleSequenceStart.Ticks,
                            MarkerType = MarkerType.Chapter
                        });
                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = "IntroStartMarker",
                            StartPositionTicks = titleSequenceStart.Ticks,
                            MarkerType = MarkerType.IntroStart
                        });

                        PluginChapters.Sort(CompareStartTimes);
                    }

                    //if the Title Sequence DOES start at Zero then lets just remove chapter 1 and replace it with Title sequence.
                    if (sequence.HasTitleSequence && titleSequenceStart == TimeSpan.Zero)
                    {
                        PluginChapters.RemoveAt(0);

                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = introStartChapterName,
                            StartPositionTicks = titleSequenceStart.Ticks,
                            MarkerType = MarkerType.Chapter
                        });
                        PluginChapters.Add(new ChapterInfo
                        {
                            Name = "IntroStartMarker",
                            StartPositionTicks = titleSequenceStart.Ticks,
                            MarkerType = MarkerType.IntroStart
                        });
                        PluginChapters.Sort(CompareStartTimes);
                    }

                    //create new chapter entry point for the End of the Intro
                    int startIndex = PluginChapters.FindIndex(st => st.Name == introStartChapterName);

                    if (startIndex >= lastIndex)
                    {
                        //Create the new error - not really an error, but we'll call it that for now.
                        //The Error can consist of just one piece of data, the item InternalId. That is all we need to request further information about it later.
                        //This will keep memory low, and also a list of long integers will be faster to send to the UI
                        // We'll use the browsers engine (the UI) to request further data about the item if necessary.
                        ChapterErrors.Add(new ChapterError()
                        {
                            Id = item.InternalId, //<-- use the internalId, they are shorter, less data to send to the UI
                            Date = DateTime.Now, //<-- Give them a date, so they know when this happened.
                            ChapterCount = PluginChapters.Count,
                            FilePathString = item.Path,

                        });

                        Log.Warn("Not enough Chapter Markers to insert Title Sequence for {0}: {1}, Episode{2}: {3}",
                            tvShowName, seasonName, episodeNo.ToString(), item.Name);
                        Log.Warn("{0} has been added to Bad Chapter List", item.Name);
                    }

                    if (sequence.HasTitleSequence && startIndex < lastIndex)
                    {
                        ChapterInfo neededChapInfo = PluginChapters[startIndex + 2];
                        string chapName = neededChapInfo.Name;
                        //Log.Debug("CHAPTER: Organizing..... New Chapter name after Insert = {0}", chapName);
                        string newVal = introEndChapterName.Replace(introEndChapterName, chapName);

                        int changeStart = startIndex + 2;
                        PluginChapters.RemoveAt(changeStart);
                        ChapterInfo edit = new ChapterInfo
                        {
                            Name = newVal,
                            StartPositionTicks = titleSequenceEnd.Ticks,
                            MarkerType = MarkerType.IntroEnd

                        };
                        ChapterInfo edit2 = new ChapterInfo
                        {
                            Name = newVal,
                            StartPositionTicks = titleSequenceEnd.Ticks,
                            MarkerType = MarkerType.Chapter

                        };
                        //add the entry and lets arrange the chapter list
                        PluginChapters.Add(edit2);
                        PluginChapters.Add(edit);
                        PluginChapters.Sort(CompareStartTimes);
                    }

                    var cleanedChapters = PluginChapters.Distinct().ToList(); // <== This is will force a clean to remove any duplicate chapters.

                    ItemRepository.SaveChapters(id, cleanedChapters);
                    Log.Info("Successfully added Chapters for {0} - {1} - {2}: {3}", tvShowName, seasonName, episodeNo, item.Name);
                }
            }
            catch (Exception e)
            {
                Log.Error(e.Message, e);
                throw;
            }
        }

        private Task RunMetadataScanTask()
        {
            var scanMetadata = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Scan Metadata Folder");

            TaskManager.Execute(scanMetadata, new TaskOptions());

            return Task.FromResult(true);
        }

        private Task RunThumbTask()
        {
            var scanMetadata = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Thumbnail image extraction");

            TaskManager.Execute(scanMetadata, new TaskOptions());

            return Task.FromResult(true);
        }

        private static int CompareStartTimes(ChapterInfo tick1, ChapterInfo tick2)
        {
            return tick1.StartPositionTicks.CompareTo(tick2.StartPositionTicks);
        }

        private static string ConvertTicksToTime(long ticks)
        {
            TimeSpan time = TimeSpan.FromTicks(ticks);
            string output = time.ToString(@"hh\:mm\:ss");

            return output;
        }
        
        public void Dispose()
        {
            
        }

        public void Run()
        {
           
        }
    }

}