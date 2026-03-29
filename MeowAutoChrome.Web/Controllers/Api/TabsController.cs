using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/tabs")]
    public class TabsController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;

        public TabsController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
        }

        [HttpPost("new")]
        public async Task<IActionResult> NewTab([FromBody] BrowserCreateTabRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.InstanceId))
            {
                var selected = await browserInstances.SelectBrowserInstanceAsync(request.InstanceId);
                if (!selected)
                    return NotFound(new { error = "实例不存在" });
            }

            if (browserInstances.GetInstances().Count == 0)
            {
                var ownerPluginId = "ui";
                await browserInstances.CreateBrowserInstanceAsync(ownerPluginId);
                await screencastService.RefreshTargetAsync();
                return Ok(new { createdInstance = true });
            }

            await browserInstances.CreateTabAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(new { created = true });
        }

        [HttpPost("close")]
        public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
        {
            var closed = await browserInstances.CloseTabAsync(request.TabId);
            if (!closed)
                return NotFound(new { error = "标签页不存在或已无法关闭" });

            await screencastService.RefreshTargetAsync();
            return Ok(new { closed = true });
        }

        [HttpPost("select")]
        public async Task<IActionResult> SelectTab([FromBody] BrowserSelectTabRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TabId))
                return BadRequest(new { error = "tabId required" });

            var ok = await browserInstances.SelectPageAsync(request.TabId);
            if (!ok)
                return NotFound(new { error = "标签页不存在" });

            await screencastService.RefreshTargetAsync();
            return Ok(new { selected = true });
        }
    }
}
