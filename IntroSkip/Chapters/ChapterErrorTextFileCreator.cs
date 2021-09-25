using System.IO;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;

namespace IntroSkip.Chapters
{
    public class ChapterErrorTextFileCreator : IServerEntryPoint
    {
        private IFileSystem FileSystem { get; }
        private IApplicationPaths ApplicationPaths { get; }
        private char Separator { get; }
        private ILogger Log { get; }
        public static ChapterErrorTextFileCreator Instance { get; set; }
        public ChapterErrorTextFileCreator(IFileSystem file, IApplicationPaths applicationPaths, ILogManager logMan)
        {
            Instance = this;
            FileSystem = file;
            ApplicationPaths = applicationPaths;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            Separator = FileSystem.DirectorySeparatorChar;
        }

        public string GetChapterErrorDir()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}ChapterError";
        }

        public Task JotErrorFilePaths()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            Log.Debug("CHAPTER ERRORS TEXT FILE: Writing each episode to xml file");
            var errors = ChapterInsertion.Instance.ChapterErrors;
            var filePath = $"{configDir}{Separator}ChapterError{Separator}ChapterErrorList.txt";

            //FileStream stream;

            if (errors == null)
            {
                Log.Info("CHAPTER ERRORS TEXT FILE: NOTHING TO WRITE TO THE FILE");
            }
            else
            {
                foreach (var error in errors)
                {
                    var path = error.FilePathString;
                    using (StreamWriter writer = new StreamWriter(filePath, true))
                    {
                        Log.Debug("CHAPTER ERRORS TEXT FILE: FilePath = {0}", path);
                        writer.WriteLine(path);
                    }
                }
            }
            return null;
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
