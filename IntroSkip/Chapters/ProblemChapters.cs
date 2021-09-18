using System;

namespace IntroSkip.Chapters
{
    public class ProblemChapters
    {
        public Guid ShowId { get; set; }
        public string ShowName { get; set; }
        public Guid SeasonId { get; set; }
        public string SeasonName { get; set; }
        public int? EpisodeIndex { get; set; }
        public Guid EpisodeId { get; set; }
        public string EpisodeName { get; set; }
        public int ChaptersNos { get; set; }
    }
}
