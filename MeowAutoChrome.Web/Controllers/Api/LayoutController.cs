using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Web.Models;

namespace MeowAutoChrome.Web.Controllers.Api
{
    [ApiController]
    [Route("api/layout")]
    public class LayoutController : ControllerBase
    {
        private readonly Core.Interface.IProgramSettingsProvider programSettingsService;

        public LayoutController(Core.Interface.IProgramSettingsProvider programSettingsService)
        {
            this.programSettingsService = programSettingsService;
        }

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
