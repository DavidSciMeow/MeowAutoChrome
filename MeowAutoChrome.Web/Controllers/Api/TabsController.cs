using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 标签（tab）相关的 API，提供创建、关闭与选择标签的操作。<br/>
    /// API for tab operations: create, close and select tabs.
    /// </summary>
    [ApiController]
    [Route("api/tabs")]
    public class TabsController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;

        /// <summary>
        /// 创建 TabsController。<br/>
        /// Create a TabsController instance.
        /// </summary>
        /// <param name="browserInstances">浏览器实例管理器 / browser instance manager.</param>
        /// <param name="screencastService">投屏服务 / screencast service.</param>
        public TabsController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
        }

        /// <summary>
        /// 在指定实例中创建新标签，或在没有实例时创建新实例并打开标签。<br/>
        /// Create a new tab in the specified instance, or create an instance if none exist.
        /// </summary>
        /// <param name="request">包含实例 Id 与 URL 的请求 / request with instance id and URL.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
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

        /// <summary>
        /// 关闭指定标签页。<br/>
        /// Close the specified tab.
        /// </summary>
        /// <param name="request">包含要关闭的标签 Id 的请求 / request containing the tab id.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("close")]
        public async Task<IActionResult> CloseTab([FromBody] BrowserCloseTabRequest request)
        {
            var closed = await browserInstances.CloseTabAsync(request.TabId);
            if (!closed)
                return NotFound(new { error = "标签页不存在或已无法关闭" });

            await screencastService.RefreshTargetAsync();
            return Ok(new { closed = true });
        }

        /// <summary>
        /// 选择指定的标签页使其成为活动页。<br/>
        /// Select the specified tab as the active page.
        /// </summary>
        /// <param name="request">包含 tabId 的请求 / request containing the tab id.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
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
