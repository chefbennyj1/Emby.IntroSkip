using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.AutoOrganize.Data;
using IntroSkip.TitleSequence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using IntroSkip.Data;

namespace IntroSkip.Chapters
{
    public class ChapterEditScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        public ILibraryManager LibraryManager {get; set;}
        public IItemRepository ItemRepository;
        //private ITitleSequenceRepository Repo;
        //private ILogger Log;

        public ChapterEditScheduledTask(ILibraryManager libraryManager, IItemRepository itemRepo /*ITitleSequenceRepository repo ILogger log*/)
        {
            LibraryManager = libraryManager;
            ItemRepository = itemRepo;
            //Repo = repo;
            //Log = log;
        }
        //QueryResult<TitleSequenceResult> dbResults = null;
        
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            ProcessEpisodeChaptersPoints();
            //ChapterManager.Instance.EditChapters(11951);
        }

        
        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        public string Name => "IntroSkip Chapter Insertion";

        public string Key => "Chapter Edit Options";

        public string Description => "Insert a Chapter Marker for Intro Start and End Times";

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

        public void ProcessEpisodeChaptersPoints()
        {
            ILogger Log;
            QueryResult<TitleSequenceResult> dbResults = null;
            ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            dbResults = repo.GetResults(new TitleSequenceResultQuery());
            
            foreach (var episode in dbResults.Items)
            {
                if(episode.HasSequence)
                {
                    var id = episode.InternalId;
                    //Log.Info("CHAPTER EDIT: TASK ---- ID = {0}", id);
                    ChapterManager.Instance.EditChapters(id);

                }
            }
        }
    }
}