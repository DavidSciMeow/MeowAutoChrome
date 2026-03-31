using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 导航相关的 API 控制器，包含前进、后退、刷新与导航操作。<br/>
    /// API controller for navigation operations (navigate, back, forward, reload).
    /// </summary>
    [ApiController]
    [Route("api/navigation")]
    public class NavigationController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;
        private readonly Core.Services.ScreencastServiceCore screencastService;

        /// <summary>
        /// 构造函数：注入浏览器实例管理器与 Screencast 服务。<br/>
        /// Constructor: injects the browser instance manager and the screencast service.
        /// </summary>
        /// <param name="browserInstances">浏览器实例管理器 / browser instance manager.</param>
        /// <param name="screencastService">屏幕投射服务 / screencast service.</param>
        public NavigationController(BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastService)
        {
            this.browserInstances = browserInstances;
            this.screencastService = screencastService;
        }

        /// <summary>
        /// 导航到指定 URL 并刷新投屏目标。<br/>
        /// Navigate to the specified URL and refresh the screencast target.
        /// </summary>
        /// <param name="request">包含目标 URL 的请求对象 / request containing the target URL.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("navigate")]
        public async Task<IActionResult> Navigate([FromBody] BrowserNavigateRequest request)
        {
            await browserInstances.NavigateAsync(request.Url);
            await screencastService.RefreshTargetAsync();
            return Ok(new { navigated = true });
        }

        /// <summary>
        /// 后退到上一个页面。<br/>
        /// Navigate back to the previous page.
        /// </summary>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("back")]
        public async Task<IActionResult> Back()
        {
            await browserInstances.GoBackAsync();
            return Ok(new { navigated = true });
        }

        /// <summary>
        /// 前进到下一个页面。<br/>
        /// Navigate forward to the next page.
        /// </summary>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("forward")]
        public async Task<IActionResult> Forward()
        {
            await browserInstances.GoForwardAsync();
            return Ok(new { navigated = true });
        }

        /// <summary>
        /// 重新加载当前页面。<br/>
        /// Reload the current page.
        /// </summary>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("reload")]
        public async Task<IActionResult> Reload()
        {
            await browserInstances.ReloadAsync();
            return Ok(new { reloaded = true });
        }
    }
}
