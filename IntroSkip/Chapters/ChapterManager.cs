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
       public ChapterInsertion Instance {get; set; }

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
            Log.Debug("INTROSKIP CHAPTER EDIT: PASSED ID = {0}", id);
            ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            TitleSequenceResult titleSequence = repo.GetResult(id.ToString());
            
            var item = ItemRepository.GetItemById(id);
            Log.Info("INTROSKIP CHAPTER EDIT: Name of Episode = {0}", item.Name);

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
                    Log.Debug("INTROSKIP CHAPTER EDIT: ADDED.... NAME = {0} --- START TIME = {1}", chap.Name, chap.StartPositionTicks.ToString());
                }

                long insertStart = titleSequence.TitleSequenceStart.Ticks;
                long insertEnd = titleSequence.TitleSequenceEnd.Ticks;

                string introStartString = "Title Sequence";
                string introEndString = "Intro End";

                //This wil check if the Introstart chapter point is already in the list
                if (chapters.Exists(chapterPoint => chapterPoint.Name == introStartString))
                {
                    Log.Info(
                        "INTROSKIP CHAPTER EDIT: Title Sequence Chapter already Added - move along nothing to do here");
                }
                else
                {
                    //create new chapter entry point for the Start of the Intro
                    ChapterInfo introStart = new ChapterInfo
                    {
                        Name = introStartString,
                        StartPositionTicks = insertStart
                    };

                    //add the entry and lets arrange the chapter list
                    chapters.Add(introStart);
                    chapters.Sort(CompareStartTimes);

                    //create new chapter entry point for the End of the Intro
                    int startIndex = chapters.FindIndex(st => st.Name == introStartString);

                    ChapterInfo neededChapInfo = chapters[startIndex + 1];
                    string chapName = neededChapInfo.Name;
                    Log.Debug("INTROSKIP CHAPTER EDIT: New Chapter name = {0}", chapName);
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
        }


        public static int CompareStartTimes(ChapterInfo tick1, ChapterInfo tick2)
        {
            return tick1.StartPositionTicks.CompareTo(tick2.StartPositionTicks);
        }
    }
}
