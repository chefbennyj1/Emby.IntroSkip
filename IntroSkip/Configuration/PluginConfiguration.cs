using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool FastDetect { get; set; } = true;
        public int TitleSequenceLengthThreshold         { get; set; } = 10; //Default
        public int HammingDistanceThreshold             { get; set; } = 8; //Default
        public int MaxDegreeOfParallelism               { get; set; } = 2; //Default
        
        public int FingerprintingMaxDegreeOfParallelism { get; set; } = 2; //Default
        public bool EnableItemAddedTaskAutoRun          { get; set; } = false;
        public bool EnableIntroDetectionAutoRun         { get; set; } = false;
        public bool EnableChapterInsertion              { get; set; }  //give the user the option to insert the chapter points into their library.
        public bool EnableAutomaticImageExtraction      { get; set; } //give the user the option to automatically run Thumbnail image extraction process after the Chapter Points are created.

        public List<long> IgnoredList                   { get; set; }

        public bool EnableEndCreditChapterInsertion     { get; set; } = false;

        public int Version                              { get; set; } = 0;
        public int? Limit                               { get; set; } = null;

    }
}