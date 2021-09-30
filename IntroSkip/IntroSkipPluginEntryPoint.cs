
using IntroSkip.Data;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Linq;
using System.Threading;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace IntroSkip
{
    public class IntroSkipPluginEntryPoint : IServerEntryPoint
    {
        public static IntroSkipPluginEntryPoint Instance { get; set; }
        private ITitleSequenceRepository Repository { get; set; }
        private static ILibraryManager LibraryManager { get; set; }
        private static ITaskManager TaskManager { get; set; }
        private static IServerConfigurationManager Config { get; set; }
        private static ILogger Logger { get; set; }
        private static IJsonSerializer _json { get; set; }

        //Handling new items added to the library
        private static readonly Timer ItemsAddedTimer = new Timer(AllItemsAdded);
       
        public IntroSkipPluginEntryPoint(ILogManager logManager, IServerConfigurationManager config, IJsonSerializer json, ILibraryManager libraryManager, ITaskManager taskManager)
        {
            _json          = json;
            Config         = config;
            LibraryManager = libraryManager;
            TaskManager    = taskManager;
            Instance       = this;
            Logger         = logManager.GetLogger(Plugin.Instance.Name);
        }

        public void Dispose()
        {
            LibraryManager.ItemAdded -= LibraryManager_ItemAdded;
            
            TaskManager.TaskCompleted -= TaskManagerOnTaskCompleted;
            var repo = Repository as IDisposable;
            repo?.Dispose();
            ItemsAddedTimer.Dispose();
            
        }

        public void Run()
        {
            ItemsAddedTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            LibraryManager.ItemAdded += LibraryManager_ItemAdded;
            
            TaskManager.TaskCompleted += TaskManagerOnTaskCompleted;

            Plugin.Instance.SaveConfiguration();
        }

        private void TaskManagerOnTaskCompleted(object sender, TaskCompletionEventArgs e)
        {
            switch (e.Task.Name)
            {
                //Run the Detection task after fingerprinting
                case "Episode Audio Fingerprinting":
                    if (!Plugin.Instance.Configuration.EnableIntroDetectionAutoRun) return;
                    TaskManager.Execute(
                        TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Title Sequence Detection"),
                        new TaskOptions());
                    break;

                //Run the Chapters after detection
                case "Episode Title Sequence Detection":
                    if (!Plugin.Instance.Configuration.EnableChapterInsertion) return;
                    TaskManager.Execute(
                        TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "IntroSkip Chapter Insertion"),
                        new TaskOptions());
                    break;
            }
            
        }

        
        private void LibraryManager_ItemAdded(object sender, ItemChangeEventArgs e)
        {
            if (!Plugin.Instance.Configuration.EnableItemAddedTaskAutoRun)
            {
                return;
            }

            if (e.Item.GetType().Name != "Episode")
            {
                return;
            }
            
            //if the timer is reset then a new item has been added
            //if the timer goes off, then no new items have been added
            ItemsAddedTimer.Change(10000, Timeout.Infinite); //Wait ten seconds to see if anything else is about to be added

        }
        

        private static async void AllItemsAdded(object state)
        {
            var libraryTask = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Scan media library");
            if (libraryTask?.State == TaskState.Running) //We're not ready for fingerprinting yet.
            {
                ItemsAddedTimer.Change(10000, Timeout.Infinite); //Check back in 10 seconds
                return;
            }

            //Okay, we're ready for fingerprinting now - go ahead.
            ItemsAddedTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.Info("New Items are ready to fingerprint scan...");
            var fingerprint = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Episode Audio Fingerprinting");

            if (fingerprint?.State == TaskState.Running) return;

            try
            {
                await TaskManager.Execute(fingerprint, new TaskOptions()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
        }

        public ITitleSequenceRepository GetRepository()
        {
            var repo = new SqliteTitleSequenceRepository(Logger, Config.ApplicationPaths, _json);

            repo.Initialize();

            return repo;
        }
    }
}
