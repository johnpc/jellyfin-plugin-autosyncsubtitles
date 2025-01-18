using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using MediaBrowser.Common.Configuration;

namespace Jellyfin.Plugin.AutoSyncSubtitles.ScheduledTasks
{
    public class ExecuteAutoSyncSubtitlesTask : IScheduledTask
    {
        private readonly ILogger<AutoSyncSubtitlesManager> _logger;
        private readonly AutoSyncSubtitlesManager _autoSyncSubtitlesManager;

        public ExecuteAutoSyncSubtitlesTask(ILibraryManager libraryManager, ILogger<AutoSyncSubtitlesManager> logger, IApplicationPaths applicationPaths)
        {
            _logger = logger;
            _autoSyncSubtitlesManager = new AutoSyncSubtitlesManager(libraryManager, logger, applicationPaths);
        }
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.LogInformation("Starting plugin, executing Autosync Subtitles...");
            _autoSyncSubtitlesManager.ExecuteAutoSyncSubtitles();
            _logger.LogInformation("All autosync subtitles generated");
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Execute(cancellationToken, progress);
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Run this task every 24 hours
            yield return new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerInterval,
                IntervalTicks = TimeSpan.FromHours(24).Ticks
            };
        }

        public string Name => "Autosync Subtitles";
        public string Key => "AutoSyncSubtitles";
        public string Description => "Scans all libraries and automatically syncs subtitles files";
        public string Category => "Autosync Subtitles";
    }
}
