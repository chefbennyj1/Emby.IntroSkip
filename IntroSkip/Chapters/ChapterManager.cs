using System;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;
using IntroSkip.Data;
using IntroSkip.TitleSequence;

namespace IntroSkip.Chapters
{
    public class ChapterInsertion
    {
        public ChapterInsertion Instance { get; set; }

        public ILogger Log;

        public IItemRepository ItemRepository;

        public ChapterInsertion(ILogManager logManager, IItemRepository itemRepo)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            ItemRepository = itemRepo;
            Instance = this;
        }

        public void EditChapters(long id)
        {
            Log.Debug("CHAPTER EDIT: PASSED ID = {0}", id);
            ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            TitleSequenceResult titleSequence = repo.GetResult(id.ToString());

            var item = ItemRepository.GetItemById(id);
            var tvShow = item.Parent.Parent.Name;
            var season = item.Parent.Name;
            var episodeNo = item.IndexNumber;
            Log.Info("CHAPTER EDIT: TV Show: {0}", tvShow);
            Log.Info("CHAPTER EDIT: Getting Chapter Info for {0}: {1}", episodeNo, item.Name);

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
                    Log.Debug("CHAPTER EDIT: ADDED.... NAME = {0} --- START TIME = {1}", chap.Name, chap.StartPositionTicks.ToString());
                }

                long insertStart = titleSequence.TitleSequenceStart.Ticks;
                long insertEnd = titleSequence.TitleSequenceEnd.Ticks;

                string introStartString = "Title Sequence";
                string introEndString = "Intro End";
                int iCount = chapters.Count;

                //This wil check if the Introstart chapter point is already in the list
                if (chapters.Exists(chapterPoint => chapterPoint.Name == introStartString))
                {
                    Log.Info("CHAPTER EDIT: Title Sequence Chapter already Added for {0}", item.Name);
                }
                else
                {
                    try
                    {
                        //create new chapter entry point for the Start of the Intro
                        ChapterInfo introStart = new ChapterInfo
                        {
                            Name = introStartString,
                            StartPositionTicks = insertStart
                        };


                        //add the entry and lets arrange the chapter list
                        //if the Title sequence doesn't start at 0, insert it
                        if (introStart.StartPositionTicks != 0 && chapters.Count >= 2)
                        {
                            chapters.Add(introStart);
                            chapters.Sort(CompareStartTimes);
                        }

                        //if the Title Sequence does start at Zero then lets just remove chapter 1 and replace it with Title sequence.
                        if (introStart.StartPositionTicks == 0)
                        {
                            chapters.RemoveAt(0);
                            chapters.Add(introStart);
                            chapters.Sort(CompareStartTimes);
                        }

                        //create new chapter entry point for the End of the Intro
                        int startIndex = chapters.FindIndex(st => st.Name == introStartString);

                        if (chapters.Count <= 2)
                        {
                            Log.Warn("CHAPTER EDIT: Not enough Chapter Markers for {0}: {1}, Episode{2}: {3}", tvShow, season, episodeNo, item.Name);
                            Log.Warn("CHAPTER EDIT: Please check this episode in your library");
                        }
                        if (chapters.Count >= 2)
                        {
                            ChapterInfo neededChapInfo = chapters[startIndex + 1];
                            string chapName = neededChapInfo.Name;
                            Log.Debug("CHAPTER EDIT: New Chapter name = {0}", chapName);
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
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error("########### CHAPTER EDIT ERROR ###########", e);
                        throw;
                    }
                }
            }
        }


        public static int CompareStartTimes(ChapterInfo tick1, ChapterInfo tick2)
        {
            return tick1.StartPositionTicks.CompareTo(tick2.StartPositionTicks);
        }
    }
}
