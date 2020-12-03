using System.Runtime.InteropServices;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;

namespace IntroSkip
{
    public class IntroServerEntryPoint : IServerEntryPoint
    {
        private IFileSystem FileSystem { get; }
        public IApplicationPaths ApplicationPaths { get; }
        public string FolderPathDelimiter { get; set; }
        
        public static IntroServerEntryPoint Instance { get; private set; }
        
        public IntroServerEntryPoint(IFileSystem file, IApplicationPaths applicationPaths)
        {
            FileSystem = file;
            ApplicationPaths = applicationPaths;
            Instance = this;
        }

        
        
        public void AudioFileCleanUp(string audio_file_2, string audio_file_1 = null)
        {
            //If we had a success scan for episodes we can keep audio_file_1 so we don;t have to encode it again.
            if (!(audio_file_1 is null))
            {
                if (FileSystem.FileExists(audio_file_1))
                {
                    FileSystem.DeleteFile(audio_file_1);
                }
            }

            if (FileSystem.FileExists(audio_file_2))
            {
                FileSystem.DeleteFile(audio_file_2);
            }
        }

        private static bool IsUnix()
        {
            var isUnix = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
                         RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            return isUnix;
        }
    


        public void Dispose()
        {
            
        }

        public void Run()
        {
            
        }
    }
}
