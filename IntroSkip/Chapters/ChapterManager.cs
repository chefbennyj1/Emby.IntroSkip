using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using System;
using System.Collections.Generic;
using  MediaBrowser.Model.Querying;
using System.Threading;

namespace IntroSkip.Chapters
{
    public class ChapterManager : IServerEntryPoint
    {
        private ILibraryManager LibraryManager {get; set;}
        private IDtoService DtoService { get; set; }
       
        public ChapterManager(ILibraryManager libraryManager, IDtoService dtoService)
        {
            LibraryManager = libraryManager;
            DtoService = dtoService;                 
        }

        private void EditChapters(long id)
        {
            var repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var titleSequence = repo.GetResult(id.ToString());

            if (!titleSequence.HasSequence)
            {
                return;
            }


            var item = LibraryManager.GetItemById(id);
            var itemDto = DtoService.GetBaseItemDto(item, new DtoOptions() { Fields = new ItemFields[] { ItemFields.Chapters } });
            
            //We are about to edit chapters by increasing the existing chapter data by the duration of the intro.
            //We do not want to edit chapters beyond the item runtime!
            var runtime               = itemDto.RunTimeTicks;
            var titleSequenceDuration = (titleSequence.TitleSequenceEnd - titleSequence.TitleSequenceStart).Ticks;
            var chapters              = itemDto.Chapters;

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
            
            LibraryManager.UpdateItem(item, item.Parent, ItemUpdateType.MetadataEdit); //<-- Does this update the itemDto?
            
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
