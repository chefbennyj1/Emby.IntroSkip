using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace IntroSkip.AudioFingerprinting
{
    public class AudioFingerprintFileManager : IServerEntryPoint
    {
        private IFileSystem FileSystem                     { get; }
        private IApplicationPaths ApplicationPaths         { get; }
        private char Separator                             { get; }
        private ILogger Log                                { get; }
        public static AudioFingerprintFileManager Instance { get; private set; }
        private IJsonSerializer JsonSerializer             { get; }

        // ReSharper disable once TooManyDependencies
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
        
        public string GetFingerprintFileNameHash(BaseItem episode)
        {
            return $"{CreateMD5(episode.Path)}";
        }

        public string GetFingerprintDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}introEncoding{Separator}fingerprints";
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
            var fileName = GetFingerprintFileNameHash(episode);
            var filePath = $"{GetFingerprintDirectory()}{Separator}{fileName}.json";
            
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
            for (int i = 0; i < stream.Length; i++) fileStream.WriteByte((byte)stream.ReadByte());
            fileStream.Close();
        }

        private Stream GetEmbeddedResourceStream(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name     = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceName));
            return GetType().Assembly.GetManifestResourceStream(name);
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
