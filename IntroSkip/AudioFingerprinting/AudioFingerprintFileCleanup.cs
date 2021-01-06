using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintFileCleanup : IScheduledTask, IConfigurableScheduledTask
    {
        private ILibraryManager LibraryManager { get; }
        private IUserManager UserManager       { get; }
        private IFileSystem FileSystem         { get; }

        // ReSharper disable once TooManyDependencies
        public AudioFingerprintFileCleanup(ILibraryManager libraryManager, IUserManager user, IFileSystem file)
        {
            LibraryManager = libraryManager;
            UserManager = user;
            FileSystem = file;
        }
        // ReSharper disable twice TooManyChainedReferences
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var episodes = new List<string>();
            var seriesQuery = LibraryManager.QueryItems(new InternalItemsQuery()
            {
                Recursive = true,
                IncludeItemTypes = new[] { "Series" },
                User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator)
            });

            foreach (var series in seriesQuery.Items)
            {
                var episodeQuery = LibraryManager.QueryItems(new InternalItemsQuery()
                {
                    Parent = series,
                    Recursive = true,
                    IncludeItemTypes = new[] { "Episode" },
                    User = UserManager.Users.FirstOrDefault(user => user.Policy.IsAdministrator),
                    IsVirtualItem = false

                });

                episodes.AddRange(episodeQuery.Items.Select(episode => AudioFingerprintFileManager.Instance.GetFingerprintFileName(episode)));
            }

            var fingerprintDirectory = AudioFingerprintFileManager.Instance.GetFingerprintDirectory() +
                                       FileSystem.DirectorySeparatorChar;

            var files = FileSystem.GetFiles(fingerprintDirectory, true);
            foreach (var file in files)
            {
                if (episodes.Exists(e => $"{e}.json" == file.Name)) continue;
                FileSystem.DeleteFile(file.FullName);
            }

            await Task.FromResult(true);
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

        public string Name        => "Clean up fingerprinting files";
        public string Key         => "Intro Skip Options";
        public string Description => "Clean up fingerprinting files from items which have been removed from the library";
        public string Category    => "Intro Skip";
        public bool IsHidden      => true;
        public bool IsEnabled     => true;
        public bool IsLogged      => true;
    }
}
