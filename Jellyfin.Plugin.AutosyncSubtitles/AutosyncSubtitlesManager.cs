using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
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

        private string GetExecutablePath(string executableName)
        {
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

            string finalExecutableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{executableName}.exe" : executableName;
            Directory.CreateDirectory(Path.Combine(_pluginDirectory, platform));
            string executablePath = Path.Combine(_pluginDirectory, platform, finalExecutableName);
            // Extract executable if it doesn't exist
            if (!File.Exists(executablePath))
            {
                _logger.LogInformation($"Downloading {finalExecutableName} for {platform}...");
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Jellyfin-Plugin-AutosyncSubtitles");

                    try
                    {
                        // Replace with your GitHub release URL
                        string downloadUrl = $"https://raw.githubusercontent.com/johnpc/jellyfin-plugin-autosyncsubtitles/refs/heads/main/Jellyfin.Plugin.AutosyncSubtitles/Binaries/{executableName}/{platform}/{finalExecutableName}";
                        client.DownloadFile(downloadUrl, executablePath);

                        // Set executable permissions on Unix systems
                        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            try
                            {
                                var unixFileMode = Convert.ToInt32("755", 8);
                                File.SetUnixFileMode(executablePath, (UnixFileMode)unixFileMode);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to set executable permissions on {Path}", executablePath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to download {Binary} for {Platform}", finalExecutableName, platform);
                        throw;
                    }
                }
            }

            _logger.LogInformation($"{executableName} path is {executablePath}");
            return executablePath;
        }

        private string GetAutosubsyncPath()
        {

            string executableName = "autosubsync";
            return GetExecutablePath(executableName);
        }

        private string GetFfsubsyncPath()
        {
            string executableName = "ffsubsync";
            return GetExecutablePath(executableName);
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
                .Take(1)
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

                if (originalFileName.Contains(".ffsubsync.") || originalFileName.Contains(".autosubsync."))
                {
                    continue;
                }

                // ExecFfsubsync(mediaItem, fileNameWithoutExt, originalExtension, directory, videoFilePath, subtitlePath);
                ExecAutosubsync(mediaItem, fileNameWithoutExt, originalExtension, directory, videoFilePath, subtitlePath);
            }
        }

        private void ExecFfsubsync(BaseItem mediaItem, string fileNameWithoutExt, string originalExtension, string directory,
            string videoFilePath, string subtitlePath)
        {
            try
            {
                string syncedFileName = $"{fileNameWithoutExt}.ffsubsync{originalExtension}";
                string syncedSubtitlePath = Path.Combine(directory, syncedFileName);
                if (File.Exists(syncedSubtitlePath))
                {
                    _logger.LogInformation($"Synced ffsubsync subtitle already exists for {mediaItem.Name}: {syncedFileName}, skipping");
                    return;
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
                        return;
                    }

                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError($"ffsubsync failed for {mediaItem.Name}: {error}");
                        return;
                    }

                    _logger.LogInformation($"Successfully generated synced subtitle for {mediaItem.Name}");
                }

                if (exitCode != 0)
                {
                    _logger.LogError($"ffsubsync failed for {mediaItem.Name}: {error}");
                    return;
                }

                _logger.LogInformation($"Successfully generated synced subtitle for {mediaItem.Name}");

                // Refresh the media item to detect the new subtitle
                mediaItem.RefreshMetadata(CancellationToken.None);
                _logger.LogInformation($"Refreshed metadata for {mediaItem.Name} to detect new subtitle");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing ffsubsync subtitle sync for {mediaItem.Name}");
            }
        }

        private void ExecAutosubsync(BaseItem mediaItem, string fileNameWithoutExt, string originalExtension, string directory,
            string videoFilePath, string subtitlePath)
        {
            try
            {
                string syncedFileName = $"{fileNameWithoutExt}.autosubsync{originalExtension}";
                string syncedSubtitlePath = Path.Combine(directory, syncedFileName);
                if (File.Exists(syncedSubtitlePath))
                {
                    _logger.LogInformation($"Synced autosubsync subtitle already exists for {mediaItem.Name}: {syncedFileName}, skipping");
                    return;
                }
                _logger.LogInformation($"Running autosubsync for {mediaItem.Name}");
                _logger.LogInformation($"Video path: {videoFilePath}");
                _logger.LogInformation($"Original subtitle path: {subtitlePath}");
                _logger.LogInformation($"Synced subtitle path: {syncedSubtitlePath}");

                // Create process to run autosubsync
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = GetAutosubsyncPath(),
                    Arguments = $"\"{videoFilePath}\" \"{subtitlePath}\" \"{syncedSubtitlePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _logger.LogInformation($"Running command: {GetAutosubsyncPath()} {processStartInfo.Arguments}");

                string output;
                string error;
                int exitCode;

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        _logger.LogError($"Failed to start autosubsync process for {mediaItem.Name}");
                        return;
                    }

                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError($"autosubsync failed for {mediaItem.Name}: {error}");
                        return;
                    }

                    _logger.LogInformation($"Successfully generated autosubsync synced subtitle for {mediaItem.Name}");
                }

                if (exitCode != 0)
                {
                    _logger.LogError($"autosubsync failed for {mediaItem.Name}: {error}");
                    return;
                }

                _logger.LogInformation($"Successfully generated autosubsync synced subtitle for {mediaItem.Name}");

                // Refresh the media item to detect the new subtitle
                mediaItem.RefreshMetadata(CancellationToken.None);
                _logger.LogInformation($"Refreshed metadata for {mediaItem.Name} to detect new subtitle");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing autosubsync subtitle sync for {mediaItem.Name}");
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
