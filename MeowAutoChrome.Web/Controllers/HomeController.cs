using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MeowAutoChrome.Web.Controllers
{
    public class HomeController(ProgramSettingsService programSettingsService, ScreencastService screencastService, AppLogService appLogService, PlayWrightWarpper browser) : Controller
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

            try
            {
                var currentUserDataDirectory = Path.GetFullPath(browser.UserDataDirectoryPath);
                var targetUserDataDirectory = Path.GetFullPath(model.UserDataDirectory);

                if (IsNestedDirectory(currentUserDataDirectory, targetUserDataDirectory) || IsNestedDirectory(targetUserDataDirectory, currentUserDataDirectory))
                    ModelState.AddModelError(nameof(model.UserDataDirectory), "浏览器用户数据目录不能设置为当前目录的子目录或父目录。");
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UserDataDirectory), "浏览器用户数据目录无效。");
            }
        }

        private static bool IsNestedDirectory(string parentPath, string childPath)
        {
            var normalizedParentPath = Path.TrimEndingDirectorySeparator(parentPath);
            var normalizedChildPath = Path.TrimEndingDirectorySeparator(childPath);
            return normalizedChildPath.StartsWith(normalizedParentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedChildPath.StartsWith(normalizedParentPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> SaveProgramSettingsAsync(ProgramSettingsViewModel model)
        {
            var settings = new ProgramSettings
            {
                SearchUrlTemplate = model.SearchUrlTemplate,
                ScreencastFps = model.ScreencastFps,
                PluginPanelWidth = model.PluginPanelWidth,
                UserDataDirectory = model.UserDataDirectory,
                Headless = model.Headless
            };

            await programSettingsService.SaveAsync(settings);

            var userDataDirectoryChanged = !string.Equals(browser.UserDataDirectoryPath, settings.UserDataDirectory, StringComparison.OrdinalIgnoreCase);
            var headlessChanged = browser.IsHeadless != settings.Headless;
            if (userDataDirectoryChanged || headlessChanged)
                await browser.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless);

            await screencastService.UpdateSettingsAsync(
                screencastService.RequestedEnabled,
                screencastService.MaxWidth,
                screencastService.MaxHeight,
                FpsToInterval(model.ScreencastFps));

            if (userDataDirectoryChanged || headlessChanged)
                await screencastService.OnBrowserModeChangedAsync();

            if (userDataDirectoryChanged && headlessChanged)
                return "设置已自动保存，浏览器用户数据目录和 Headless 模式已切换。";

            if (userDataDirectoryChanged)
                return "设置已自动保存，浏览器用户数据目录已切换。";

            if (headlessChanged)
                return "设置已自动保存，Headless 模式已切换。";

            return "设置已自动保存。";
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
                PluginPanelWidth = settings.PluginPanelWidth,
                UserDataDirectory = settings.UserDataDirectory,
                Headless = settings.Headless
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(ProgramSettingsViewModel model)
        {
            ValidateProgramSettings(model);

            if (!ModelState.IsValid)
                return View(model);

            string message;
            try
            {
                message = await SaveProgramSettingsAsync(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }

            TempData["StatusMessage"] = message;
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

            try
            {
                var message = await SaveProgramSettingsAsync(model);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
