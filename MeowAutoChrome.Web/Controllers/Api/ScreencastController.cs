using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/screencast")]
    public class ScreencastController : ControllerBase
    {
        private readonly Core.Services.ScreencastServiceCore screencastService;

        public ScreencastController(Core.Services.ScreencastServiceCore screencastService)
        {
            this.screencastService = screencastService;
        }

        [HttpPost("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] ScreencastSettingsRequest request)
        {
            await screencastService.UpdateSettingsAsync(request.Enabled, request.MaxWidth, request.MaxHeight, request.FrameIntervalMs);
            return Ok(new { updated = true });
        }
    }
}
