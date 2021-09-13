using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public int TitleSequenceLengthThreshold         { get; set; } = 10;        
        public int MaxDegreeOfParallelism               { get; set; } = 2;
        public int FingerprintingMaxDegreeOfParallelism { get; set; } = 5;
        public bool EnableItemAddedTaskAutoRun          { get; set; }
        public int Version { get; set; } = 0;
        public int? Limit { get; set; } = null;
        public bool EnableAutomaticImageExtraction { get; internal set; }
    }
}
