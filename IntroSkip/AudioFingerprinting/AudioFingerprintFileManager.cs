using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

// ReSharper disable twice TooManyChainedReferences
// ReSharper disable once TooManyDependencies

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintFileManager : FileManagerHelper, IServerEntryPoint
    {
        private IFileSystem FileSystem                     { get; }
        private IApplicationPaths ApplicationPaths         { get; }
        private char Separator                             { get; }
        private ILogger Log                                { get; }
        public static AudioFingerprintFileManager Instance { get; private set; }
        private IJsonSerializer JsonSerializer             { get; }

        
        public AudioFingerprintFileManager(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer jsonSerializer,  ILogManager logMan)
        {
            Instance         = this;
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            Log              = logMan.GetLogger(Plugin.Instance.Name);
            JsonSerializer   = jsonSerializer;
            Separator        = FileSystem.DirectorySeparatorChar;
        }

        public void RemoveAllSeasonAudioEncodings(long internalId)
        {
            var introEncodingPath  = $"{GetEncodingDirectory()}{Separator}";
            var files              = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();

            if (!fileSystemMetadata.Any()) return;

            foreach (var file in fileSystemMetadata)
            {
                if (file.Name.Substring(0, internalId.ToString().Length) != internalId.ToString()) continue;
                Log.Info($"Removing encoding file {file.FullName}");
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }
        }

        public void RemoveAllAudioEncodings()
        {
            var introEncodingPath  = $"{GetEncodingDirectory()}{Separator}";
            var files              = FileSystem.GetFiles(introEncodingPath, true).Where(file => file.Extension == ".wav");
            var fileSystemMetadata = files.ToList();

            if (!fileSystemMetadata.Any()) return;
            Log.Info("Removing all encoding files");
            foreach (var file in fileSystemMetadata)
            {
                try
                {
                    FileSystem.DeleteFile(file.FullName);
                }
                catch { }
            }
        }

        public string GetFingerprintFileName(BaseItem episode)
        {
            var season = episode.Parent;
            var series = season.Parent;
            var fileName = $"{series.Name} {season.IndexNumber}x{episode.IndexNumber} {episode.DateCreated.Date:yy-MM-dd}";
            var pattern = "[\\~#%&*{}/:<>?|\"]";
            var regEx = new Regex(pattern);
            return Regex.Replace(regEx.Replace(fileName, ""), @"\s+", " ");
        }

        public string GetFingerprintDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding{Separator}fingerprints";
        }

        public string GetFingerprintFolderName(BaseItem episode)
        {
            var series = episode.Parent.Parent;
            var productionYear = series.ProductionYear is null ? string.Empty : $" ({series.ProductionYear})"; 
            var pattern        = "[\\~#%&*{}/:<>?|\"]";
            var regEx          = new Regex(pattern);
            var fileName       = $"{series.Name}{productionYear}";
            
            return Regex.Replace(regEx.Replace(fileName, ""), @"\s+", " ");
        }

        public AudioFingerprintDto GetSavedFingerPrintFromFile(string filePath)
        {
            using (var sr = new StreamReader(filePath))
            {
                return JsonSerializer.DeserializeFromString<AudioFingerprintDto>(sr.ReadToEnd());
            }
        }

        public void SaveFingerPrintToFile(BaseItem episode, AudioFingerprintDto fingerprintDto)
        {
            var folder = GetFingerprintFolderName(episode);
            var savePath   = $"{GetFingerprintDirectory()}{Separator}{folder}{Separator}";

            if (!FileSystem.DirectoryExists(savePath))
            {
                FileSystem.CreateDirectory(savePath);
            }

            var fileName = GetFingerprintFileName(episode);
            var filePath = $"{savePath}{Separator}{fileName}.json";
            
            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(JsonSerializer.SerializeToString(fingerprintDto));
                sw.Flush();
            }
        }

        private void CopyFpCalc(string location)
        {
            if (OperatingSystem.IsLinux())
            {
                if (!FileSystem.FileExists($"{location}{Separator}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("linux_fpcalc");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc", Separator);
                }
            }

            if (OperatingSystem.IsMacOS())
            {
                if (!FileSystem.FileExists($"{location}{Separator}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("mac_fpcalc");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc", Separator);
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (!FileSystem.FileExists($"{location}{Separator}fpcalc.exe"))
                {
                    var stream = GetEmbeddedResourceStream("fpcalc.exe");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc.exe", Separator);
                }
            }
        }

        

        //private string CreateMD5(string input)
        //{
        //    // Use input string to calculate MD5 hash
        //    if (string.IsNullOrEmpty(input))
        //    {
        //        Log.Info("Create MD5 string is null");
        //        return string.Empty;
        //    }
        //    using (var md5 = MD5.Create())
        //    {
        //        var inputBytes = Encoding.ASCII.GetBytes(input);
        //        var hashBytes  = md5.ComputeHash(inputBytes);
        //        var sb         = new StringBuilder();

        //        for (int i = 0; i < hashBytes.Length; i++)
        //        {
        //            sb.Append(hashBytes[i].ToString("X2"));
        //        }
        //        return sb.ToString();
        //    }
        //}

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
            var fingerPrintDir   = GetFingerprintDirectory();
            var encodingDir      = GetEncodingDirectory();

            if (!FileSystem.DirectoryExists($"{encodingDir}")) FileSystem.CreateDirectory( $"{encodingDir}");

            if (!FileSystem.DirectoryExists($"{fingerPrintDir}")) FileSystem.CreateDirectory($"{fingerPrintDir}");
            CopyFpCalc($"{encodingDir}"); 
        }
        

    }
}
