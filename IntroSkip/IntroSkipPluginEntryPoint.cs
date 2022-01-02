using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Data;
using IntroSkip.ScheduledTasks;
using IntroSkip.Sequence;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.IO;

namespace IntroSkip
{
    public class IntroSkipPluginEntryPoint : IServerEntryPoint
    {
        public static IntroSkipPluginEntryPoint Instance { get; set; }
        private ISequenceRepository Repository { get; set; }
        private static ILibraryManager LibraryManager { get; set; }
        private static ITaskManager TaskManager { get; set; }
        private static IServerConfigurationManager Config { get; set; }
        private static ILogger Logger { get; set; }
        private static IJsonSerializer _json { get; set; }
        private IFileSystem FileSystem { get; set; }
        //Handling new items added to the library
        private static readonly Timer ItemsAddedTimer = new Timer(AllItemsAdded);
       
        public IntroSkipPluginEntryPoint(ILogManager logManager, IServerConfigurationManager config, IJsonSerializer json, ILibraryManager libraryManager, ITaskManager taskManager, IFileSystem fileSystem)
        {
            _json          = json;
            Config         = config;
            LibraryManager = libraryManager;
            TaskManager    = taskManager;
            FileSystem     = fileSystem;
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

            LibraryManager.ItemUpdated += LibraryManager_ItemUpdated;

            Plugin.Instance.UpdateConfiguration(Plugin.Instance.Configuration);
        }

        private void LibraryManager_ItemUpdated(object sender, ItemChangeEventArgs e)
        {
            
            return;



            //var repository = GetRepository();
            //var dbResults = repository.GetBaseTitleSequenceResults(new SequenceResultQuery() {SeasonInternalId = e.Item.InternalId});
            //foreach (var result in dbResults.Items)
            //{
            //    try
            //    {
            //        repository.Delete(result.InternalId.ToString());
            //    }
            //    catch
            //    {

            //    }
            //}
            //ItemsAddedTimer.Change(10000, Timeout.Infinite);
        }

        private void TaskManagerOnTaskCompleted(object sender, TaskCompletionEventArgs e)
        {
            switch (e.Task.Name)
            {
                //Run the Detection task after fingerprinting
                case "Episode Audio Fingerprinting":
                    if (!Plugin.Instance.Configuration.EnableIntroDetectionAutoRun) return;
                    TaskManager.Execute(TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Title Sequence Detection"),
                        new TaskOptions());
                    break;

                //Run the Chapters after detection
                case "Episode Title Sequence Detection":
                    if (!Plugin.Instance.Configuration.EnableChapterInsertion) return;
                    TaskManager.Execute(TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "IntroSkip Chapter Insertion"),
                            new TaskOptions());
                    break;

                ////Run the Fingerprinting task after each library scan
                //case "Scan media library":
                //    try
                //    {
                //        TaskManager.Execute(
                //            TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Audio Fingerprinting"),
                //            new TaskOptions());
                //    }catch {} //If this task is already running, we'll catch the error

                //    break;

            }
        }

        
        private void LibraryManager_ItemAdded(object sender, ItemChangeEventArgs e)
        {
            if (!Plugin.Instance.Configuration.EnableItemAddedTaskAutoRun)
            {
                return;
            }

            Logger.Debug("Fingerprinting Task is ready to scan new items...");

            if (e.Item.GetType().Name != "Episode")
            {
                return;
            }
            
            //if the timer is reset then a new item has been added
            //if the timer goes off, then we are ready to scan new items
            ItemsAddedTimer.Change(5000, Timeout.Infinite); //Wait 5 seconds to see if anything else is about to be added

        }
        

        private static async void AllItemsAdded(object state)
        {
            var libraryTask   = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Scan media library");
            var detectionTask = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Title Sequence Detection");
            var fingerprintingTask = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Audio Fingerprinting");
            
            if (libraryTask?.State == TaskState.Running || fingerprintingTask?.State == TaskState.Running || detectionTask?.State == TaskState.Running) //We're not ready for fingerprinting yet.
            {
                Logger.Debug("Fingerprinting Task will wait for library scan to complete.");
                ItemsAddedTimer.Change(5000, Timeout.Infinite ); //Check back in 5 seconds
                return;
            }

            //Okay, we're ready for fingerprinting now - go ahead.
            ItemsAddedTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.Info("New Items are ready to fingerprint...");
            

            try
            {
                await TaskManager.Execute(fingerprintingTask, new TaskOptions()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex.Message);
            }
        }

        public ISequenceRepository GetRepository()
        {
            var repo = new SqliteSequenceRepository(Logger, Config.ApplicationPaths, _json, FileSystem);

            repo.Initialize();
            
            return repo;
        }
    }
}
