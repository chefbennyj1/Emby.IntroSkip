using System;
using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool QuickScan { get; set; } = true;
      public double TitleSequenceLengthThreshold { get; set; } = 10.5;
      public double EncodingLength { get; set; } = 10.0;
    }
}
