using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class TitleSequenceChapterService : IService
    {
        public TitleSequenceChapterService()
        {

        }

        //[Route("/ScanSeries", "POST", Summary = "Remove Episode Title Sequence Start and End Data")]
        //public class ScanSeriesRequest : IReturnVoid
        //{
        //    [ApiMember(Name = "InternalIds", Description = "Comma delimited list Internal Ids of the series to scan", IsRequired = true, DataType = "long[]", ParameterType = "query", Verb = "POST")]
        //    public long[] InternalIds { get; set; }
        //}
    }
}
