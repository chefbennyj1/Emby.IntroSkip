using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using IntroSkip.Data;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.IO;

namespace IntroSkip
{
    public class IntroSkipPluginEntryPoint : IServerEntryPoint
    {
        public static IntroSkipPluginEntryPoint Instance { get; private set; }
        private ISequenceRepository Repository { get; set; }
        private static ILibraryManager LibraryManager { get; set; }
        private static ITaskManager TaskManager { get; set; }
        private static IServerConfigurationManager Config { get; set; }
        private static User User { get; set; }
        private static ILogger Logger { get; set; }
        private static IJsonSerializer _json { get; set; }
        private IFileSystem FileSystem { get; set; }
       
       
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
            TaskManager.TaskCompleted -= TaskManagerOnTaskCompleted;
            var repo = Repository as IDisposable;
            repo?.Dispose();

        }

        //private static Timer NewItemAddedTimer = new Timer(RunFingerprinting);
        private IServerConfigurationManager GetConfig()
        {
            return Config.GetConfiguration<IServerConfigurationManager>("system");
            //system.xml <ShowIntroDetectionScheduledTask>false</ShowIntroDetectionScheduledTask>
            //Folder options.xml
            //<EnableMarkerDetection>false</EnableMarkerDetection>
            //<EnableMarkerDetectionDuringLibraryScan> false </EnableMarkerDetectionDuringLibraryScan>

        }
        public void Run()
        {
            TaskManager.TaskCompleted += TaskManagerOnTaskCompleted;
            LibraryManager.ItemRemoved += LibraryManagerItemRemoved;
            Plugin.Instance.UpdateConfiguration(Plugin.Instance.Configuration);
            
        }

        private void LibraryManagerItemRemoved(object sender, ItemChangeEventArgs e)
        {
            if (e.Item.GetType().Name != nameof(Episode)) return;
            try
            {
                var repository = GetRepository();
                repository.Delete(e.Item.InternalId.ToString());
                var repo = repository as IDisposable;
                repo.Dispose();
            }
            catch{}

            
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

                //Run the Fingerprinting task after each library scan, in case new items have been added.
                case "Scan media library":
                    if (!Plugin.Instance.Configuration.EnableItemAddedTaskAutoRun) return;
                    try
                    {
                        TaskManager.Execute(
                            TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Audio Fingerprinting"),
                            new TaskOptions());
                    }
                    catch { } //If this task is already running, we'll catch the error

                    break;

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
