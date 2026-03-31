using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 提供投屏（screencast）设置相关的 API。<br/>
    /// API for screencast-related settings.
    /// </summary>
    [ApiController]
    [Route("api/screencast")]
    public class ScreencastController : ControllerBase
    {
        private readonly Core.Services.ScreencastServiceCore screencastService;

        /// <summary>
        /// 创建 ScreencastController。<br/>
        /// Create a ScreencastController instance.
        /// </summary>
        /// <param name="screencastService">投屏服务实例 / screencast service instance.</param>
        public ScreencastController(Core.Services.ScreencastServiceCore screencastService)
        {
            this.screencastService = screencastService;
        }

        /// <summary>
        /// 更新投屏设置（启用标志、最大宽高与帧间隔）。<br/>
        /// Update screencast settings (enabled, max dimensions, frame interval).
        /// </summary>
        /// <param name="request">包含投屏设置的请求 / request containing screencast settings.</param>
        /// <returns>操作结果的 IActionResult / IActionResult for the operation result.</returns>
        [HttpPost("settings")]
        public async Task<IActionResult> UpdateSettings([FromBody] ScreencastSettingsRequest request)
        {
            await screencastService.UpdateSettingsAsync(request.Enabled, request.MaxWidth, request.MaxHeight, request.FrameIntervalMs);
            return Ok(new { updated = true });
        }
    }
}
