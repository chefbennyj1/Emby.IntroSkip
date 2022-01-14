using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace IntroSkip.Chapters
{
    public class ChapterErrorTextFileCreator : IServerEntryPoint
    {
        private IFileSystem FileSystem { get; }
        private IApplicationPaths ApplicationPaths { get; }
        private ILogger Log { get; }
        private static ChapterErrorTextFileCreator Instance { get; set; }
        public ChapterErrorTextFileCreator(IFileSystem file, IApplicationPaths applicationPaths, ILogManager logMan)
        {
            Instance = this;
            FileSystem = file;
            ApplicationPaths = applicationPaths;
            Log = logMan.GetLogger(Plugin.Instance.Name);
        }

        private string GetChapterErrorDir()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return Path.Combine(configDir, "IntroSkipInfo");
        }

        public void JotErrorFilePaths()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            Log.Debug("CHAPTER ERRORS TEXT FILE: Writing each episode to xml file");

            var errors = ChapterInsertion.Instance.ChapterErrors;
            var filePath = Path.Combine(configDir, "IntroSkipInfo", "ChapterErrorList.txt");
           

            if (errors == null)
            {
                Log.Info("CHAPTER ERRORS TEXT FILE: NOTHING TO WRITE TO THE FILE");
            }
            else
            {
                using (StreamWriter writer = new StreamWriter(filePath, false))
                {
                    Log.Debug("Saving Chapter data.");
                    foreach (var error in errors)
                    {
                        var path = error.FilePathString;
                   
                        //Log.Debug("CHAPTER ERRORS TEXT FILE: FilePath = {0}", path);
                        writer.WriteLine(path);
                    }
                }
            }
        }

        #region IServerEntryPoint Implemented Members

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void Run()
        {
            var errorDir = GetChapterErrorDir();
            if (!FileSystem.DirectoryExists($"{errorDir}")) FileSystem.CreateDirectory($"{errorDir}");
        }

        #endregion
    }
}