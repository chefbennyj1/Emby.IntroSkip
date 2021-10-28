using System;

namespace IntroSkip.Statistics
{
    public class DetectionStats
    {
        public long SeasonId            { get; set; }
        public string TVShowName        { get; set; }
        public string Season            { get; set; }
        public int EpisodeCount         { get; set; }
        public int HasSeqCount          { get; set; }
        public double PercentDetected   { get; set; }
        public double EndPercentDetected { get; set; }
        public DateTime Date            { get; set; }
        public bool HasIssue            { get; set; }
        public TimeSpan IntroDuration   { get; set; }
        public string Comment           { get; set; }
    }
}
