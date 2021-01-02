using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

// ReSharper disable once TooManyDependencies
// ReSharper disable three TooManyChainedReferences
// ReSharper disable twice ComplexConditionExpression

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceDetectionScheduledTask : IScheduledTask, IConfigurableScheduledTask
    {
        private static ILogger Log                  { get; set; }

        public TitleSequenceDetectionScheduledTask(ILogManager logManager)
        {
            Log = logManager.GetLogger(Plugin.Instance.Name);
        }

 
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
 
        {
            Log.Info("Beginning Title Sequence Task");

            TitleSequenceDetectionManager.Instance.Analyze(cancellationToken, progress);
            await Task.FromResult(true);
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

        public string Name        => "Detect Episode Title Sequence";
        public string Key         => "Intro Skip Options";
        public string Description => "Detect start and finish times of episode title sequences to allow for a 'skip' option";
        public string Category    => "Intro Skip";
        public bool IsHidden      => false;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;

    }
}

