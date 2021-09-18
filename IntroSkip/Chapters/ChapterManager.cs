
﻿using System;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
 using MediaBrowser.Model.Entities;
 using IntroSkip.TitleSequence;

 namespace IntroSkip.Chapters
{
    public class ChapterInsertion
    {
        public ChapterInsertion Instance { get; set; }

        private ILogger Log;

        private IItemRepository ItemRepository;

        public List<ProblemChapters> ProbChapterList = new List<ProblemChapters>();

        public ChapterInsertion(ILogManager logManager, IItemRepository itemRepo)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            ItemRepository = itemRepo;
            Instance = this;
        }

        public void EditChapters(long id)
        {
            Log.Debug("CHAPTER INSERT: PASSED ID from TASK = {0}", id);

            //ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            TitleSequenceResult titleSequence = repository.GetResult(id.ToString());

            var repo = repository as IDisposable;
            repo?.Dispose();



            var item = ItemRepository.GetItemById(id);
            var tvShowName = item.Parent.Parent.Name;
            var tvShowId = item.Parent.Parent.Id;
            var seasonName = item.Parent.Name;
            var seasonId = item.Parent.Id;
            var episodeNo = item.IndexNumber;
            Log.Info("CHAPTER INSERT: TV Show: {0} - {1}", tvShowName, seasonName);
            Log.Info("CHAPTER INSERT: Getting Chapter Info for {0}: {1}", episodeNo, item.Name);

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
                    Log.Debug("CHAPTER INSERT: Fetch Existing Chapters: {0} Starts at {1}", chap.Name, chap.StartPositionTicks.ToString());
                }

                long insertStart = titleSequence.TitleSequenceStart.Ticks;
                long insertEnd = titleSequence.TitleSequenceEnd.Ticks;

                string introStartString = "Title Sequence";
                string introEndString = "Intro End";

                int iCount = chapters.Count;
                int lastIndex = iCount - 1;

                //This wil check if the Introstart chapter point is already in the list
                if (chapters.Exists(chapterPoint => chapterPoint.Name == introStartString))
                {
                    Log.Info("CHAPTER INSERT: Title Sequence Chapter already Added for {0}", item.Name);
                }
                else
                {
                    try
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

                        //if the Title Sequence does start at Zero then lets just remove chapter 1 and replace it with Title sequence.
                        if (insertStart == 0)
                        {
                            chapters.RemoveAt(0);

                            chapters.Add( new ChapterInfo
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
                            //Lets add these to a list so that we can display them for the user on the Chapters page.
                            ProbChapterList.Add( new ProblemChapters
                                {
                                    ShowId = tvShowId,
                                    ShowName = tvShowName,
                                    SeasonId = seasonId,
                                    SeasonName = seasonName,
                                    EpisodeIndex = episodeNo,
                                    EpisodeId = item.Id,
                                    EpisodeName = item.Name,
                                    ChaptersNos = chapters.Count
                                });

                            Log.Warn("CHAPTER INSERT: Not enough Chapter Markers for {0}: {1}, Episode{2}: {3}", tvShowName, seasonName, episodeNo, item.Name);
                            Log.Warn("CHAPTER INSERT: Please check this episode in your library");
                        }

                        if (startIndex < lastIndex)
                        {
                            ChapterInfo neededChapInfo = chapters[startIndex + 1];
                            string chapName = neededChapInfo.Name;
                            Log.Debug("CHAPTER INSERT: Organising..... New Chapter name after Insert = {0}", chapName);
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
                        Log.Error("########### CHAPTER INSERT ERROR ###########");
                        Log.Error("######## GO GIVE CHEESE SOME STICK #########", e);
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
