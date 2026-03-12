using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MeowAutoChrome.Web.Controllers
{
    public class HomeController(ProgramSettingsService programSettingsService, ScreencastService screencastService) : Controller
    {
        private static int FpsToInterval(int fps)
            => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

        private void ValidateProgramSettings(ProgramSettingsViewModel model)
        {
            if (!model.SearchUrlTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(model.SearchUrlTemplate), "搜索地址模板必须包含 {query} 占位符。");
        }

        private async Task SaveProgramSettingsAsync(ProgramSettingsViewModel model)
        {
            await programSettingsService.SaveAsync(new ProgramSettings
            {
                SearchUrlTemplate = model.SearchUrlTemplate,
                ScreencastFps = model.ScreencastFps,
                PluginPanelWidth = model.PluginPanelWidth
            });

            await screencastService.UpdateSettingsAsync(
                screencastService.Enabled,
                screencastService.MaxWidth,
                screencastService.MaxHeight,
                FpsToInterval(model.ScreencastFps));
        }

        public IActionResult Index()
            => RedirectToAction("Index", "Browser");

        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var settings = await programSettingsService.GetAsync();
            return View(new ProgramSettingsViewModel
            {
                SearchUrlTemplate = settings.SearchUrlTemplate,
                ScreencastFps = settings.ScreencastFps,
                PluginPanelWidth = settings.PluginPanelWidth
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(ProgramSettingsViewModel model)
        {
            ValidateProgramSettings(model);

            if (!ModelState.IsValid)
                return View(model);

            await SaveProgramSettingsAsync(model);

            TempData["StatusMessage"] = "设置已保存。";
            return RedirectToAction(nameof(Settings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSaveSettings(ProgramSettingsViewModel model)
        {
            ValidateProgramSettings(model);

            if (!ModelState.IsValid)
            {
                var errorMessage = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault() ?? "设置保存失败。";

                return BadRequest(new { message = errorMessage });
            }

            await SaveProgramSettingsAsync(model);
            return Ok(new { message = "设置已自动保存。" });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
