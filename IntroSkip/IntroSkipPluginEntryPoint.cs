using Emby.AutoOrganize.Data;
using IntroSkip.AudioFingerprinting;
using IntroSkip.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Linq;
using System.Threading;

namespace IntroSkip
{
    public class IntroSkipPluginEntryPoint : IServerEntryPoint
    {
        public static IntroSkipPluginEntryPoint Instance { get; set; }
        public ITitleSequenceRepository Repository {get; set;}
        private ILibraryManager LibraryManager { get; set; }
        private ITaskManager TaskManager { get; set; }
        private IServerConfigurationManager _config { get; set; }
        private ILogger _logger {get; set;}
        private IJsonSerializer _json { get; set; }

        private readonly int CurrentVersion = 3;

        public IntroSkipPluginEntryPoint(ILogger logger, IServerConfigurationManager config, IJsonSerializer json, ILibraryManager libraryManager, ITaskManager taskManager)
        {
            _logger = logger;
            _json = json;
            _config = config;
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Instance = this;
        }

        public void Dispose()
        {
            var repo = Repository as IDisposable;
            if (repo != null)
            {
                repo.Dispose();
            }
        }

        public void Run()
        {
                    

            try
            {
                Repository = GetRepository();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error initializing title sequence database", ex);
            }         
             
            _logger.Info("Database loaded Sucessfully...");

            LibraryManager.ItemAdded += LibraryManager_ItemAdded;

        }

        private async void LibraryManager_ItemAdded(object sender, ItemChangeEventArgs e)
        {
            var fingerprint = TaskManager.ScheduledTasks.FirstOrDefault(task => task.Name == "Episode Audio Fingerprinting");
            if(fingerprint.State != TaskState.Running)
            {
                try
                {
                    await fingerprint.ScheduledTask.Execute(new CancellationToken(), new Progress<double>()).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private ITitleSequenceRepository GetRepository()
        {
            var repo = new SqliteTitleSequenceRepository(_logger, _config.ApplicationPaths, _json);

            repo.Initialize();

            return repo;
        }
    }
}
