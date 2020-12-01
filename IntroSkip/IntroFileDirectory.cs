using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;

namespace IntroSkip
{
    public class IntroFileDirectory :IServerEntryPoint
    {
        private IFileSystem FileSystem { get; set; }
        public static IntroFileDirectory Instance { get; private set; }
        
        private const string EncodingDir = "../programdata/IntroEncodings/";
        
        public IntroFileDirectory(IFileSystem file)
        {
            FileSystem = file;
            Instance = this;
        }


        public void MaintainIntroEncodingDirectory()
        {
            if (!FileSystem.DirectoryExists(EncodingDir))
            {
                FileSystem.CreateDirectory(EncodingDir);
            }
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

        public void Dispose()
        {
            
        }

        public void Run()
        {
           
        }
    }
}
