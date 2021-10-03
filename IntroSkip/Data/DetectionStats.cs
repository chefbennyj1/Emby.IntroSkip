using System;
using System.Collections.Generic;
using System.Text;
using IntroSkip.Chapters;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;

namespace IntroSkip.Data
{
    public class DetectionStats
    {
        public ILibraryManager LibraryManager { get; set; }
        private ITaskManager TaskManager { get; }
        private IItemRepository ItemRepo { get; }
        private ILogger Log { get; }

        public List<TVShowStats> ShowStats = new List<TVShowStats>();

        public DetectionStats(ILibraryManager libraryManager, ITaskManager taskManager, ILogManager logManager, IItemRepository itemRepo)
        {
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
            ItemRepo = itemRepo;
        }

        

        public void GetStatistics()
        {
            var Repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            QueryResult<TitleSequenceResult> dbResults = Repository.GetResults(new TitleSequenceResultQuery());

            foreach (var episode in dbResults.Items)
            {
                
            }
        }

    }

    public class TVShowStats
    {
        
    }
}
