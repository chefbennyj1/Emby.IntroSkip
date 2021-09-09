using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using System;
using System.Collections.Generic;
using MediaBrowser.Model.Querying;
using System.Linq;
using MediaBrowser.Model.Logging;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;

namespace IntroSkip.Chapters
{
    public class ChapterManager : IServerEntryPoint
    {
        private ILibraryManager LibraryManager {get; set;}
        private IDtoService DtoService { get; set; }
        private IUserManager UserManager {get; set;}
        public static ChapterManager Instance {get; set; }
        private ILogger Log {get; set;}

        private IItemRepository ItemRepository {get; set;}
        public ChapterManager(ILibraryManager libraryManager, IDtoService dtoService, IUserManager userManager, ILogManager logManager, IItemRepository itemRepo)
        {
            UserManager    = userManager;
            LibraryManager = libraryManager;
            DtoService     = dtoService;  
            Log            = logManager.GetLogger(Plugin.Instance.Name);
            ItemRepository = itemRepo;
            Instance       = this;
            
        }

        public void EditChapters(long id)
        {
            var admin = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator);
            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var titleSequence = repo.GetResult(id.ToString());

            var item = LibraryManager.GetItemById(id);
            var itemDto = DtoService.GetBaseItemDto(item, new DtoOptions() { Fields = new ItemFields[] { ItemFields.Chapters } });

            if (!titleSequence.HasSequence)
            {
                return;
            }
                      
            
            //We are about to edit chapters by increasing the existing chapter data by the duration of the intro.
            //We do not want to edit chapters beyond the item runtime!
            var runtime               = itemDto.RunTimeTicks;
            var titleSequenceDuration = (titleSequence.TitleSequenceEnd - titleSequence.TitleSequenceStart).Ticks;
            List<ChapterInfo> chapters = new List<ChapterInfo>();
            foreach(var chap in ItemRepository.GetChapters(item))
            {
                chapters.Add(new ChapterInfo
                {
                    Name = chap.Name,
                    StartPositionTicks = chap.StartPositionTicks,
                });
            }

            if(titleSequence.TitleSequenceStart == TimeSpan.FromSeconds(0))
            {
                chapters[0].StartPositionTicks = 0;
                chapters[0].Name = "Intro";
                chapters[1].StartPositionTicks = titleSequence.TitleSequenceEnd.Ticks;

                for(var i = 2; i <= chapters.Count -1; i++)
                {
                    var startPositionTicks = chapters[i].StartPositionTicks;
                    if((startPositionTicks + titleSequenceDuration) >= runtime)
                    {
                        break;
                    }
                    
                    chapters[i].StartPositionTicks = startPositionTicks + titleSequenceDuration;                    
                }                
                
            }
            else
            {
                chapters[1].StartPositionTicks = titleSequence.TitleSequenceStart.Ticks;
                chapters[1].Name = "Intro";
                chapters[2].StartPositionTicks = titleSequence.TitleSequenceEnd.Ticks;
                for(var i = 3; i <= chapters.Count -1; i++)
                {
                    var startPositionTicks = chapters[i].StartPositionTicks;
                    if((startPositionTicks + titleSequenceDuration) >= runtime)
                    {
                        break;
                    }
                    
                    chapters[i].StartPositionTicks = startPositionTicks + titleSequenceDuration;
                    
                }
            }
                        
            ItemRepository.SaveChapters(id, chapters);
            
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Run()
        {
            
        }
    }
}
