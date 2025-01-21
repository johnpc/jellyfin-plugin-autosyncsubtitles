using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AutoSyncSubtitles.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AutoSyncSubtitles
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly AutoSyncSubtitlesManager _autosyncManager;

        public Plugin(
            IServerApplicationPaths appPaths,
            IXmlSerializer xmlSerializer,
            ILibraryManager libraryManager,
            ILoggerFactory loggerFactory)
            : base(appPaths, xmlSerializer)
        {
            Instance = this;
            _autosyncManager = new AutoSyncSubtitlesManager(
                libraryManager,
                loggerFactory.CreateLogger<AutoSyncSubtitlesManager>(),
                appPaths);
        }

        public override string Name => "Autosync Subtitles";

        public static Plugin Instance { get; private set; }

        public override string Description
            => "Autosyncs Subtitles";

        private readonly Guid _id = new Guid("2ac0161e-b8e9-4ccb-b425-0020abe1afec");
        public override Guid Id => _id;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Autosync Subtitles",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configurationpage.html"
                }
            };
        }
    }
}
