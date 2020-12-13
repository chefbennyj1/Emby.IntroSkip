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
    public class IntroServerEntryPoint : IServerEntryPoint
    {
        // ReSharper disable once TooManyDependencies
        public IntroServerEntryPoint(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer json,
            ILogManager logMan)
        {
            FileSystem = file;
            ApplicationPaths = applicationPaths;
            JsonSerializer = json;
            Log = logMan.GetLogger(Plugin.Instance.Name);
            Instance = this;
        }

        private IFileSystem FileSystem { get; }
        private IApplicationPaths ApplicationPaths { get; }
        public string FolderPathDelimiter { get; set; }
        private IJsonSerializer JsonSerializer { get; }
        public static IntroServerEntryPoint Instance { get; private set; }
        private ILogger Log { get; }

        public void Dispose()
        {
        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            if (!FileSystem.DirectoryExists($"{configDir}{FileSystem.DirectorySeparatorChar}TitleSequences"))
                FileSystem.CreateDirectory($"{configDir}{FileSystem.DirectorySeparatorChar}TitleSequences");

            if (!FileSystem.DirectoryExists($"{configDir}{FileSystem.DirectorySeparatorChar}IntroEncoding"))
                FileSystem.CreateDirectory($"{configDir}{FileSystem.DirectorySeparatorChar}IntroEncoding");

            CopyFpCalc($"{configDir}{FileSystem.DirectorySeparatorChar}IntroEncoding");
        }

        public TitleSequenceDto GetTitleSequenceFromFile(long seriesId, long seasonId)
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            var filePath =
                $"{configDir}{FileSystem.DirectorySeparatorChar}TitleSequences{FileSystem.DirectorySeparatorChar}{seriesId}{seasonId}.json";

            if (!FileSystem.FileExists(filePath)) return new TitleSequenceDto();

            using (var sr = new StreamReader(filePath))
            {
                return JsonSerializer.DeserializeFromString<TitleSequenceDto>(sr.ReadToEnd());
            }
        }

        public void SaveTitleSequenceJsonToFile(long seriesId, long seasonId, TitleSequenceDto introDto)
        {
            Log.Info($"Saving {seriesId}{seasonId}.json");

            var configDir = ApplicationPaths.PluginConfigurationsPath;
            using (var sw =
                new StreamWriter(
                    $"{configDir}{FileSystem.DirectorySeparatorChar}TitleSequences{FileSystem.DirectorySeparatorChar}{seriesId}{seasonId}.json")
            )
            {
                sw.Write(JsonSerializer.SerializeToString(introDto));
                sw.Flush();
            }
        }

        private void CopyFpCalc(string location)
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;

            if (OperatingSystem.IsLinux())
            {
                if (FileSystem.FileExists($"{configDir}{FileSystem.DirectorySeparatorChar}fpcalc"))
                    FileSystem.DeleteFile($"{configDir}{FileSystem.DirectorySeparatorChar}fpcalc");

                if (!FileSystem.FileExists($"{location}{FileSystem.DirectorySeparatorChar}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("linux_fpcalc");
                    var fileStream = new FileStream($"{location}{FileSystem.DirectorySeparatorChar}fpcalc",
                        FileMode.CreateNew);
                    for (var i = 0; i < stream.Length; i++) fileStream.WriteByte((byte) stream.ReadByte());
                    fileStream.Close();
                }
            }

            if (OperatingSystem.IsMacOs())
            {
                if (FileSystem.FileExists($"{configDir}{FileSystem.DirectorySeparatorChar}fpcalc"))
                    FileSystem.DeleteFile($"{configDir}{FileSystem.DirectorySeparatorChar}fpcalc");

                if (!FileSystem.FileExists($"{location}{FileSystem.DirectorySeparatorChar}fpcalc"))
                {
                    var stream = GetEmbeddedResourceStream("mac_fpcalc");
                    var fileStream = new FileStream($"{location}{FileSystem.DirectorySeparatorChar}fpcalc",
                        FileMode.CreateNew);
                    for (var i = 0; i < stream.Length; i++) fileStream.WriteByte((byte) stream.ReadByte());
                    fileStream.Close();
                }
            }

            if (OperatingSystem.IsWindows())
            {
                if (FileSystem.FileExists($"{configDir}{FileSystem.DirectorySeparatorChar}fpcalc.exe"))
                    FileSystem.DeleteFile($"{configDir}{FileSystem.DirectorySeparatorChar}fpcalc.exe");

                if (!FileSystem.FileExists($"{location}{FileSystem.DirectorySeparatorChar}fpcalc.exe"))
                {
                    var stream = GetEmbeddedResourceStream("fpcalc.exe");
                    var fileStream = new FileStream($"{location}{FileSystem.DirectorySeparatorChar}fpcalc.exe",
                        FileMode.CreateNew);
                    for (var i = 0; i < stream.Length; i++) fileStream.WriteByte((byte) stream.ReadByte());
                    fileStream.Close();
                }
            }
        }

        private Stream GetEmbeddedResourceStream(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames()
                .Single(s => s.EndsWith(resourceName));

            return GetType()
                .Assembly.GetManifestResourceStream(name);
        }
    }
}