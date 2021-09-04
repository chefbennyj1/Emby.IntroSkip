using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;

// ReSharper disable twice TooManyChainedReferences
// ReSharper disable once TooManyDependencies

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintFileManager : IServerEntryPoint
    {
        private IFileSystem FileSystem                     { get; }
        private IApplicationPaths ApplicationPaths         { get; }
        private char Separator                             { get; }
        private ILogger Log                                { get; }
        public static AudioFingerprintFileManager Instance { get; private set; }
                
        public AudioFingerprintFileManager(IFileSystem file, IApplicationPaths applicationPaths,  ILogManager logMan)
        {
            Instance         = this;
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            Log              = logMan.GetLogger(Plugin.Instance.Name);
            Separator        = FileSystem.DirectorySeparatorChar;
        }

        public void RemoveEpisodeFingerprintBinFile(string path, BaseItem item)
        {
            if (!FileSystem.FileExists(path)) return;
            try
            {
                FileSystem.DeleteFile(path);
                Log.Debug($"{item.Parent.Parent.Name} - S:{item.Parent.IndexNumber} - E:{item.IndexNumber}: .bin file removed.");
            }
            catch { }
        }
            
       
        public string GetEncodingDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding";
        }

        public void Dispose()
        {
            
        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {            
            var encodingDir      = GetEncodingDirectory();
            if (!FileSystem.DirectoryExists($"{encodingDir}")) FileSystem.CreateDirectory( $"{encodingDir}");            
        }
        

    }
}
