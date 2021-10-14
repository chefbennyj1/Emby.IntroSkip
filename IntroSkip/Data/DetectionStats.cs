﻿using System;
using System.Collections.Generic;
using System.Text;
using IntroSkip.Chapters;
using IntroSkip.TitleSequence;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;

namespace IntroSkip.Data
{
    public class DetectionStats
    {
        public long SeasonId            { get; set; }
        public string TVShowName        { get; set; }
        public string Season            { get; set; }
        public int EpisodeCount         { get; set; }
        public double PercentDetected   { get; set; }
        public DateTime Date            { get; set; }
        public bool HasIssue            { get; set; }
    }
}
