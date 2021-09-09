using MediaBrowser.Controller.Plugins;
using System;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace IntroSkip.Chapters
{
    public class ChapterManager : IServerEntryPoint
    {
       public static ChapterManager Instance {get; set; }

        public ILogger Log;

        public IItemRepository ItemRepository;
        public ChapterManager(ILogger log, IItemRepository itemRepo)
        {
            Log = log;
            ItemRepository = itemRepo;
            Instance = this;            
        }

        public void EditChapters(long id)
        {
            Log.Info("INTROSKIP CHAPTER EDIT: PASSED ID = {0}", id);
            Emby.AutoOrganize.Data.ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            TitleSequence.TitleSequenceResult titleSequence = repo.GetResult(id);
            
            var item = ItemRepository.GetItemById(id);
            Log.Info("INTROSKIP CHAPTER EDIT: Name of Episode = {0}", item.Name);
                        
            List<ChapterInfo> getChapters = ItemRepository.GetChapters(item);
            List<ChapterInfo> chapters = new List<ChapterInfo>();

            //Lets get the existing chapters and put them in a new list so we can insert the new Intro Chapter
            foreach (var chap in getChapters)
            {
                chapters.Add(new ChapterInfo
                {
                    Name = chap.Name,
                    StartPositionTicks = chap.StartPositionTicks,
                });
                Log.Debug("INTROSKIP CHAPTER EDIT: ADDED.... NAME = {0} --- START TIME = {1}", chap.Name.ToString(), chap.StartPositionTicks.ToString());
            }

            long insertStart = titleSequence.TitleSequenceStart.Ticks;
            long insertEnd = titleSequence.TitleSequenceEnd.Ticks;

            if (titleSequence.HasSequence)
            {
                string introStartString = "Intro Start";
                string introEndString = "Intro End";

                //This wil check if the Introstart and introEnd chapter points are already in the list, we may have one or the other so lets independently check.
                if (chapters.Exists(chapterPoint => chapterPoint.Name == introStartString))
                {
                    Log.Info("INTROSKIP CHAPTER EDIT: INTRO START CHAPTER ALREADY ADDED - move along nothing to see here");
                }
                else
                {
                    //create new chapter entry point for the Start of the Intro
                    ChapterInfo IntroStart = new ChapterInfo();
                    IntroStart.Name = introStartString;
                    IntroStart.StartPositionTicks = insertStart;

                    //add the entry and lets arrange the chapter list
                    chapters.Add(IntroStart);
                    chapters.Sort(CompareStartTimes);

                    //we need to put this in here otherwise having the SaveChapters outside of this scope will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                    ItemRepository.SaveChapters(id, chapters);
                }

                if (chapters.Exists(chapterPoint => chapterPoint.Name == introEndString))
                {
                    Log.Info("INTROSKIP CHAPTER EDIT: INTRO END CHAPTER ALREADY ADDED - move along nothing to see here");
                }
                else 
                { 
                    //create new chapter entry point for the End of the Intro
                    ChapterInfo IntroEnd = new ChapterInfo();
                    IntroEnd.Name = introEndString;
                    IntroEnd.StartPositionTicks = insertEnd;

                    //add the entry and lets arrange the chapter list
                    chapters.Add(IntroEnd);                    
                    chapters.Sort(CompareStartTimes);

                    //lets check and remove any chapters that fall inbetween the IntroStart and IntroEnd they are not needed.
                    List<ChapterInfo> organisedChapters = new List<ChapterInfo>();
                    int startIndex = chapters.FindIndex(st => st.Name == introStartString);
                    int endIndex = chapters.FindIndex(en => en.Name == introEndString);
                    Log.Info("INTROSKIP CHAPTER EDIT: IntroStartIndex = {0} & IntroEndIndex = {1}", startIndex.ToString(), endIndex.ToString());

                    if (startIndex + 1 != endIndex)
                    {
                        Log.Info("INTROSKIP CHAPTER EDIT: HOUSTON WE HAVE A PROBLEM, Removing Chapter at Index = {0}", (endIndex-1));
                        var naughtyIndex = endIndex - 1;
                        chapters.RemoveAt(naughtyIndex);
                        
                        foreach (var chap in chapters)
                        {
                            //put in some stuff to rename emby standard chapters so it looks good and has sequencial numbering and add it to the organised chapter list.
                            //chapters = organisedchapters;
                        }
                    }
                    else
                    {
                        Log.Info("INTROSKIP CHAPTER EDIT: No issues with Rogue Chapters");
                    }

                    //we need to put this in here otherwise having the SaveChapters outside of this scope will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                    ItemRepository.SaveChapters(id, chapters);
                }
            }
        }

        public static int CompareStartTimes(ChapterInfo tick1, ChapterInfo tick2)
        {
            return tick1.StartPositionTicks.CompareTo(tick2.StartPositionTicks);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Run()
        {
            throw new NotImplementedException();
        }
    }
}