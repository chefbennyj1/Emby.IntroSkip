using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IntroSkip.AudioFingerprinting;
using IntroSkip.TitleSequenceDetection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace IntroSkip
{
    public class FileManager : IServerEntryPoint
    {
        private IFileSystem FileSystem                  { get; }
        private IApplicationPaths ApplicationPaths      { get; }
        private IJsonSerializer JsonSerializer          { get; }
        public static FileManager Instance              { get; private set; }
        private ILogger Log                             { get; }
        
        private char Separator                          { get; set; }

        // ReSharper disable once TooManyDependencies
        public FileManager(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer json, ILogManager logMan)
        {
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            JsonSerializer   = json;
            Log              = logMan.GetLogger(Plugin.Instance.Name);
            Separator        = FileSystem.DirectorySeparatorChar;
            Instance         = this;
        }
        

        public void SaveFingerPrintToFile(BaseItem episode, AudioFingerprint audioFingerprint)
        {
            var fileName = GetFingerprintFileName(episode);
            var filePath = $"{GetFingerprintDirectory()}{Separator}{fileName}.json";

            if (audioFingerprint is null)
            {
                Log.Info("Fingerprint was null");
                return;
            }

            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(JsonSerializer.SerializeToString(audioFingerprint));
                sw.Flush();
            }
        }

        public string GetFingerprintFileName(BaseItem episode)
        {
            return $"{CreateMD5(episode.Path)}";
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

        public string GetEncodingDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding";
        }

        public string GetFingerprintDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding{Separator}fingerprints";
        }

        public string GetTitleSequenceDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}titleSequences";
        }

        private string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes  = md5.ComputeHash(inputBytes);
                var sb         = new StringBuilder();

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        public TitleSequenceDto GetTitleSequenceFromFile(string fileName)
        {
            var filePath = $"{GetTitleSequenceDirectory()}{Separator}{fileName}.json";

            if (!FileSystem.FileExists(filePath))
            {
                return new TitleSequenceDto();
            }
            
            using (var sr = new StreamReader(filePath))
            {
                return JsonSerializer.DeserializeFromString<TitleSequenceDto>(sr.ReadToEnd());
            }

        }

        public void SaveTitleSequenceJsonToFile(string fileName, TitleSequenceDto introDto)
        {
            Log.Info($"Saving {fileName}.json");
            
            using (var sw = new StreamWriter( $"{GetTitleSequenceDirectory()}{Separator}{fileName}.json"))
            {
                sw.Write(JsonSerializer.SerializeToString(introDto));
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
                    CopyEmbeddedResourceStream(stream, location, "fpcalc");
                }
            }

            if (OperatingSystem.IsMacOS())
            {
                if (!FileSystem.FileExists($"{location}{Separator}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("mac_fpcalc");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc");
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (!FileSystem.FileExists($"{location}{Separator}fpcalc.exe"))
                {
                    var stream = GetEmbeddedResourceStream("fpcalc.exe");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc.exe");
                }
            }
        }

        private void CopyEmbeddedResourceStream(Stream stream, string location, string fileName)
        {
            var fileStream = new FileStream($"{location}{Separator}{fileName}", FileMode.CreateNew);
            for (int i = 0; i < stream.Length; i++)
                fileStream.WriteByte((byte)stream.ReadByte());
            fileStream.Close();
        }

        private Stream GetEmbeddedResourceStream(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name     = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceName));
            return GetType().Assembly.GetManifestResourceStream(name);
        }

        public void Dispose()
        {
            
        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {
            
            var titleSequenceDir = GetTitleSequenceDirectory();
            var encodingDir      = GetEncodingDirectory();
            var fingerPrintDir   = GetFingerprintDirectory();
            
            if (!FileSystem.DirectoryExists($"{titleSequenceDir}")) FileSystem.CreateDirectory($"{titleSequenceDir}");

            if (!FileSystem.DirectoryExists($"{encodingDir}"))      FileSystem.CreateDirectory( $"{encodingDir}");

            if (!FileSystem.DirectoryExists($"{fingerPrintDir}"))   FileSystem.CreateDirectory( $"{fingerPrintDir}");

            CopyFpCalc($"{encodingDir}");

        }
    }
}
