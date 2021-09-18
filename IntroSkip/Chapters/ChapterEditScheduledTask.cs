
﻿using MediaBrowser.Controller.Library;

using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using IntroSkip.TitleSequence;
using MediaBrowser.Model.Querying;
using IntroSkip.Data;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;


namespace IntroSkip.Chapters
{
    public class ChapterEditScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        public ILibraryManager LibraryManager { get; set; }
        private ITaskManager TaskManager { get; }
        private ChapterInsertion ChapterInsertion { get; }
        private IItemRepository ItemRepo { get; }
        private ILogger Log { get; }

        public ChapterEditScheduledTask(ILibraryManager libraryManager, ITaskManager taskManager, ILogManager logManager, ChapterInsertion chapterInsertion, IItemRepository itemRepo)
        {
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
            ChapterInsertion = chapterInsertion;
            ItemRepo = itemRepo;
        }

        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance.Configuration;
            Task chapterExecute = null;
            if (config.EnableChapterInsertion)
            {
                Log.Debug("CHAPTER TASK IS STARTING");
                //Run the ChapterEdit task and wait for it to finish before moving on to Image extraction
                chapterExecute = Task.Factory.StartNew(ProcessEpisodeChaptersPoints, cancellationToken);
                chapterExecute.Wait(cancellationToken);

                // If the user has enabled Chapter Insert option and Chapter Image Extraction in the Advanced menu then lets run that process! 
                if (chapterExecute.IsCompleted && config.EnableAutomaticImageExtraction)
                {
                    ProcessChapterImageExtraction();
                }
                //we need to return the Chapter Point Edit Task to close out that the task has completed otherwise the process flags as "Failed"
            }

            return chapterExecute;
        }

        public bool IsHidden => false;

        public bool IsEnabled => true;

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

        public Task ProcessEpisodeChaptersPoints()
        {
            Log.Debug("CHAPTER TASK: STARTING PROCESSEPISODECHAPTERPOINTS() METHOD");
            var config = Plugin.Instance.Configuration;
            //ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            var Repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            QueryResult<TitleSequenceResult> dbResults = Repository.GetResults(new TitleSequenceResultQuery());

            foreach (TitleSequenceResult episode in dbResults.Items)
            {
                if (config.EnableChapterInsertion && episode.HasSequence)
                {
                    long id = episode.InternalId;
                    Log.Debug("CHAPTER TASK: EPISODE ID = {0}", id);
                    ChapterInsertion.Instance.EditChapters(id);
                }
            }

            var repo = Repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }

            return null;
        }

        public Task RefreshChapters()
        {
            var Repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            //ITitleSequenceRepository repo = IntroSkipPluginEntryPoint.Instance.Repository;
            QueryResult<TitleSequenceResult> dbResults = Repository.GetResults(new TitleSequenceResultQuery());
            foreach (TitleSequenceResult episode in dbResults.Items)
            {
                BaseItem item = ItemRepo.GetItemById(episode.InternalId);
                item.RefreshMetadata(CancellationToken.None);
            }

            var repo = Repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
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