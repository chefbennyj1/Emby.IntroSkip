using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntroSkip.Chapters
{
    public class ChapterEditScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        public ILibraryManager LibraryManager {get; set;}
        public IItemRepository ItemRepository;

        public ChapterEditScheduledTask(ILibraryManager libraryManager, IItemRepository itemRepo)
        {
            LibraryManager = libraryManager;
            ItemRepository = itemRepo;
        }
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {

            //NEED TO PULL THE ID FROM TITLESEQUENCE DATABASE WHEN RUNNING THIS.
            //ChapterManager.Instance.EditChapters(11954); // Wesworld S01 Episode 4
            ChapterManager.Instance.EditChapters(11952); // Westworld S02 episode 2
            ChapterManager.Instance.EditChapters(12056); // Westworld S02 Episode 3 tricky one!!
        }

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public string Name => "IntroSkip Chapter Edit";

        public string Key => "Chapter Edit Options";

        public string Description => "Insert a Chapter Marker for Intro Timestamp";

        public string Category => "Intro Skip";

        

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type          = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }                
            };
        }
    }
}
