using System;
using System.Collections.Generic;
using System.IO;
using IntroSkip.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace IntroSkip
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths,
            xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; set; }

        public override Guid Id => new Guid("93A5E794-E0DA-48FD-8D3A-606A20541ED6");

        public override string Name => "Intro Skip";

        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.jpg");
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "IntroSkipConfigurationPage",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.IntroSkipConfigurationPage.html"
                },
                new PluginPageInfo
                {
                    Name = "IntroSkipConfigurationPageJS",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.IntroSkipConfigurationPage.js"
                }
            };
        }
    }
}