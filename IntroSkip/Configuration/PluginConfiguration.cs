using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public int TitleSequenceLengthThreshold         { get; set; } = 10;        
        public int MaxDegreeOfParallelism               { get; set; } = 4;
        public int FingerprintingMaxDegreeOfParallelism { get; set; } = 5;
        public bool EnableItemAddedTaskAutoRun          { get; set; }
        public bool EnableChapterInsertion { get; set; }  //give the user the option to insert the chapter points into their library.
        public bool EnableAutomaticImageExtraction      { get; set; }  //give the user the option to automatically run Thumbnail image extraction process after the Chapter Points are created.
        
        public int Version { get; set; } = 0;
    }
}
