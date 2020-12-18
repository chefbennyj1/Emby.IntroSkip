using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;

namespace IntroSkip.Events
{
    public class EventManager : IServerEntryPoint
    {
        private IScheduledTask ScheduledTask   { get; }
        private ITaskManager TaskManager       { get; }
        private ILibraryManager LibraryManager { get; }

        public EventManager(ILibraryManager libraryManager, IScheduledTask task, ITaskManager taskManager)
        {
            ScheduledTask  = task;
            TaskManager    = taskManager;
            LibraryManager = libraryManager;
        }

        private void LibraryManager_ItemUpdated(object sender, ItemChangeEventArgs e)
        {
            if (e.Item.GetType().Name != "Episode") return;
            foreach (var task in TaskManager.ScheduledTasks)
            {
                if (task.Name != "Episode Audio Fingerprinting") continue;
                if (task.State == TaskState.Running) return;

                task.TaskProgress += FingerprintingTask_TaskProgress;
                TaskManager.Execute(task, new TaskOptions());
            }
        }

        private void FingerprintingTask_TaskProgress(object sender, MediaBrowser.Model.Events.GenericEventArgs<double> e)
        {
            if (e.Argument < 100.0) return;
            foreach (var task in TaskManager.ScheduledTasks)
            {
                if (task.Name != "Detect Episode Title Sequence") continue;
                if (task.State == TaskState.Running) return;

                TaskManager.Execute(task, new TaskOptions());
            }
        }

        private void LibraryManager_ItemAdded(object sender, ItemChangeEventArgs e)
        {
            if (e.Item.GetType().Name != "Episode") return;
            foreach (var task in TaskManager.ScheduledTasks)
            {
                if (task.Name != "Episode Audio Fingerprinting") continue;
                if (task.State == TaskState.Running) return;

                task.TaskProgress += FingerprintingTask_TaskProgress;
                TaskManager.Execute(task, new TaskOptions());
            }
        }

        public void Dispose()
        {
            LibraryManager.ItemAdded   -= LibraryManager_ItemAdded;
            LibraryManager.ItemUpdated -= LibraryManager_ItemUpdated;
        }

        public void Run()
        {
            LibraryManager.ItemAdded   += LibraryManager_ItemAdded;
            LibraryManager.ItemUpdated += LibraryManager_ItemUpdated;
        }
    }
}
