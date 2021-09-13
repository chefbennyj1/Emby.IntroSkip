using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IntroSkip.TitleSequence;
using MediaBrowser.Model.Querying;
using IntroSkip.Data;

namespace IntroSkip.Chapters
{
    public class ChapterEditScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        public ILibraryManager LibraryManager {get; set;}
        public IItemRepository ItemRepository;
        private ITaskManager TaskManager { get;}

        public ChapterEditScheduledTask(ILibraryManager libraryManager, IItemRepository itemRepo, ITaskManager taskManager)
        {
            LibraryManager = libraryManager;
            ItemRepository = itemRepo;
            TaskManager = taskManager;
        }
        
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            //Run the ChapterEdit task and wait for it to finish before moving on to Image extraction
            Task chapterExecute = Task.Factory.StartNew(ProcessEpisodeChaptersPoints, cancellationToken);
            chapterExecute.Wait(cancellationToken);

            var config = Plugin.Instance.Configuration;
            
            // If the user has enabled Chapter Image Extraction in the Advanced menu then lets run that process! 
            if (chapterExecute.IsCompleted && config.EnableAutomaticImageExtraction == true)
            {
                ProcessChapterImageExtraction();
            }
            //we need to return the Chapter Point Edit Task to close out that the task has completed otherwise the process flags as "Failed"
            return chapterExecute;
        }

        public bool IsHidden => false;

        public bool IsEnabled => false;

        public bool IsLogged => true;

        public string Name => "IntroSkip Chapter Insertion";

        public string Key => "Chapter Edit Options";

        public string Description => "Insert a Chapter Marker for Intro Start and End Times";

        public string Category => "Intro Skip";
                

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo()                
            };
        }

        public Task ProcessEpisodeChaptersPoints()
        {
            
            QueryResult<TitleSequenceResult> dbResults = null;
            ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            dbResults = repo.GetResults(new TitleSequenceResultQuery());
            
            foreach (var episode in dbResults.Items)
            {
                if(episode.HasSequence)
                {
                    var id = episode.InternalId;
                    ChapterManager.Instance.EditChapters(id);
                }
            }

            return null;
        }

        public Task ProcessChapterImageExtraction()
        {
            //var chapterEdit = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "IntroSkip Chapter Insertion");
            var thumbnail = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Thumbnail image extraction");
            
            TaskManager.Execute(thumbnail, new TaskOptions());

            return null;
        }
    }
}