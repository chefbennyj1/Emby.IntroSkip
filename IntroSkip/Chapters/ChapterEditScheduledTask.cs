using MediaBrowser.Controller.Library;
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
        public ChapterEditScheduledTask(ILibraryManager libraryManager)
        {
            LibraryManager = libraryManager;
        }
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            
            ChapterManager.Instance.EditChapters(40);
        }

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public string Name => "Chapter Edit";

        public string Key => "Chapter Edit Options";

        public string Description => "Edit Chapters with found intro timestamps";

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
