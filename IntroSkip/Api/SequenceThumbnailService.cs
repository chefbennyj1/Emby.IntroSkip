using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Services;

namespace IntroSkip.Api
{
    public class SequenceThumbnailService : IService, IHasResultFactory
    {
        public enum SequenceImageType
        {
            IntroStart = 0,
            IntroEnd = 1,
            CreditStart = 2,
            CreditEnd = 3
        }

        [Route("/ExtractThumbImage", "GET", Summary = "Image jpg resource frame")]
        public class ExtractThumbImage : IReturn<object>
        {
            [ApiMember(Name = "ImageFrameTimestamp",
                Description = "The image frame time stamp to extract from the stream", IsRequired = true,
                DataType = "string", ParameterType = "query", Verb = "GET")]
            public string ImageFrameTimestamp { get; set; }

            [ApiMember(Name = "InternalId", Description = "The episode internal Id", IsRequired = true,
                DataType = "long", ParameterType = "query", Verb = "GET")]
            public long InternalId { get; set; }

            [ApiMember(Name = "SequenceImageType",
                Description = "IntroStart = 0, IntroEnd = 1, CreditStart = 2, CreditEnd = 3", IsRequired = true,
                DataType = "object", ParameterType = "query", Verb = "GET")]
            public SequenceImageType SequenceImageType { get; set; }

        }

        [Route("/NoTitleSequenceThumbImage", "GET", Summary = "No Title Sequence Thumb Image")]
        public class NoTitleSequenceThumbImageRequest : IReturn<object> { }

        private ILogger Log { get; }
        private ILibraryManager LibraryManager { get; }
        public IHttpResultFactory ResultFactory { get; set; }
        private IFfmpegManager FfmpegManager { get; }
        private IServerApplicationPaths AppPaths { get; set; }
        private IFileSystem FileSystem { get; set; }
        public IRequest Request { get; set; }
        public static SequenceThumbnailService Instance { get; set; }

        // ReSharper disable once TooManyDependencies
        public SequenceThumbnailService(ILogManager logMan, ILibraryManager libraryManager,
            IHttpResultFactory resultFactory, IFfmpegManager ffmpegManager, IServerApplicationPaths paths,
            IFileSystem file)
        {
            Log = logMan.GetLogger(Plugin.Instance.Name);
            LibraryManager = libraryManager;
            ResultFactory = resultFactory;
            FfmpegManager = ffmpegManager;
            AppPaths = paths;
            FileSystem = file;
            Instance = this;
            CreateImageCacheDirectoryIfNotExist();
        }

        public async Task<object> Get(ExtractThumbImage request)
        {
            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
            var item = LibraryManager.GetItemById(request.InternalId);
            var canExtract = TimeSpan.TryParse(request.ImageFrameTimestamp, out var requestFrame);
            if (!canExtract)
            {
                Log.Debug($"Error extracting thumb image time span: {request.ImageFrameTimestamp}");
            }
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

            var config = Plugin.Instance.Configuration;

            

            //We have enabled the the image cache
            if (config.ImageCache)
            {
                //If we are caching, this will be the file path.
                var cache = GetCacheDirectory();
                var imageFile = GetHashString($"{item.InternalId}{request.SequenceImageType}");

               
                //We have the image in the cache
                if (CacheImageExists(imageFile))
                {
                    Log.Debug("Returning thumb images from cache.");
                    
                    return ResultFactory.GetResult(Request, new FileStream(Path.Combine(cache, imageFile), FileMode.Open), "image/jpeg");
                }
            }

            var frame = $"{requestFrame.Hours}:{requestFrame.Minutes}:{requestFrame.Seconds}";

            //If we have gotten this far with ImageCache enabled, then we don't have a copy of the image in the cache. 
            //Now we have to run ffmpeg process to save the image
            if (config.ImageCache) UpdateImageCache(item.InternalId, request.SequenceImageType, frame);

            //Get the extracted frame using FFmpeg. 
            //If the cache is enabled, but we don't have the image yet, return the image stream
            //If the cache is disabled, return the image stream
           
            var args = $"-accurate_seek -ss {frame} -i \"{item.Path}\" -vframes 1 -f image2pipe -s 175x100 -";
            var procStartInfo = new ProcessStartInfo(ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Stream output;
           
            //Stream error;
            using (var process = new Process {StartInfo = procStartInfo})
            {
                process.Start();
                process.ErrorDataReceived += Process_ErrorDataReceived;
                output = process.StandardOutput.BaseStream;
                //error = process.StandardError.BaseStream;
                
            }
            
            //using (var sr = new StreamReader(error))
            //{
            //    //Log.Debug(await sr.ReadToEndAsync());
            //}

            return await Task.Factory.StartNew(() => ResultFactory.GetResult(Request, output, "image/jpeg"));
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log.Warn(e.Data);
        }

        public async Task<object> Get(NoTitleSequenceThumbImageRequest request) =>
            await Task<object>.Factory.StartNew(() => GetEmbeddedResourceStream("no_intro.jpg".AsSpan(), "image/jpg"));

        private object GetEmbeddedResourceStream(ReadOnlySpan<char> resourceName, string contentType)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNameAsString = resourceName.ToString();
            var name = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceNameAsString));

            return ResultFactory.GetResult(Request, GetType().Assembly.GetManifestResourceStream(name), contentType);
        }

        private bool CacheImageExists(string fileName)
        {
            var cache = GetCacheDirectory();
            return FileSystem.FileExists(Path.Combine(cache, fileName));
        }

        private void CreateImageCacheDirectoryIfNotExist()
        {
            var cache = GetCacheDirectory();
            if (!FileSystem.DirectoryExists(cache)) FileSystem.CreateDirectory(cache);
        }

        private string GetCacheDirectory()
        {
            return Path.Combine(AppPaths.DataPath, "introcache");
        }

        private byte[] GetHash(string inputString)
        {
            using (HashAlgorithm algorithm = SHA256.Create())
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        private string GetHashString(string inputString)
        {
            var sb = new StringBuilder();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        public void RemoveCacheImages(long internalId)
        {
            var cache = GetCacheDirectory();
            var titleSequenceStartImageFile = GetHashString($"{internalId}{SequenceImageType.IntroStart}");
            var titleSequenceEndImageFile = GetHashString($"{internalId}{SequenceImageType.IntroEnd}");
            var creditSequenceStartImageFile = GetHashString($"{internalId}{SequenceImageType.CreditStart}");

            try
            {
                FileSystem.DeleteFile(Path.Combine(cache, titleSequenceStartImageFile));
            }
            catch { }

            try
            {
                FileSystem.DeleteFile(Path.Combine(cache, titleSequenceEndImageFile));
            }
            catch { }

            try
            {
                FileSystem.DeleteFile(Path.Combine(cache, creditSequenceStartImageFile));
            }
            catch { }
        }

        public void UpdateImageCache(long internalId, SequenceImageType sequenceImageType, string frame)
        {
            var item = LibraryManager.GetItemById(internalId);

            var imageFile = GetHashString($"{item.InternalId}{sequenceImageType}");

            var cache = GetCacheDirectory();

            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
           
            var arguments = $"-accurate_seek -ss {frame} -i \"{item.Path}\" -vcodec mjpeg -vframes 1 -an -f rawvideo -s 175x100 \"{Path.Combine(cache, imageFile)}\"";
            var processStartInfo = new ProcessStartInfo(ffmpegPath, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var process = new Process {StartInfo = processStartInfo})
            {
                process.Start();
            }


        }
        
    }
}