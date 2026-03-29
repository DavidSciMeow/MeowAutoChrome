using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/navigation")]
    public class NavigationController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;

        public NavigationController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
        }

        [HttpPost("navigate")]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(new { navigated = true });
        }

        [HttpPost("back")]
        public async Task<IActionResult> Back()
        {
            await browserInstances.GoBackAsync();
            return Ok(new { navigated = true });
        }

        [HttpPost("forward")]
        public async Task<IActionResult> Forward()
        {
            await browserInstances.GoForwardAsync();
            return Ok(new { navigated = true });
        }

        [HttpPost("reload")]
        public async Task<IActionResult> Reload()
        {
            await browserInstances.ReloadAsync();
            return Ok(new { reloaded = true });
        }
    }
}
