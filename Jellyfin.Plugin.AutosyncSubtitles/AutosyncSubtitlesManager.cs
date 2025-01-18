using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
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
        private readonly string _pluginDirectory;

        public AutoSyncSubtitlesManager(ILibraryManager libraryManager, ILogger<AutoSyncSubtitlesManager> logger, IApplicationPaths applicationPaths)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _timer = new Timer(_ => OnTimerElapsed(), null, Timeout.Infinite, Timeout.Infinite);
            _pluginDirectory = Path.Combine(applicationPaths.DataPath, "autosyncsubtitles");
            Directory.CreateDirectory(_pluginDirectory);
        }

        private string GetFfsubsyncPath()
        {
            // TODO: Use precompiled binaries of ffsubsync
            // For now, I have manually installed ffs in my jellyfin container
            return "/root/.local/bin/ffs";

            // Create plugin directory if it doesn't exist
            Directory.CreateDirectory(_pluginDirectory);

            string platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platform = "win-x64";
                throw new PlatformNotSupportedException($"Unsupported operating system: {platform}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = "osx-x64";
                throw new PlatformNotSupportedException($"Unsupported operating system: {platform}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = "linux-x64";
            }
            else
            {
                throw new PlatformNotSupportedException(
                    $"Unsupported operating system: {RuntimeInformation.OSDescription} " +
                    $"(OS: {Environment.OSVersion}, " +
                    $"Architecture: {RuntimeInformation.OSArchitecture}, " +
                    $"Framework: {RuntimeInformation.FrameworkDescription})"
                );
            }

            string executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffsubsync.exe" : "ffsubsync";
            string ffsubsyncPath = Path.Combine(_pluginDirectory, executableName);

            // Extract executable if it doesn't exist
            if (!File.Exists(ffsubsyncPath))
            {
                throw new PlatformNotSupportedException(
                    $"Not found ffsubsync binary at path {ffsubsyncPath} for operating system: {RuntimeInformation.OSDescription} " +
                    $"(OS: {Environment.OSVersion}, " +
                    $"Architecture: {RuntimeInformation.OSArchitecture}, " +
                    $"Framework: {RuntimeInformation.FrameworkDescription})"
                );
            }

            _logger.LogInformation($"ffsubsyncPath path is {ffsubsyncPath}");
            return ffsubsyncPath;
        }

        private IEnumerable<Episode> GetEpisodesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                IsVirtualItem = false,
                Recursive = true,
                HasTvdbId = true
            }).Select(m => m as Episode);
        }

        private IEnumerable<Movie> GetMoviesFromLibrary()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                IsVirtualItem = false,
                Recursive = true,
                HasTvdbId = false
            }).Select(m => m as Movie);
        }


        public void ExecuteAutoSyncSubtitles()
        {
            _logger.LogInformation("Performing ExecuteAutoSyncSubtitles");
            var movies = GetMoviesFromLibrary().ToList();
            var episodes = GetEpisodesFromLibrary().ToList();
            _logger.LogInformation($"Found {movies.Count} movies and {episodes.Count} episodes in library");
            var mediaItems = movies.Cast<BaseItem>().Concat(episodes.Cast<BaseItem>())
                .ToList();
            _logger.LogInformation($"Processing {mediaItems.Count} total media items");

            foreach (var mediaItem in mediaItems)
            {
                if (mediaItem == null)
                {
                    _logger.LogInformation($"Skipping process for invalid movie");
                    continue;
                }
                // Get subtitles for the movie
                var subtitles = mediaItem.GetMediaStreams()
                    ?.Where(s => s.Type == MediaStreamType.Subtitle)
                    ?.ToList();

                // Check if there are any subtitles
                if (subtitles == null || !subtitles.Any())
                {
                    _logger.LogInformation($"No subtitles found for mediaItem: {mediaItem.Name}. Nothing to auto correct");
                    continue;
                }

                GenerateAutosyncedSubtitleFile(mediaItem, subtitles);
            }
        }

        private void GenerateAutosyncedSubtitleFile(BaseItem mediaItem, IEnumerable<MediaStream> subtitles)
        {
            _logger.LogInformation($"Subtitles detected for mediaItem: {mediaItem.Name}. Attempting to generate autosync'd subtitles.");

            foreach (var subtitle in subtitles)
            {
                // Get the video file path
                string videoFilePath = mediaItem.Path;

                // Get the subtitle file path
                string subtitlePath = subtitle.Path;
                if (string.IsNullOrEmpty(subtitlePath))
                {
                    _logger.LogWarning($"Subtitle path is empty for {mediaItem.Name}, skipping");
                    continue;
                }

                // Generate the new subtitle path by adding prefix to the filename
                string directory = Path.GetDirectoryName(subtitlePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(subtitlePath);
                string originalFileName = Path.GetFileName(subtitlePath);
                string originalExtension = Path.GetExtension(subtitlePath);  // Gets .srt, .ass, etc.

                try
                {
                    string syncedFileName = $"{fileNameWithoutExt}.ffsubsync{originalExtension}";
                    string syncedSubtitlePath = Path.Combine(directory, syncedFileName);
                    if (File.Exists(syncedSubtitlePath))
                    {
                        _logger.LogInformation($"Synced subtitle already exists for {mediaItem.Name}: {syncedFileName}, skipping");
                        continue;
                    }
                    _logger.LogInformation($"Running ffsubsync for {mediaItem.Name}");
                    _logger.LogInformation($"Video path: {videoFilePath}");
                    _logger.LogInformation($"Original subtitle path: {subtitlePath}");
                    _logger.LogInformation($"Synced subtitle path: {syncedSubtitlePath}");

                    // Create process to run ffsubsync
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = GetFfsubsyncPath(),
                        Arguments = $"\"{videoFilePath}\" -i \"{subtitlePath}\" -o \"{syncedSubtitlePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    _logger.LogInformation($"Running command: {GetFfsubsyncPath()} {processStartInfo.Arguments}");

                    string output;
                    string error;
                    int exitCode;

                    using (var process = Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            _logger.LogError($"Failed to start ffsubsync process for {mediaItem.Name}");
                            continue;
                        }

                        output = process.StandardOutput.ReadToEnd();
                        error = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        exitCode = process.ExitCode;

                        if (process.ExitCode != 0)
                        {
                            _logger.LogError($"ffsubsync failed for {mediaItem.Name}: {error}");
                            continue;
                        }

                        _logger.LogInformation($"Successfully generated synced subtitle for {mediaItem.Name}");
                    }

                    if (exitCode != 0)
                    {
                        _logger.LogError($"ffsubsync failed for {mediaItem.Name}: {error}");
                        continue;
                    }

                    _logger.LogInformation($"Successfully generated synced subtitle for {mediaItem.Name}");

                    // Refresh the media item to detect the new subtitle
                    mediaItem.RefreshMetadata(CancellationToken.None);
                    _logger.LogInformation($"Refreshed metadata for {mediaItem.Name} to detect new subtitle");

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing subtitle sync for {mediaItem.Name}");
                }
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
            _timer?.Dispose();
        }
    }
}
