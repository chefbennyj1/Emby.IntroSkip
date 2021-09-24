using Emby.AutoOrganize.Data;
using IntroSkip.Data;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace IntroSkip
{
    public class IntroSkipPluginEntryPoint : IServerEntryPoint
    {
        public static IntroSkipPluginEntryPoint Instance { get; set; }
        private ITitleSequenceRepository Repository { get; set; }
        private ILibraryManager LibraryManager { get; set; }
        private static ITaskManager TaskManager { get; set; }
        private static IServerConfigurationManager Config { get; set; }
        private static ILogger Logger { get; set; }
        private static IJsonSerializer _json { get; set; }

        
        //Handling new items added to the library
        private static readonly Timer ItemsAddedTimer = new Timer(AllItemsAdded);
        private static readonly Timer ItemsRemovedTimer = new Timer(AllItemsRemoved);
        private static readonly List<long> ItemsRemoved = new List<long>();
    
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
            LibraryManager.ItemRemoved -= LibraryManager_ItemRemoved;
            var repo = Repository as IDisposable;
            repo?.Dispose();
        }

        public void Run()
        {
            ItemsAddedTimer.Change(Timeout.Infinite, Timeout.Infinite);
            ItemsRemovedTimer.Change(Timeout.Infinite, Timeout.Infinite);

            LibraryManager.ItemAdded += LibraryManager_ItemAdded;
            LibraryManager.ItemRemoved += LibraryManager_ItemRemoved;
            TaskManager.TaskCompleted += TaskManagerOnTaskCompleted;

            Plugin.Instance.SaveConfiguration();
        }

        private void TaskManagerOnTaskCompleted(object sender, TaskCompletionEventArgs e)
        {
            switch (e.Task.Name)
            {
                //Run the Detection task after fingerprinting
                case "Episode Audio Fingerprinting":
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


        private void LibraryManager_ItemRemoved(object sender, ItemChangeEventArgs e)
        {
            ItemsRemovedTimer.Change(10000, Timeout.Infinite);
            var item = e.Item;
            if (item.GetType().Name != "Episode")
            {
                return;
            }

            ItemsRemoved.Add(e.Item.InternalId); //Add the removed Item to the list of items being removed.
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
            ItemsAddedTimer.Change(10000, Timeout.Infinite); //Reset the timer because we just got a new episode item. 10 secs

        }

        private static void AllItemsRemoved(object state)
        {
            Logger.Info("Items removed from library... syncing database.");
            ItemsRemovedTimer.Change(Timeout.Infinite, Timeout.Infinite);
            var repository = Instance.GetRepository();
            foreach (var item in ItemsRemoved)
            {
                try
                {
                    //This item may not exist in the database is if the user removes a items from their library
                    //They have opted out of auto scanning
                    //They have not manually run the fingerprinting task
                    
                    repository.Delete(item.ToString());

                } catch {}
            }
            ItemsRemoved.Clear();
            repository.Vacuum();
            var repo = repository as IDisposable;
            repo?.Dispose();
        }

        private static async void AllItemsAdded(object state)
        {
            ItemsAddedTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.Info("New Items are ready to fingerprint scan...");
            var fingerprint = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Episode Audio Fingerprinting");

            if (fingerprint?.State == TaskState.Running) return;

            try
            {
                await TaskManager.Execute(fingerprint, new TaskOptions());
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
