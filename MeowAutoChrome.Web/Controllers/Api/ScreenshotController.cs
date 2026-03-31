using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 提供截图（screenshot）相关的 API。<br/>
    /// API for screenshot operations.
    /// </summary>
    [ApiController]
    [Route("api/screenshot")]
    public class ScreenshotController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;

        /// <summary>
        /// 创建 ScreenshotController。<br/>
        /// Create a ScreenshotController instance.
        /// </summary>
        /// <param name="browserInstances">浏览器实例管理器 / browser instance manager.</param>
        public ScreenshotController(BrowserInstanceManager browserInstances)
        {
            this.browserInstances = browserInstances;
        }

        /// <summary>
        /// 获取当前实例的截图（PNG）。<br/>
        /// Get a PNG screenshot of the current instance.
        /// </summary>
        /// <returns>PNG 文件流或错误响应 / PNG file stream or error response.</returns>
        [HttpGet]
        public async Task<IActionResult> GetScreenshot()
        {
            if (browserInstances.GetInstances().Count == 0)
                return BadRequest(new { error = "无实例" });

            var screenshot = await HttpContext.RequestServices.GetRequiredService<Core.Services.ScreenshotService>().CaptureScreenshotAsync();
            if (screenshot == null)
                return NotFound();

            return File(screenshot, "image/png");
        }
    }
}
