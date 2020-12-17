using System;
using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool QuickScan { get; set; } = true;
        public int TitleSequenceLengthThreshold { get; set; } = 10;
        public int EncodingLength { get; set; } = 10;
    }
}
