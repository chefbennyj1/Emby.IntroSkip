using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Cryptography;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace IntroSkip
{
    public class TitleSequenceEncoding : IServerEntryPoint
    {
        private IFileSystem FileSystem               { get; }
        private IApplicationPaths ApplicationPaths   { get; }
        public string FolderPathDelimiter            { get; set; }
        private IJsonSerializer JsonSerializer       { get; }
        public static TitleSequenceEncoding Instance { get; private set; }
        private ILogger Log                          { get; }
        public string EncodingDir                    { get; private set; }
        private ICryptoProvider CryptoProvider       { get; }
        private string TitleSequenceDir              { get; set; }

        public string FingerPrintDir                 { get; private set; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceEncoding(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer json, ILogManager logMan, ICryptoProvider cryptoProvider)
        {
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            JsonSerializer   = json;
            Log              = logMan.GetLogger(Plugin.Instance.Name);
            CryptoProvider = cryptoProvider;
            Instance         = this;
        }
        
        public  string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (MD5 md5 = MD5.Create())
            {
                byte[] inputBytes = Encoding.ASCII.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        public void MigrateCurrentFingerPrints(string fingerprintFile, BaseItem episode)
        {
            if (FileSystem.FileExists(fingerprintFile))
            {
                var hash = CreateMD5(episode.Path);
                FileSystem.CopyFile(fingerprintFile, $"{FingerPrintDir}{FileSystem.DirectorySeparatorChar}{hash}.json", true);
                try
                {
                    FileSystem.DeleteFile(fingerprintFile);

                } catch{}
            }
        }

        public TitleSequenceDto GetTitleSequenceFromFile(string fileName)
        {
            
            var filePath = $"{TitleSequenceDir}{FileSystem.DirectorySeparatorChar}{fileName}.json";

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
            
            using (var sw = new StreamWriter( $"{TitleSequenceDir}{FileSystem.DirectorySeparatorChar}{fileName}.json"))
            {
                sw.Write(JsonSerializer.SerializeToString(introDto));
                sw.Flush();
            }
        }

        private void CopyFpCalc(string location)
        {
            if (OperatingSystem.IsLinux())
            {
                if (!FileSystem.FileExists($"{location}{FileSystem.DirectorySeparatorChar}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("linux_fpcalc");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc");
                }
            }

            if (OperatingSystem.IsMacOS())
            {
                if (!FileSystem.FileExists($"{location}{FileSystem.DirectorySeparatorChar}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("mac_fpcalc");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc");
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (!FileSystem.FileExists($"{location}{FileSystem.DirectorySeparatorChar}fpcalc.exe"))
                {
                    var stream = GetEmbeddedResourceStream("fpcalc.exe");
                    CopyEmbeddedResourceStream(stream, location, "fpcalc.exe");
                }
            }
        }

        private void CopyEmbeddedResourceStream(Stream stream, string location, string fileName)
        {
            var fileStream = new FileStream($"{location}{FileSystem.DirectorySeparatorChar}{fileName}", FileMode.CreateNew);
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
            var configDir = ApplicationPaths.PluginConfigurationsPath;

            TitleSequenceDir = $"{configDir}{FileSystem.DirectorySeparatorChar}TitleSequences";
            EncodingDir      = $"{configDir}{FileSystem.DirectorySeparatorChar}IntroEncoding";
            FingerPrintDir   = $"{configDir}{FileSystem.DirectorySeparatorChar}IntroEncoding{FileSystem.DirectorySeparatorChar}Fingerprints";

            
            if (!FileSystem.DirectoryExists($"{TitleSequenceDir}")) FileSystem.CreateDirectory($"{TitleSequenceDir}");

            if (!FileSystem.DirectoryExists($"{EncodingDir}"))      FileSystem.CreateDirectory( $"{EncodingDir}");

            if (!FileSystem.DirectoryExists($"{FingerPrintDir}"))   FileSystem.CreateDirectory( $"{FingerPrintDir}");

            CopyFpCalc($"{EncodingDir}");

        }
    }
}
