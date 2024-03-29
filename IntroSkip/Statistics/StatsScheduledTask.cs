
using MediaBrowser.Controller.Library;

using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;


namespace IntroSkip.Statistics
{
    public class StatsScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private ILibraryManager LibraryManager { get; set; }
        private ITaskManager TaskManager { get; }
        private ILogger Log { get; }

        public StatsScheduledTask(ILibraryManager libraryManager, ITaskManager taskManager, ILogManager logManager)
        {
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            Log.Debug("STATISTICS: TASK IS STARTING");
            
            IScheduledTaskWorker detection = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Title Sequence Detection");
            IScheduledTaskWorker fingerprinting = TaskManager.ScheduledTasks.FirstOrDefault(t => t.Name == "Episode Audio Fingerprinting");

            if (detection?.State == TaskState.Running || fingerprinting?.State == TaskState.Running)
            {
                Log.Info("STATS: Task will wait until chroma-printing task,and detection task has completed.");
                progress.Report(100.0);
                return;
            }
            try
            {
                StatsManager.Instance.GetDetectionStatistics();
                await Task.FromResult(true);
                progress.Report(100.0);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex.Message, ex);
            }
        }

        public bool IsHidden => false;

        public bool IsEnabled => true;
        
        public bool IsLogged => true;

        public string Name => "IntroSkip Statistics";

        public string Key => "IntroSkip Statistics Options";

        public string Description => "Run Detection Success Statistics";

        public string Category => "Intro Skip";



        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type          = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks,
                    TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
                }
            };
        }
    }
}