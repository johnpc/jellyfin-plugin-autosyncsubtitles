using System.Net.Mime;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AutoSyncSubtitles.Api
{
    /// <summary>
    /// The Autosync Subtitles api controller.
    /// </summary>
    [ApiController]
    [Route("AutoSyncSubtitles")]
    [Produces(MediaTypeNames.Application.Json)]


    public class AutoSyncSubtitlesController : ControllerBase
    {
        private readonly AutoSyncSubtitlesManager _autoSyncSubtitlesManager;
        private readonly ILogger<AutoSyncSubtitlesManager> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="AutoSyncSubtitlesController"/>.

        public AutoSyncSubtitlesController(
            ILibraryManager libraryManager,
            ILogger<AutoSyncSubtitlesManager> logger)
        {
            _autoSyncSubtitlesManager = new AutoSyncSubtitlesManager(libraryManager,  logger);
            _logger = logger;
        }

        /// <summary>
        /// Creates autosync subtitles.
        /// </summary>
        /// <reponse code="204">Subtitle autosync started successfully. </response>
        /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
        [HttpPost("AutosyncSubtitles")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public ActionResult AutoSyncSubtitlesRequest()
        {
            _logger.LogInformation("Autosyncing Subtitles");
            _autoSyncSubtitlesManager.ExecuteAutoSyncSubtitles();
            _logger.LogInformation("Completed");
            return NoContent();
        }



    }
}