using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace IntroSkip.TitleSequenceDetection
{
    public class TitleSequenceFileManager : IServerEntryPoint
    {
        private IFileSystem FileSystem                  { get; }
        private IApplicationPaths ApplicationPaths      { get; }
        private IJsonSerializer JsonSerializer          { get; }
        public static TitleSequenceFileManager Instance { get; private set; }
        private ILogger Log                             { get; }
        private char Separator                          { get; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceFileManager(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer json, ILogManager logMan)
        {
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            JsonSerializer   = json;
            Log              = logMan.GetLogger(Plugin.Instance.Name);
            Separator        = FileSystem.DirectorySeparatorChar;
            Instance         = this;
        }

        
        public string GetTitleSequenceDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}titleSequences";
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
        
        public void Dispose()
        {
            
        }

        // ReSharper disable once MethodNameNotMeaningful
        public void Run()
        {
            var titleSequenceDir = GetTitleSequenceDirectory();
            
            if (!FileSystem.DirectoryExists($"{titleSequenceDir}")) FileSystem.CreateDirectory($"{titleSequenceDir}");
            
        }
    }
}
