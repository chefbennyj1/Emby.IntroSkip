﻿using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace IntroSkip.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool FastDetect                          { get; set; } = false;
        public int TitleSequenceLengthThreshold         { get; set; } = 10; //Default
        public int HammingDistanceThreshold             { get; set; } = 8; //Default
        public int MaxDegreeOfParallelism               { get; set; } = 2; //Default
        public double DetectionConfidence               { get; set; } = 0.60; //Default
        
        public int FingerprintingMaxDegreeOfParallelism { get; set; } = 2; //Default
        public double BlackDetectionPixelThreshold      { get; set; } = 0.00; //Default
        public double BlackDetectionSecondIntervals     { get; set; } = 0.05; //Default
        public bool EnableItemAddedTaskAutoRun          { get; set; } = false;
        public bool EnableIntroDetectionAutoRun         { get; set; } = false;
        public bool EnableChapterInsertion              { get; set; }  //give the user the option to insert the chapter points into their library.
        public bool EnableAutomaticImageExtraction      { get; set; }  //give the user the option to automatically run Thumbnail image extraction process after the Chapter Points are created.
        public List<long> IgnoredList                   { get; set; }
        public bool EnableEndCreditChapterInsertion     { get; set; } = false;
        public bool EnableFullStatistics                { get; set; } = false;
        public int Version                              { get; set; } = 0;
        public int? Limit                               { get; set; } = null;
        public bool ImageCache { get; set; } = false;
        
        //AUTO SKIP
        public bool EnableAutoSkipCreditSequence          { get; set; }
        public bool EnableAutoSkipTitleSequence           { get; set; }
        public bool ShowAutoTitleSequenceSkipMessage      { get; set; } = true;
        public long? AutoTitleSequenceSkipMessageDuration { get; set; } = 800L;
        public bool EnableAutoSkipEndCreditSequence       { get; set; }
        public List<string> AutoSkipUsers                 { get; set; }
        public bool IgnoreEpisodeOneTitleSequenceSkip     { get; set; } = false;
        public string AutoSkipLocalization                { get; set; } = "English";
        public int? AutoSkipDelay                         { get; set; }


        //Core IntroSkip Features
        public bool DisableCoreIntroSkip { get; set; } = false;
        public bool DisableCoreTask { get; set; } = true;
        public PluginConfiguration()
        {

        }
    }
}