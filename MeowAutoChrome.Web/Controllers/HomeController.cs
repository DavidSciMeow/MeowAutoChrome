using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Struct;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MeowAutoChrome.Web.Controllers
{
    /// <summary>
    /// 主页控制器（用于设置、日志和隐私页面），负责处理程序设置的保存与验证，以及日志的展示与清理。<br/>
    /// Home controller for settings, logs and privacy pages; handles saving/validation of program settings and log display/cleanup.
    /// </summary>
    /// <param name="programSettingsService">程序设置提供者 / program settings provider.</param>
    /// <param name="screencastService">投屏服务 / screencast service.</param>
    /// <param name="appLogService">应用日志服务 / application log service.</param>
    /// <param name="browserInstances">浏览器实例管理服务 / browser instance manager.</param>
    /// <param name="settingsService">页面/设置服务 / settings helper service.</param>
    public class HomeController(IProgramSettingsProvider programSettingsService, Core.Services.ScreencastServiceCore screencastService, Core.Services.AppLogService appLogService, BrowserInstanceManager browserInstances, SettingsService settingsService) : Controller
    {
        /// <summary>
        /// 显示浏览器控制台页面（已将浏览器页面合并到 Home 区）。<br/>
        /// Shows the browser console page (browser UI merged into the Home area).
        /// </summary>
        public IActionResult Index() => View();

        /// <summary>
        /// 显示当前程序设置页面（GET）。<br/>
        /// Displays the current program settings page (GET).
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
        /// 保存程序设置（POST）。<br/>
        /// Save program settings (POST).
        /// </summary>
        /// <param name="model">一个包含程序设置的视图模型。<br/>A view model containing program settings.</param>
        /// <returns>重定向或带模型错误的视图。<br/>Redirects or returns the view with model errors.</returns>
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
        /// 显示日志页面（视图）。<br/>
        /// Displays the logs page (view).
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
        /// 清空日志文件并重定向回日志页面（保留用于表单提交回退）。<br/>
        /// Clears the log file and redirects back to the logs page (kept for form POST fallback).
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLogs()
        {
            await appLogService.ClearAsync();
            TempData["StatusMessage"] = "日志已清空。";
            return RedirectToAction(nameof(Logs));
        }

        /// <summary>
        /// 隐私页展示。<br/>
        /// Displays the privacy page.
        /// </summary>
        public IActionResult Privacy() => View();

        /// <summary>
        /// Show plugin upload/manage page (moved from Browser area to Settings sidebar).<br/>
        /// 显示插件上传/管理页面（从 Browser 区迁移到 Settings 侧边栏）。
        /// </summary>
        public IActionResult PluginUpload() => View();

        /// <summary>
        /// 错误页面展示接口。<br/>
        /// Error page display endpoint.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
        }
    }
}
