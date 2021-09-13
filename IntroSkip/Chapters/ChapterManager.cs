using MediaBrowser.Controller.Plugins;
using System;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
using System.Linq;
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
            TitleSequence.TitleSequenceResult titleSequence = repo.GetResult(id.ToString());
            var config = Plugin.Instance.Configuration;

            var item = ItemRepository.GetItemById(id);
            Log.Info("INTROSKIP CHAPTER EDIT: Name of Episode = {0}", item.Name);

            List<ChapterInfo> getChapters = ItemRepository.GetChapters(item);
            List<ChapterInfo> chapters = new List<ChapterInfo>();

            if (titleSequence.HasSequence && config.EnableChapterInsertion)
            {
                //Lets get the existing chapters and put them in a new list so we can insert the new Intro Chapter
                foreach (var chap in getChapters)
                {
                    chapters.Add(new ChapterInfo
                    {
                        Name = chap.Name,
                        StartPositionTicks = chap.StartPositionTicks,
                    });
                    Log.Debug("INTROSKIP CHAPTER EDIT: ADDED.... NAME = {0} --- START TIME = {1}", chap.Name.ToString(),
                        chap.StartPositionTicks.ToString());
                }

                long insertStart = titleSequence.TitleSequenceStart.Ticks;
                long insertEnd = titleSequence.TitleSequenceEnd.Ticks;


                string introStartString = "Title Sequence";
                string introEndString = "Intro End";

                //This will check if the Introstart chapter point is already in the list.
                if (chapters.Exists(chapterPoint => chapterPoint.Name == introStartString))
                {
                    Log.Info(
                        "INTROSKIP CHAPTER EDIT: INTRO START CHAPTER ALREADY ADDED - move along nothing to see here");
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

<<<<<<< Updated upstream
                    //we need to put this in here otherwise having the SaveChapters outside of this scope will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                    //ItemRepository.SaveChapters(id, chapters);
                
                    //create new chapter entry point for the End of the Intro
                    ChapterInfo introEnd = new ChapterInfo
                    {
                        Name = introEndString,
                        StartPositionTicks = insertEnd
                    };
                    int startIndex = chapters.FindIndex(st => st.Name == introStartString);
                    int endIndex = chapters.FindIndex(en => en.Name == introEndString);

<<<<<<< Updated upstream
=======
                    //create new chapter entry point for the End of the Intro
                    ChapterInfo introEnd = new ChapterInfo
                    {
                        Name = introEndString,
                        StartPositionTicks = insertEnd
                    };
                    int startIndex = chapters.FindIndex(st => st.Name == introStartString);
                    
                    //Get the next chapter information
=======
>>>>>>> Stashed changes
                    ChapterInfo neededChapInfo = chapters[startIndex + 1];
                    string chapName = neededChapInfo.Name;
                    Log.Info("INTROSKIP CHAPTER EDIT: New Chapter name = {0}", chapName);
                    string newVal = introEndString.Replace(introEndString, chapName);

<<<<<<< Updated upstream
                    //remove the IntroEnd point and replace it with the correct chapter name.
=======
>>>>>>> Stashed changes
                    int changeStart = startIndex + 1;
                    chapters.RemoveAt(changeStart);
                    ChapterInfo edit = new ChapterInfo
                    {
                        Name = newVal,
                        StartPositionTicks = insertEnd
                    };
<<<<<<< Updated upstream
>>>>>>> Stashed changes
=======
>>>>>>> Stashed changes
                    //add the entry and lets arrange the chapter list
                    chapters.Add(edit);
                    chapters.Sort(CompareStartTimes);
<<<<<<< Updated upstream
<<<<<<< Updated upstream

=======
                    
>>>>>>> Stashed changes
                    //lets check and remove any chapters that fall inbetween the IntroStart and IntroEnd they are not needed.
                    Log.Info("INTROSKIP CHAPTER EDIT: IntroStartIndex = {0} & IntroEndIndex = {1}", startIndex.ToString(), endIndex.ToString());
                    /*if (startIndex + 1 != endIndex)
                    {
                        Log.Info("INTROSKIP CHAPTER EDIT: HOUSTON WE HAVE A PROBLEM, Removing Chapter at Index = {0}",
                            (endIndex - 1));
                        var naughtyIndex = endIndex - 1;
                        chapters.RemoveAt(naughtyIndex);
                    }
                    else
                    {
                        Log.Info("INTROSKIP CHAPTER EDIT: No issues with Rogue Chapters");
                    }*/
                    

                    //we need to put this in here otherwise having the SaveChapters outside of this scope will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                    ItemRepository.SaveChapters(id, chapters);
<<<<<<< Updated upstream
                    
=======
                    
                    //we need to put this in here otherwise having the SaveChapters outside of this scope will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                    ItemRepository.SaveChapters(id, chapters);
>>>>>>> Stashed changes
=======

>>>>>>> Stashed changes
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
