using System.IO;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Serialization;

namespace IntroSkip.TitleSequence
{
    public class TitleSequenceFileManager : FileManagerHelper, IServerEntryPoint
    {
        private IFileSystem FileSystem                  { get; }
        private IApplicationPaths ApplicationPaths      { get; }
        private IJsonSerializer JsonSerializer          { get; }
        public static TitleSequenceFileManager Instance { get; private set; }
        //private ILogger Log                             { get; }
        private char Separator                          { get; }

        // ReSharper disable once TooManyDependencies
        public TitleSequenceFileManager(IFileSystem file, IApplicationPaths applicationPaths, IJsonSerializer json)
        {
            FileSystem       = file;
            ApplicationPaths = applicationPaths;
            JsonSerializer   = json;
            //Log              = logMan.GetLogger(Plugin.Instance.Name);
            Separator        = FileSystem.DirectorySeparatorChar;
            Instance         = this;
        }
        private static string GetTitleSequenceFolderName(BaseItem series)
        {
            var productionYear = series.ProductionYear is null ? string.Empty : $" ({series.ProductionYear})"; 
            var pattern        = "[\\~#%&*{}/:<>?|\"]";
            var regEx          = new Regex(pattern);
            var fileName       = $"{series.Name}{productionYear}";
            
            return Regex.Replace(regEx.Replace(fileName, ""), @"\s+", " ");
        }
        public string GetTitleSequenceDirectory()
        {
            var configDir = ApplicationPaths.PluginConfigurationsPath;
            return $"{configDir}{Separator}titleSequences";
        }
        
        public TitleSequenceDto GetTitleSequenceFromFile(BaseItem series)
        {
            var fileName = GetTitleSequenceFolderName(series);
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

        public void SaveTitleSequenceJsonToFile(BaseItem series, TitleSequenceDto introDto)
        {
            var fileName = GetTitleSequenceFolderName(series);
            var filePath = $"{GetTitleSequenceDirectory()}{Separator}{fileName}.json";
            
            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(JsonSerializer.SerializeToString(introDto));
                sw.Flush();
            }
        }

        public void RemoveSeriesTitleSequenceData(BaseItem series)
        {
            if (FileSystem.FileExists($"{GetTitleSequenceDirectory()}{Separator}{GetTitleSequenceFolderName(series)}.json"))
            {
                FileSystem.DeleteFile($"{GetTitleSequenceDirectory()}{Separator}{GetTitleSequenceFolderName(series)}.json");
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
