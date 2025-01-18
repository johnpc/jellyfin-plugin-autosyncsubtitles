using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;

namespace Jellyfin.Plugin.AutoSyncSubtitles

{


    public class AutoSyncSubtitlesManager : IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly Timer _timer;
        private readonly ILogger<AutoSyncSubtitlesManager> _logger;

        public AutoSyncSubtitlesManager(ILibraryManager libraryManager, ILogger<AutoSyncSubtitlesManager> logger)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
        }

        private IEnumerable<Series> GetEpisodesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {BaseItemKind.Episode},
                IsVirtualItem = false,
                Recursive = true,
                HasTvdbId = true
            }).Select(m => m as Series);
        }

        private IEnumerable<Series> GetMoviesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] {BaseItemKind.Movie},
                IsVirtualItem = false,
                Recursive = true,
                HasTvdbId = true
            }).Select(m => m as Series);
        }


        public void ExecuteAutoSyncSubtitles()
        {
            _logger.LogDebug("Performing ExecuteAutoSyncSubtitles");
            var movies = GetMoviesFromLibrary();
            var episodes = GetEpisodesFromLibrary();
            foreach (var movie in movies)
            {
                if (movie == null) {
                    _logger.LogDebug($"Skipping process for invalid movie");
                    continue;
                }
                // Get subtitles for the movie
                var subtitles = movie.GetMediaStreams()
                    ?.Where(s => s.Type == MediaStreamType.Subtitle)
                    ?.ToList();

                // Check if there are any subtitles
                if (subtitles == null || !subtitles.Any())
                {
                    _logger.LogDebug($"No subtitles found for movie: {movie.Name}. Nothing to auto correct");
                    continue;
                }

                _logger.LogInformation($"Subtitles detected for movie: {movie.Name}. Attempting to generate autosync'd subtitles.");
            }
        }


        private void OnTimerElapsed()
        {
            // Stop the timer until next update
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public Task RunAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
