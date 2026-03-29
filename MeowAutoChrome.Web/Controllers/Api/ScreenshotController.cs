using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Services;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/screenshot")]
    public class ScreenshotController : ControllerBase
    {
        private readonly BrowserInstanceManager browserInstances;

        public ScreenshotController(BrowserInstanceManager browserInstances)
        {
            this.browserInstances = browserInstances;
        }

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
