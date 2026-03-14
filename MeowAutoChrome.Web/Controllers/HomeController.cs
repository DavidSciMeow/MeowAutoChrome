using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MeowAutoChrome.Web.Controllers
{
    public class HomeController(ProgramSettingsService programSettingsService, ScreencastService screencastService, AppLogService appLogService) : Controller
    {
        private static int FpsToInterval(int fps)
            => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

        private static LogEntryViewModel ToLogEntryViewModel(AppLogEntry entry)
            => new()
            {
                TimestampText = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                LevelText = entry.Level.ToString(),
                FilterLevel = entry.Level switch
                {
                    LogLevel.Warning => "warn",
                    LogLevel.Error or LogLevel.Critical => "error",
                    _ => "info"
                },
                Category = entry.Category,
                Message = entry.Message
            };

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

        [HttpGet]
        public async Task<IActionResult> Logs()
        {
            return View(new LogPageViewModel
            {
                LogDisplayPath = appLogService.LogDisplayPath,
                Entries = (await appLogService.ReadRecentEntriesAsync()).Select(ToLogEntryViewModel).ToArray(),
                LastUpdatedUtc = appLogService.GetLastWriteTime()
            });
        }

        [HttpGet]
        public async Task<IActionResult> LogsContent()
        {
            var entries = (await appLogService.ReadRecentEntriesAsync()).Select(ToLogEntryViewModel).ToArray();

            return Json(new
            {
                entries,
                lastUpdatedLocal = appLogService.GetLastWriteTime()?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLogs()
        {
            await appLogService.ClearAsync();
            TempData["StatusMessage"] = "日志已清空。";
            return RedirectToAction(nameof(Logs));
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
