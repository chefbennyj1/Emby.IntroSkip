using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public int TitleSequenceLengthThreshold { get; set; } = 10;
        public int EncodingLength               { get; set; } = 10;
        public int MaxDegreeOfParallelism       { get; set; } = 2;
        public bool EnableItemAddedTaskAutoRun  { get; set; }
    }
}
