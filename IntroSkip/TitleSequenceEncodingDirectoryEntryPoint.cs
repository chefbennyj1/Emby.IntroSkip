using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace IntroSkip
{
    public class TitleSequenceEncodingDirectoryEntryPoint : IServerEntryPoint
    {
        private IFileSystem FileSystem               { get; }
        private IApplicationPaths ApplicationPaths   { get; }
        public string FolderPathDelimiter            { get; set; }
        private IJsonSerializer JsonSerializer       { get; }
        public static TitleSequenceEncodingDirectoryEntryPoint Instance { get; private set; }
        private ILogger Log                          { get; }
        public string EncodingDir                    { get; private set; }
        private string TitleSequenceDir              { get; set; }
        public string FingerPrintDir                 { get; private set; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceEncodingDirectoryEntryPoint(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer json, ILogManager logMan)
        {
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            JsonSerializer   = json;
            Log              = logMan.GetLogger(Plugin.Instance.Name);
            Instance         = this;
        }
        
        public TitleSequenceDto GetTitleSequenceFromFile(long seriesId, long seasonId)
        {
            var filePath = $"{TitleSequenceDir}{FileSystem.DirectorySeparatorChar}{seriesId}{seasonId}.json";

            if (!FileSystem.FileExists(filePath))
            {
                return new TitleSequenceDto();
            }
            
            using (var sr = new StreamReader(filePath))
            {
                return JsonSerializer.DeserializeFromString<TitleSequenceDto>(sr.ReadToEnd());
            }

        }

        public void SaveTitleSequenceJsonToFile(long seriesId, long seasonId, TitleSequenceDto introDto)
        {
            Log.Info($"Saving {seriesId}{seasonId}.json");
            
            using (var sw = new StreamWriter( $"{TitleSequenceDir}{FileSystem.DirectorySeparatorChar}{seriesId}{seasonId}.json"))
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
