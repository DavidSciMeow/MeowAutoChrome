using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api
{
    /// <summary>
    /// 处理布局相关设置的 API，例如插件面板宽度保存。<br/>
    /// API for layout-related settings such as saving plugin panel width.
    /// </summary>
    [ApiController]
    [Route("api/layout")]
    public class LayoutController : ControllerBase
    {
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;

        /// <summary>
        /// 创建 LayoutController。<br/>
        /// Create a LayoutController instance.
        /// </summary>
        /// <param name="programSettingsService">程序设置提供者 / program settings provider.</param>
        public LayoutController(Core.Interface.IProgramSettingsProvider programSettingsService)
        {
            this.programSettingsService = programSettingsService;
        }

        /// <summary>
        /// 保存布局设置（当前仅保存插件面板宽度）。<br/>
        /// Save layout settings (currently only plugin panel width).
        /// </summary>
        /// <param name="request">包含布局设置的请求对象 / request containing layout settings.</param>
        /// <returns>包含保存后插件面板宽度的 IActionResult / IActionResult with saved plugin panel width.</returns>
        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] BrowserLayoutSettingsRequest request)
        {
            var settings = await programSettingsService.GetAsync();
            settings.PluginPanelWidth = request.PluginPanelWidth;
            await programSettingsService.SaveAsync(settings);
            return Ok(new { pluginPanelWidth = settings.PluginPanelWidth });
        }
    }
}
