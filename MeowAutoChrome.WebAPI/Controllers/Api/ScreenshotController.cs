using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.Core.Services;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/screenshot")]
/// <summary>
/// 截图 API，返回当前活动页面的 PNG 截图。<br/>
/// Screenshot API returning a PNG capture of the current active page.
/// </summary>
public class ScreenshotController(BrowserInstanceManager browserInstances) : ControllerBase
{

    /// <summary>
    /// 获取当前页面截图。<br/>
    /// Get a screenshot of the current page.
    /// </summary>
    /// <returns>PNG 文件流或错误响应。<br/>PNG file stream or an error response.</returns>
    [HttpGet]
    public async Task<IActionResult> GetScreenshot()
    {
        if (browserInstances.GetInstances().Count == 0)
            return BadRequest(new { error = "无实例" });

        var screenshot = await HttpContext.RequestServices.GetRequiredService<ScreenshotService>().CaptureScreenshotAsync();
        if (screenshot == null)
            return NotFound();

        return File(screenshot, "image/png");
    }
}
