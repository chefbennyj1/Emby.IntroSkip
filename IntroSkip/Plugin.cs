using IntroSkip.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;

namespace IntroSkip
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasWebPages
    {
        public static Plugin Instance { get; set; }

        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;

        public override Guid Id => new Guid("93A5E794-E0DA-48FD-8D3A-606A20541ED6");

        public override string Name => "Intro Skip";

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.jpg");
        }

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths,
            xmlSerializer)
        {
            Instance = this;

        }

        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo
            {
                Name = "Chart.js",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.Chart.js",
            },
            new PluginPageInfo
            {
                Name = "IntroSkipConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.IntroSkipConfigurationPage.html",
            },
            new PluginPageInfo
            {
                Name = "IntroSkipConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.IntroSkipConfigurationPage.js"
            },
            new PluginPageInfo
            {
                Name = "AdvancedSettingsConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.AdvancedSettingsConfigurationPage.html",
            },
            new PluginPageInfo
            {
                Name = "AdvancedSettingsConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.AdvancedSettingsConfigurationPage.js"
            },
            new PluginPageInfo
            {
                Name = "ChapterEditorConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.ChapterEditorConfigurationPage.html",
            },
            new PluginPageInfo
            {
                Name = "ChapterEditorConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.ChapterEditorConfigurationPage.js"
            }

        };
    }
}