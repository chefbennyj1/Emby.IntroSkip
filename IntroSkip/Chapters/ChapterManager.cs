using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using System;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;
using System.Linq;

namespace IntroSkip.Chapters
{
    public class ChapterManager : IServerEntryPoint
    {
        private ILibraryManager LibraryManager {get; set;}
        private IDtoService DtoService { get; set; }
        public static ChapterManager Instance {get; set; }

        public ILogger Log;
        public IItemRepository ItemRepository;
        public ChapterManager(ILibraryManager libraryManager, IDtoService dtoService, ILogger log, IItemRepository itemRepo)
        {
            LibraryManager = libraryManager;
            DtoService = dtoService;
            Log = log;
            ItemRepository = itemRepo;
            Instance = this;            
        }

        public void EditChapters(long id)
        {
            Log.Info("CHAPTER EDIT: PASSED ID = {0}", id);
            Emby.AutoOrganize.Data.ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            TitleSequence.TitleSequenceResult titleSequence = repo.GetResult(id);
            
            var item = ItemRepository.GetItemById(id);
            Log.Info("CHAPTER EDIT: Name of Episode = {0}", item.Name);
                        
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
                Log.Info("CHAPTER EDIT: ADDED.... NAME = {0} --- START TIME = {1}", chap.Name.ToString(), chap.StartPositionTicks.ToString());
            }

            long insertStart = titleSequence.TitleSequenceStart.Ticks;
            long insertEnd = titleSequence.TitleSequenceEnd.Ticks;

            if (titleSequence.HasSequence)
            {
                string introStart = "Intro Start";
                string introEnd = "Intro End";

                //This wil check if the Introstart and introend chapter points are already in the list
                if (chapters.Exists(Item => Item.Name == introStart))
                {
                    Log.Info("CHAPTER EDIT: INTRO START CHAPTER ALREADY ADDED - move along nothing to see here");
                }
                else
                {
                    ChapterInfo IntroStart = new ChapterInfo();
                    IntroStart.Name = "Intro Start";
                    IntroStart.StartPositionTicks = insertStart;
                    chapters.Add(IntroStart);
                    chapters.Sort(CompareStartTimes);
                    //we need to put this in here otherwise having the SaveChapters outside will force the user to do another Thumbnail extract Task, everytime the ChapterEdit Task is run.
                    ItemRepository.SaveChapters(id, chapters);
                }

                if (chapters.Exists(Item => Item.Name == introEnd))
                {
                    Log.Info("CHAPTER EDIT: INTRO END CHAPTER ALREADY ADDED");
                }
                else 
                { 
                    ChapterInfo IntroEnd = new ChapterInfo();
                    IntroEnd.Name = "Intro End";
                    IntroEnd.StartPositionTicks = insertEnd;
                    chapters.Add(IntroEnd);
                    chapters.Sort(CompareStartTimes);
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
