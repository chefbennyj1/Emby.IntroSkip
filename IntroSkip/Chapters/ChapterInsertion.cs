using System;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
using IntroSkip.Sequence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;

namespace IntroSkip.Chapters
{
    public class ChapterInsertion : IServerEntryPoint
    {
        public static ChapterInsertion Instance { get; set; }

        private ILogger Log { get; set; }

        private IItemRepository ItemRepository { get; set; }

        private ILibraryManager LibraryManager { get; }

        public List<ChapterError> ChapterErrors = new List<ChapterError>();

        public ChapterInsertion(ILogManager logManager, IItemRepository itemRepo, ILibraryManager libraryManager)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            LibraryManager = libraryManager;
            ItemRepository = itemRepo;
            Instance = this;
        }

        public void InsertIntroChapters(long id, SequenceResult sequence)
        {
            Log.Debug("CHAPTER INSERT: PASSED ID from TASK = {0}", id);

            try
            {
                //We have to add the try/catch here incase there is an "ID" but weirdly no item for it (CBERS issue)
                var item = LibraryManager.GetItemById(id);
                
                var tvShowName = item.Parent.Parent.Name;
                var seasonName = item.Parent.Name;
                var episodeNo = item.IndexNumber;

                Log.Info("CHAPTER INSERT: TV Show: {0} - {1}", tvShowName, seasonName);
                Log.Debug("CHAPTER INSERT: Getting Chapter Info for {0}: {1}", episodeNo, item.Name);

                var config = Plugin.Instance.Configuration;

                List<ChapterInfo> getChapters = ItemRepository.GetChapters(item);
                List<ChapterInfo> chapters = new List<ChapterInfo>();

                if (config.EnableChapterInsertion)
                {
                    //Lets get the existing chapters and put them in a new list so we can insert the new Intro Chapter
                    foreach (var chap in getChapters)
                    {
                        chapters.Add(new ChapterInfo
                        {
                            Name = chap.Name,
                            StartPositionTicks = chap.StartPositionTicks,
                        });
                        Log.Debug("CHAPTER INSERT: Fetch Existing Chapters: {0} Starts at {1}", chap.Name,
                            ConvertTicksToTime(chap.StartPositionTicks));
                    }

                    long insertStart = sequence.TitleSequenceStart.Ticks;
                    long insertEnd = sequence.TitleSequenceEnd.Ticks;
                    long EndCredStart = sequence.CreditSequenceStart.Ticks;

                    string introStartString = "Title Sequence";
                    string introEndString = "Intro End";
                    string endCredString = "End Credits";

                    int iCount = chapters.Count;
                    int lastIndex = iCount - 1;

                    //This wil check if the Introstart chapter point is already in the list,
                    if (chapters.Exists(chapterPoint => chapterPoint.Name == introStartString))
                    {
                        Log.Debug("CHAPTER INSERT: Title Sequence Chapter already Added for {0}", item.Name);
                    }
                    else if(chapters.Exists(chapterPoint => chapterPoint.Name == endCredString))
                    {
                        Log.Debug("CHAPTER INSERT: End Credit Chapter already Added for {0}", item.Name);
                    }

                    else
                    {
                        try
                        {
                            if (iCount == 0)
                            {
                                Log.Warn(
                                    "CHAPTER INSERT: {0} has NO chapters, so can't insert - Added To Bad Chapter List",
                                    item.Name);
                                ChapterErrors.Add(new ChapterError()
                                {
                                    Id = item
                                        .InternalId, //<-- use the internalId, they are shorter, less data to send to the UI
                                    Date = DateTime.Now, //<-- Give them a date, so they know when this happened.
                                    ChapterCount = iCount,
                                    FilePathString = item.Path
                                });

                            }
                            else 
                            {
                                //add the entry and lets arrange the chapter list
                                //if the Title sequence doesn't start at 0, insert it
                                if (insertStart != 0)
                                {
                                    chapters.Add(new ChapterInfo
                                    {
                                        Name = introStartString,
                                        StartPositionTicks = insertStart
                                    });
                                    chapters.Sort(CompareStartTimes);
                                }

                                if (sequence.HasCreditSequence && !chapters.Exists(chapterPoint => chapterPoint.Name == endCredString))
                                {
                                    Log.Debug("CHAPTER INSERT: Adding End Credit Chapter Point for {0}: {1}, Episode{2}: {3} at {4}",
                                        tvShowName, seasonName, episodeNo.ToString(), item.Name, ConvertTicksToTime(EndCredStart));
                                    chapters.Add(new ChapterInfo
                                    {
                                        Name = endCredString,
                                        StartPositionTicks = EndCredStart
                                        
                                    });
                                    chapters.Sort(CompareStartTimes);
                                }

                                //if the Title Sequence does start at Zero then lets just remove chapter 1 and replace it with Title sequence.
                                if (insertStart == 0)
                                {
                                    chapters.RemoveAt(0);

                                    chapters.Add(new ChapterInfo
                                    {
                                        Name = introStartString,
                                        StartPositionTicks = insertStart
                                    });

                                    chapters.Sort(CompareStartTimes);
                                }

                                //create new chapter entry point for the End of the Intro
                                int startIndex = chapters.FindIndex(st => st.Name == introStartString);

                                if (startIndex >= lastIndex)
                                {
                                    //Create the new error - not really an error, but we'll call it that for now.
                                    //The Error can consist of just one piece of data, the item InternalId. That is all we need to request further information about it later.
                                    //This will keep memory low, and also a list of long integers will be faster to send to the UI
                                    // We'll use the browsers engine (the UI) to request further data about the item if necessary.
                                    ChapterErrors.Add(new ChapterError()
                                    {
                                        Id = item
                                            .InternalId, //<-- use the internalId, they are shorter, less data to send to the UI
                                        Date = DateTime.Now, //<-- Give them a date, so they know when this happened.
                                        ChapterCount = iCount,
                                        FilePathString = item.Path
                                    });


                                    Log.Warn(
                                        "CHAPTER INSERT: Not enough Chapter Markers to insert Title Sequence for {0}: {1}, Episode{2}: {3}",
                                        tvShowName, seasonName, episodeNo.ToString(), item.Name);
                                    Log.Warn("CHAPTER INSERT: {0} has been added to Bad Chapter List", item.Name);
                                }

                                if (startIndex < lastIndex)
                                {
                                    ChapterInfo neededChapInfo = chapters[startIndex + 1];
                                    string chapName = neededChapInfo.Name;
                                    Log.Debug("CHAPTER INSERT: Organising..... New Chapter name after Insert = {0}",
                                        chapName);
                                    string newVal = introEndString.Replace(introEndString, chapName);

                                    int changeStart = startIndex + 1;
                                    chapters.RemoveAt(changeStart);
                                    ChapterInfo edit = new ChapterInfo
                                    {
                                        Name = newVal,
                                        StartPositionTicks = insertEnd,

                                    };
                                    //add the entry and lets arrange the chapter list
                                    chapters.Add(edit);
                                    chapters.Sort(CompareStartTimes);

                                    //we need to put this in here otherwise having the SaveChapters outside of this scope will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                                    ItemRepository.SaveChapters(id, chapters);
                                    Log.Debug(
                                        "CHAPTER INSERT: Successfully added Title Sequence for {0} - {1} - {2}: {3}",
                                        tvShowName, seasonName, episodeNo, item.Name);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error("########### CHAPTER INSERT ERROR ###########");
                            Log.Error("######## GO GIVE CHEESE SOME STICK #########", e);
                            throw;
                        }
                    }

                }
            }
            catch
            {
                Log.Info("CHAPTER INSERTION ERROR: THIS ID DOES NOT CONTAIN ANY INFORMATION AND CANNOT BE PROCESSED {0}", id);
            }
        }

        public static int CompareStartTimes(ChapterInfo tick1, ChapterInfo tick2)
        {
            return tick1.StartPositionTicks.CompareTo(tick2.StartPositionTicks);
        }

        public static string ConvertTicksToTime(long ticks)
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
