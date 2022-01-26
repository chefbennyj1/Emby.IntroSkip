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
        public class ExtractThumbImage : IReturn<string>
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
        public class NoTitleSequenceThumbImageRequest : IReturn<string> { }

        [Route("/NoCreditSequenceThumbImage", "GET", Summary = "No Credit Sequence Thumb Image")]
        public class NoCreditSequenceThumbImageRequest : IReturn<string> { }

       

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
            IHttpResultFactory resultFactory, IFfmpegManager ffmpegManager, IServerApplicationPaths paths, IFileSystem file)
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

        public string Get(ExtractThumbImage request)
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
                    requestFrame += TimeSpan.FromSeconds(7); //<--push the image frame so it isn't always a black screen.
                    break;
                case SequenceImageType.CreditEnd:
                case SequenceImageType.IntroEnd:
                    requestFrame -= TimeSpan.FromSeconds(7); //<--back up the image frame so it isn't always a black screen.
                    break;
            }

            var config = Plugin.Instance.Configuration;

            var cache = GetCacheDirectory();
            var imageFile = GetHashString($"{item.InternalId}{request.SequenceImageType}");

            //We have enabled the the image cache
            if (config.ImageCache)
            {
                //We have the image in the cache
                if (CacheImageExists(imageFile))
                {
                    Log.Debug("Returning thumb images from cache.");

                    using (var sr = new StreamReader(Path.Combine(cache, imageFile)))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }

            var frame = $"{requestFrame.Hours}:{requestFrame.Minutes}:{requestFrame.Seconds}";
            
            //Get the extracted frame using FFmpeg. 
            //If the cache is enabled, but we don't have the image yet, return the image stream
            //If the cache is disabled, return the image stream
            //var args = $"-accurate_seek -ss {frame} -threads 1 -copyts -i \"{item.Path}\" -an -vf \"scale=trunc(min(max(iw\\,ih*dar)\\,min(175\\,0*dar))/2)*2:trunc(min(max(iw/dar\\,ih)\\,min(175/dar\\,0))/2)*2,thumbnail=24\" -vsync 0 -f image2pipe -";
            var args = $"-accurate_seek -ss {frame} -i \"{item.Path}\" -frames 1 -f image2pipe -s 175x100 pipe:1";
            var procStartInfo = new ProcessStartInfo(ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                Stream output;

                using (var process = new Process {StartInfo = procStartInfo})
                {
                    process.Start();
                    output = process.StandardOutput.BaseStream;
                    //process.WaitForExit(2000);
                }

                using (var memoryStream = new MemoryStream())
                {
                    output.CopyTo(memoryStream);
                    var base64Image = Convert.ToBase64String(memoryStream.ToArray());
                    
                    if (!config.ImageCache) return base64Image;
                    //Update the saved base64 image
                    using(var sw = new StreamWriter(Path.Combine(cache, imageFile), append: false))
                    {
                        sw.Write(base64Image);
                    }

                    return base64Image;

                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
                return "R0lGODlhAQABAIAAAAUEBAAAACwAAAAAAQABAAACAkQBADs=";
            }
            //ResultFactory.GetResult(Request, output, "image/png");

        }
       
        public string Get(NoTitleSequenceThumbImageRequest request)
        {
            var img = GetEmbeddedResourceStream("no_intro.jpg".AsSpan(), "image/png");
            using (var memoryStream = new MemoryStream())
            {
                img.CopyTo(memoryStream);
                return Convert.ToBase64String(memoryStream.ToArray());

            }
        }
        
        public string Get(NoCreditSequenceThumbImageRequest request)
        {
            var img = GetEmbeddedResourceStream("no_credit.jpg".AsSpan(), "image/png");
            using (var memoryStream = new MemoryStream())
            {
                img.CopyTo(memoryStream);
                return Convert.ToBase64String(memoryStream.ToArray());

            }
        }
        

        private Stream GetEmbeddedResourceStream(ReadOnlySpan<char> resourceName, string contentType)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNameAsString = resourceName.ToString();
            var name = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceNameAsString));

            return GetType().Assembly.GetManifestResourceStream(name);
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

        public void RemoveCacheImages(long internalId, SequenceImageType imageType)
        {
            var cache = GetCacheDirectory();
            var titleSequenceStartImageFile  = GetHashString($"{internalId}{SequenceImageType.IntroStart}");
            var titleSequenceEndImageFile    = GetHashString($"{internalId}{SequenceImageType.IntroEnd}");
            var creditSequenceStartImageFile = GetHashString($"{internalId}{SequenceImageType.CreditStart}");

            switch (imageType)
            {
                case SequenceImageType.IntroStart:
                    try
                    {
                        FileSystem.DeleteFile(Path.Combine(cache, titleSequenceStartImageFile));
                    }
                    catch { }

                    break;

                case SequenceImageType.IntroEnd:
                    try
                    {
                        FileSystem.DeleteFile(Path.Combine(cache, titleSequenceEndImageFile));
                    }
                    catch { }

                    break;

                case SequenceImageType.CreditStart:
                    try
                    {
                        FileSystem.DeleteFile(Path.Combine(cache, creditSequenceStartImageFile));
                    }
                    catch { }
                    break;
            }
        }

        public void UpdateImageCache(long internalId, SequenceImageType sequenceImageType, string frame)
        {
            var item = LibraryManager.GetItemById(internalId);

            var imageFile = GetHashString($"{item.InternalId}{sequenceImageType}");

            var cache = GetCacheDirectory();

            var ffmpegConfiguration = FfmpegManager.FfmpegConfiguration;
            var ffmpegPath = ffmpegConfiguration.EncoderPath;
           
            var args = $"-accurate_seek -ss {frame} -i \"{item.Path}\" -frames 1 -f image2pipe -s 175x100 pipe:1";
            var procStartInfo = new ProcessStartInfo(ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                Stream output;

                using (var process = new Process {StartInfo = procStartInfo})
                {
                    process.Start();
                    output = process.StandardOutput.BaseStream;
                    //process.WaitForExit(2000);
                }

                using (var memoryStream = new MemoryStream())
                {
                    output.CopyTo(memoryStream);
                    using (var sw = new StreamWriter(Path.Combine(cache, imageFile), false))
                    {
                        sw.Write(Convert.ToBase64String(memoryStream.ToArray()));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
            }
        }
        
    }
}