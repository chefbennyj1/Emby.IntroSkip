using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkip.Data;

// ReSharper disable once TooManyDependencies
// ReSharper disable three TooManyChainedReferences
// ReSharper disable twice ComplexConditionExpression

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log { get; set; }
        private ITaskManager TaskManager { get; set; }
        public TitleSequenceDetectionScheduledTask(ILogManager logManager, ITaskManager taskManager)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
            TaskManager = taskManager;
        }


        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var tasks = TaskManager.ScheduledTasks.ToList();
            if (tasks.FirstOrDefault(task => task.Name == "Episode Audio Fingerprinting").State == TaskState.Running)
            {
                Log.Info("Title sequence task will wait until chroma-printing task has completed.");
                progress.Report(100.0);
                return;
            }



            Log.Info("Beginning Title Sequence Task");
            try
            {
                var repository = IntroSkipPluginEntryPoint.Instance.GetRepository();
                TitleSequenceDetectionManager.Instance.Analyze(cancellationToken, progress, repository);
                await Task.FromResult(true);
                var repo = repository as IDisposable;
                repo?.Dispose();

            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
            }

            progress.Report(100.0);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public string Name => "Episode Title Sequence Detection";
        public string Key => "Intro Skip Options";
        public string Description => "Detect start and finish times of episode title sequences to allow for a 'skip' option";
        public string Category => "Intro Skip";
        public bool IsHidden => false;
        public bool IsEnabled => true;
        public bool IsLogged => true;

    }
}
