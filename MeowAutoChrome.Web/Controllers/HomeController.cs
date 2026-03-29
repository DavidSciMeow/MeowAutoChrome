using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Struct;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MeowAutoChrome.Web.Controllers
{
    /// <summary>
    /// 主页控制器（用于设置、日志和隐私页面），负责处理程序设置的保存与验证，以及日志的展示与清理。
    /// </summary>
    public class HomeController(IProgramSettingsProvider programSettingsService, Core.Services.ScreencastServiceCore screencastService, Core.Services.AppLogService appLogService, BrowserInstanceManager browserInstances, SettingsService settingsService) : Controller
    {
        /// <summary>
        /// 显示浏览器控制台页面（已将浏览器页面合并到 Home 区）。
        /// </summary>
        public IActionResult Index() => View();

        /// <summary>
        /// 显示当前程序设置页面（GET）.
        /// </summary>
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
                UserAgent = settings.UserAgent,
                AllowInstanceUserAgentOverride = settings.AllowInstanceUserAgentOverride,
                Headless = settings.Headless,
                CustomSettings = settings.CustomSettings ?? new Dictionary<string, string?>()
            });
        }

        /// <summary>
        /// 保存程序设置（POST）。
        /// </summary>
        /// <param name="model">一个包含程序设置的视图模型。</param>
        /// <returns></returns>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(ProgramSettingsViewModel model)
        {
            settingsService.ValidateProgramSettings(model, err => ModelState.AddModelError(string.Empty, err));

            if (!ModelState.IsValid)
                return View(model);

            string message;
            try
            {
                message = await settingsService.SaveProgramSettingsAsync(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }

            TempData["StatusMessage"] = message;
            return RedirectToAction(nameof(Settings));
        }

        /// <summary>
        /// 显示日志页面（视图）。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logs()
        {
            return View(new LogPageViewModel
            {
                LogDisplayPath = appLogService.LogDisplayPath,
                Entries = (await appLogService.ReadRecentEntriesAsync()).Select(e => new LogEntryViewModel
                {
                    TimestampText = e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    LevelText = e.Level.ToString(),
                    FilterLevel = e.Level switch
                    {
                        LogLevel.Warning => "warn",
                        LogLevel.Error or LogLevel.Critical => "error",
                        _ => "info"
                    },
                    Category = e.Category,
                    Message = e.Message
                }).ToArray(),
                LastUpdatedUtc = appLogService.GetLastWriteTime()
            });
        }

        /// <summary>
        /// 清空日志文件并重定向回日志页面（保留用于表单提交回退）。
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLogs()
        {
            await appLogService.ClearAsync();
            TempData["StatusMessage"] = "日志已清空。";
            return RedirectToAction(nameof(Logs));
        }

        /// <summary>
        /// 隐私页展示。
        /// </summary>
        public IActionResult Privacy() => View();

        /// <summary>
        /// Show plugin upload/manage page (moved from Browser area to Settings sidebar).
        /// </summary>
        public IActionResult PluginUpload() => View();

        /// <summary>
        /// 错误页面展示接口。
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
        }
    }
}
