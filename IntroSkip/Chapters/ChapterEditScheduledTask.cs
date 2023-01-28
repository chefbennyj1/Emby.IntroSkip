using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using SQLitePCL.pretty;


namespace IntroSkip.Chapters
{
    public class ChapterEditScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private BaseItem[] _episodeItems;
        private int _totalEpisodes;

        private ITaskManager TaskManager                  { get; }
        private ChapterInsertion ChapterInsertion         { get; }
        private ChapterErrorTextFileCreator ErrorTextFile { get; }
        private ILogger Log                               { get; }
        private IItemRepository ItemRepository            { get; }
        private ILibraryManager LibraryManager            { get; }

        
        public ChapterEditScheduledTask(ITaskManager taskManager, ILogManager logManager, ChapterInsertion chapterInsertion, ChapterErrorTextFileCreator textFile, IItemRepository itemRepository, ILibraryManager libraryManager)
        {
            TaskManager = taskManager;
            Log = logManager.GetLogger(Plugin.Instance.Name + "-CHAPTER-TASK");
            ChapterInsertion = chapterInsertion;
            ErrorTextFile = textFile;
            ItemRepository = itemRepository;
            LibraryManager = libraryManager;
        }

        
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance.Configuration;
            Task chapterExecute = null;
            var detection = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Title Sequence Detection");

            while(detection != null && detection.State == TaskState.Running)
            {
                Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            
            if (config.EnableChapterInsertion)
            {
                //Run the ChapterEdit task and wait for it to finish before moving on to Image extraction
                chapterExecute = Task.Run(ProcessEpisodeChaptersPoints, cancellationToken);
                chapterExecute.Wait(cancellationToken);

                if (chapterExecute.IsCompleted && ChapterInsertion.ChapterErrors != null)
                {
                    ErrorTextFile.JotErrorFilePaths();
                }

                // If the user has enabled Chapter Insert option and Chapter Image Extraction in the Advanced menu then lets run that process! 
                if (chapterExecute.IsCompleted && config.EnableAutomaticImageExtraction)
                {
                    ProcessChapterImageExtraction();
                }
                //we need to return the Chapter Point Edit Task to close out that the task has completed otherwise the process flags as "Failed"
            }
            else
            {
                Log.Debug("FAILED - You may need to enable this in the Plugin Configuration");
            }

            //we need to return the Chapter Point Edit Task to close out that the task has completed otherwise the process flags as "Failed"
            return chapterExecute;
        }

        public bool IsHidden
        {
            get
            {
                var config = Plugin.Instance.Configuration;
                return !config.EnableChapterInsertion;
            }
        }

        public bool IsEnabled
        {
            get
            {
                var config = Plugin.Instance.Configuration;
                return config.EnableChapterInsertion;
            }
        }

        public bool IsLogged => true;

        public string Name => "IntroSkip Chapter Insertion";

        public string Key => "IntroSkip Chapter Edit Options";

        public string Description => "Insert a Title Sequence Marker in Chapters";

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

        private async Task<BaseItem[]> GetCoreEpisodes()
        {
            Log.Info("Getting Episodes in Library");
            var episodeList = new InternalItemsQuery()
            {
                Recursive = false,
                IncludeItemTypes = new[] { "Episode" },
                //IsVirtualItem = false,
            };
            var episodeItems = LibraryManager.GetItemList(episodeList);
            _totalEpisodes = _episodeItems.Length;
            Log.Info("Total Episodes in Library = {0} ", _totalEpisodes);
            return episodeItems;
        }
        

        private async Task ProcessEpisodeChaptersPoints()
        {
            Log.Debug("Starting episode chapter insertion...");
            var config = Plugin.Instance.Configuration;

            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults = repository.GetResults(new SequenceResultQuery());
           //var coreEpisodes = GetCoreEpisodes();

            ChapterInsertion.ChapterErrors.Clear();
            var repo = repository as IDisposable;

            foreach (var episode in dbResults.Items)
            {
                try
                {
                    bool hasCoreIntro = false;

                    //Core Check for chapters
                    var item = LibraryManager.GetItemById(episode.InternalId);
                    Log.Debug("CHAPTER TASK: Getting core Chapters");
                    List<ChapterInfo> getChapters = ItemRepository.GetChapters(item);
                    Log.Debug("CHAPTER TASK: Core Chapters Retrieved");

                    foreach (var chapterInfo in getChapters)
                    {
                        if (chapterInfo.MarkerType == MarkerType.IntroStart)
                        {
                            hasCoreIntro = true;
                            Log.Debug("Core Intro Detected!!");
    
                        }
                    }
                    
                    if (config.EnableChapterInsertion && (episode.HasTitleSequence || episode.HasCreditSequence) && hasCoreIntro == false)
                    {
                        Log.Debug("Plugin holds Intro/Credit info for {0} - Core has no information", episode.InternalId);
                        long id = episode.InternalId;
                        //Log.Debug("CHAPTER TASK: EPISODE ID = {0}", id);
                        await ChapterInsertion.Instance.InsertIntroChapters(id, episode);
                    }
                    if (config.EnableChapterInsertion && episode.HasCreditSequence && hasCoreIntro == true)
                    {
                        Log.Debug("Core holds Intro Info for {0} - But Plugin has End Credit info",
                            episode.InternalId);
                        long id = episode.InternalId;
                        //Log.Debug("CHAPTER TASK: EPISODE ID = {0}", id);
                        await ChapterInsertion.Instance.InsertIntroChapters(id, episode);
                        hasCoreIntro = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Failed to insert chapters:", ex);
                   
                    if (repo != null)
                    {
                        repo.Dispose();
                    }
                }
            }
            if (repo != null)
            {
                repo.Dispose();
            }
        }

        private Task ProcessChapterImageExtraction()
        {
            var thumbnail = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Thumbnail image extraction");

            TaskManager.Execute(thumbnail, new TaskOptions());

            return Task.FromResult(true);
        }

        /*public Task RefreshChapters()
        {
            var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();

            QueryResult<TitleSequenceResult> dbResults = repository.GetResults(new TitleSequenceResultQuery());
            foreach (TitleSequenceResult episode in dbResults.Items)
            {
                BaseItem item = ItemRepo.GetItemById(episode.InternalId);
                item.RefreshMetadata(CancellationToken.None);
            }

            var repo = repository as IDisposable;
            repo?.Dispose();

            return null;
        }*/
    }
}