
﻿using MediaBrowser.Controller.Library;

using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
 using IntroSkip.Configuration;
 using IntroSkip.Data;
 using IntroSkip.TitleSequence;
using MediaBrowser.Model.Querying;
 using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;


namespace IntroSkip.Chapters
{
    public class ChapterEditScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private ILibraryManager LibraryManager { get; set; }
        private ITaskManager TaskManager { get; }
        private IItemRepository ItemRepo { get; }
        private ILogger Log { get; set; }

        public ChapterEditScheduledTask(ILibraryManager libraryManager, ITaskManager taskManager, ILogManager logManager, IItemRepository itemRepo)
        {
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
            ItemRepo = itemRepo;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var repository     = IntroSkipPluginEntryPoint.Instance.GetRepository();
            var dbResults      = repository.GetResults(new TitleSequenceResultQuery());
            var titleSequences = dbResults.Items.ToList();
            Log.Info($"Title Sequences contains: {titleSequences.Count} items.");
            var config         = Plugin.Instance.Configuration;
            
            if (config.EnableChapterInsertion)
            {
                Log.Debug("CHAPTER TASK IS STARTING");
                
                //Run the ChapterEdit task and wait for it to finish before moving on to Image extraction
                ProcessEpisodeChaptersPoints(titleSequences, config, progress);
                
                progress.Report(100.0);
                //If the chapter task is completed and there are errors in the episodes lets output the error file.
                if (ChapterInsertion.Instance.ChapterErrors != null)
                {
                    ChapterErrorTextFileCreator.Instance.JotErrorFilePaths();
                }

                // If the user has enabled Chapter Insert option and Chapter Image Extraction in the Advanced menu then lets run that process! 
                if (config.EnableAutomaticImageExtraction)
                {
                    var thumbnail = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Thumbnail image extraction");
                    try
                    {
                        TaskManager.Execute(thumbnail, new TaskOptions());
                        //await Task.FromResult(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(ex.Message);
                    }
                }
            }
            else
            {
                Log.Debug("CHAPTER TASK: FAILED - You may need to enable this in the Plugin Configuration");
            }

            var repo = repository as IDisposable;
            repo.Dispose();
            
            
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

        private void ProcessEpisodeChaptersPoints(List<TitleSequenceResult> titleSequences, PluginConfiguration config, IProgress<double> progress)
        {
            Log.Debug("CHAPTER TASK: STARTING PROCESSEPISODECHAPTERPOINTS() METHOD");

            try
            {
                ChapterInsertion.Instance.ChapterErrors?.Clear();
            }
            catch { }
            var step = 100.0 / titleSequences.Count;
            var currentProgress = 0.1;

            foreach (TitleSequenceResult episode in titleSequences)
            {
                if (!config.EnableChapterInsertion || !episode.HasSequence) continue;
                long id = episode.InternalId;
                Log.Debug("CHAPTER TASK: EPISODE ID = {0}", id);
                ChapterInsertion.Instance.InsertIntroChapters(id, episode);
                progress.Report((currentProgress += step) - 1);
            }
        }

        //public Task RefreshChapters()
        //{
        //    var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
            
        //    QueryResult<TitleSequenceResult> dbResults = repository.GetResults(new TitleSequenceResultQuery());
        //    foreach (TitleSequenceResult episode in dbResults.Items)
        //    {
        //        BaseItem item = ItemRepo.GetItemById(episode.InternalId);
        //        item.RefreshMetadata(CancellationToken.None);
        //    }

        //    var repo = repository as IDisposable;
        //    repo?.Dispose();

        //    return null;
        //}

        //private async void ProcessChapterImageExtraction()
        //{
        //    //var chapterEdit = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "IntroSkip Chapter Insertion");
        //    var thumbnail = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Thumbnail image extraction");

        //    await TaskManager.Execute(thumbnail, new TaskOptions());
            
        //}
    }
}