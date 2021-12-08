using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class SequenceThumbnailService : IService, IHasResultFactory
    {
        public enum SequenceImageType
        {
            IntroStart  = 0,
            IntroEnd    = 1,
            CreditStart = 2,
            CreditEnd   = 3
        }
        [Route("/ExtractThumbImage", "GET", Summary = "Image jpg resource frame")]
        public class ExtractThumbImage : IReturn<object>
        {
            [ApiMember(Name = "ImageFrameTimestamp", Description = "The image frame time stamp to extract from the stream", IsRequired = true, DataType = "string", ParameterType = "query", Verb = "GET")]
            public string ImageFrameTimestamp { get; set; }

            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true, DataType = "long[]", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }

            [ApiMember(Name = "SequenceImageType", Description = "IntroStart = 0, IntroEnd = 1, CreditStart = 2, CreditEnd = 3", IsRequired = true, DataType = "SequenceImageType", ParameterType = "query", Verb = "GET")]
            public SequenceImageType SequenceImageType { get; set; }
            
            public object Img { get; set; }
        }

        [Route("/NoTitleSequenceThumbImage", "GET", Summary = "No Title Sequence Thumb Image")]
        public class NoTitleSequenceThumbImageRequest : IReturn<object>
        {

        }

        private ILogger Log { get; }
        private ILibraryManager LibraryManager { get; }
        public IHttpResultFactory ResultFactory { get; set; }
        private IFfmpegManager FfmpegManager { get; }
        public IRequest Request { get; set; }
     

        // ReSharper disable once TooManyDependencies
        public SequenceThumbnailService(ILogManager logMan, ILibraryManager libraryManager, IHttpResultFactory resultFactory, IFfmpegManager ffmpegManager)
        {
            Log = logMan.GetLogger(Plugin.Instance.Name);
            LibraryManager = libraryManager;
            ResultFactory = resultFactory;
            FfmpegManager = ffmpegManager;
        }

        public async Task<object> Get(ExtractThumbImage request)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
            var item = LibraryManager.GetItemById(request.InternalId);
            var requestFrame = TimeSpan.Parse(request.ImageFrameTimestamp);
            switch (request.SequenceImageType)
            {
                case SequenceImageType.CreditStart:
                    break;
                case SequenceImageType.IntroStart:
                    requestFrame +=
                        TimeSpan.FromSeconds(7); //<--push the image frame so it isn't always a black screen.
                    break;
                case SequenceImageType.CreditEnd:
                case SequenceImageType.IntroEnd:
                    requestFrame -=
                        TimeSpan.FromSeconds(7); //<--back up the image frame so it isn't always a black screen.
                    break;
            }

            var frame = $"{requestFrame.Hours}:{requestFrame.Minutes}:{requestFrame.Seconds}";
            var args =
                $"-accurate_seek -ss {frame} -i \"{item.Path}\" -vcodec mjpeg -vframes 1 -an -f rawvideo -s 175x100 -";
            var procStartInfo = new ProcessStartInfo(ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            FileStream output;

            using (var process = new Process {StartInfo = procStartInfo})
            {
                process.Start();
                output = await Task.Factory.StartNew(() => process.StandardOutput.BaseStream as FileStream);
            }

            return ResultFactory.GetResult(Request, output, "image/png");
        }

        public async Task<object> Get(NoTitleSequenceThumbImageRequest request) =>
            await Task<object>.Factory.StartNew(() => GetEmbeddedResourceStream("no_intro.png".AsSpan(), "image/png"));

        private object GetEmbeddedResourceStream(ReadOnlySpan<char> resourceName, string contentType)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNameAsString = resourceName.ToString();
            var name = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceNameAsString));

            return ResultFactory.GetResult(Request, GetType().Assembly.GetManifestResourceStream(name), contentType);
        }
    }
}
