using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Struct;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MeowAutoChrome.Web.Controllers
{
    /// <summary>
    /// 主页控制器（用于设置、日志和隐私页面），负责处理程序设置的保存与验证，以及日志的展示与清理。
    /// </summary>
public class HomeController(MeowAutoChrome.Core.Interface.IProgramSettingsProvider programSettingsService, ScreencastService screencastService, Core.Services.AppLogService appLogService, BrowserInstanceManager browserInstances) : Controller
    {
        /// <summary>
        /// 将 FPS 值转换为帧间隔（毫秒），最小支持 16ms。
        /// </summary>
        /// <param name="fps">FPS值</param>
        /// <returns></returns>
        private static int FpsToInterval(int fps)
            => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

        /// <summary>
        /// 将 AppLogEntry 转换为前端展示用的 LogEntryViewModel。
        /// </summary>
        /// <param name="entry">日志模型</param>
        /// <returns></returns>
        private static LogEntryViewModel ToLogEntryViewModel(Core.Models.AppLogEntry entry)
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

        /// <summary>
        /// 验证用户提交的程序设置视图模型的有效性（例如 SearchUrlTemplate 与用户数据目录）。
        /// </summary>
        /// <param name="model">程序设置视图模型</param>
        private void ValidateProgramSettings(ProgramSettingsViewModel model)
        {
            if (!model.SearchUrlTemplate.Contains("{query}", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(model.SearchUrlTemplate), "搜索地址模板必须包含 {query} 占位符。");

            try
            {
                var primary = browserInstances.CurrentInstance;
                if (primary is not null)
                {
                    var currentUserDataDirectory = Path.GetFullPath(primary.UserDataDirectoryPath);
                    var targetUserDataDirectory = Path.GetFullPath(model.UserDataDirectory);

                    if (IsNestedDirectory(currentUserDataDirectory, targetUserDataDirectory) || IsNestedDirectory(targetUserDataDirectory, currentUserDataDirectory))
                        ModelState.AddModelError(nameof(model.UserDataDirectory), "浏览器用户数据目录不能设置为当前目录的子目录或父目录。");
                }

                // Validate custom settings keys: must match allowed pattern and have no duplicates
                if (model.CustomSettings is not null && model.CustomSettings.Count > 0)
                {
                    var keyPattern = new System.Text.RegularExpressions.Regex("^[A-Za-z0-9_.-]+$");
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in model.CustomSettings)
                    {
                        var key = kv.Key?.Trim() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            ModelState.AddModelError(nameof(model.CustomSettings), "自定义设置的键不能为空。请删除空行或填写键名。");
                            break;
                        }

                        if (!keyPattern.IsMatch(key))
                        {
                            ModelState.AddModelError(nameof(model.CustomSettings), $"自定义键 '{key}' 包含非法字符。允许的字符为字母、数字、下划线、破折号和点（A-Z a-z 0-9 _ - .）。");
                            break;
                        }

                        if (!seen.Add(key))
                        {
                            ModelState.AddModelError(nameof(model.CustomSettings), $"自定义键 '{key}' 重复。请删除或合并重复项。");
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UserDataDirectory), "浏览器用户数据目录无效。");
            }
        }

        /// <summary>
        /// 判断 childPath 是否为 parentPath 的子目录（用于避免将用户数据目录设置为当前目录的父/子目录）。
        /// </summary>
        /// <param name="parentPath">父级路径</param>
        /// <param name="childPath">子级路径</param>
        /// <returns></returns>
        private static bool IsNestedDirectory(string parentPath, string childPath)
        {
            var normalizedParentPath = Path.TrimEndingDirectorySeparator(parentPath);
            var normalizedChildPath = Path.TrimEndingDirectorySeparator(childPath);
            return normalizedChildPath.StartsWith(normalizedParentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedChildPath.StartsWith(normalizedParentPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 保存程序设置并在必要时同步到浏览器实例（例如切换 user-data-dir 或 headless）。
        /// </summary>
        /// <param name="model">一个包含程序设置的视图模型。</param>
        /// <returns></returns>
        private async Task<string> SaveProgramSettingsAsync(ProgramSettingsViewModel model)
        {
            var previousSettings = await programSettingsService.GetAsync();
            var settings = new ProgramSettings
            {
                SearchUrlTemplate = model.SearchUrlTemplate,
                ScreencastFps = model.ScreencastFps,
                PluginPanelWidth = model.PluginPanelWidth,
                UserDataDirectory = model.UserDataDirectory,
                UserAgent = model.UserAgent,
                AllowInstanceUserAgentOverride = model.AllowInstanceUserAgentOverride,
                Headless = model.Headless
            };

            await programSettingsService.SaveAsync(settings);

            // Inject any web-specific custom settings (exposed via UI) into Core provider
            try
            {
                // Example: UI exposes a small set of web-specific flags in viewmodel.CustomSettings
                // Map them into provider's custom settings. If viewmodel has none, skip.
                if (model.CustomSettings != null && model.CustomSettings.Count > 0)
                {
                    await programSettingsService.InjectCustomSettingsAsync(model.CustomSettings);
                }
            }
            catch
            {
                // non-fatal: do not fail saving main settings if injection fails
            }

            var userDataDirectoryChanged = !string.Equals(previousSettings.UserDataDirectory, settings.UserDataDirectory, StringComparison.OrdinalIgnoreCase);
            var headlessChanged = previousSettings.Headless != settings.Headless;
            var userAgentChanged = !string.Equals(previousSettings.UserAgent, settings.UserAgent, StringComparison.Ordinal);
            var userAgentOverrideChanged = previousSettings.AllowInstanceUserAgentOverride != settings.AllowInstanceUserAgentOverride;
            var launchSettingsChanged = userDataDirectoryChanged || headlessChanged || userAgentChanged || userAgentOverrideChanged;
            if (launchSettingsChanged)
                await browserInstances.UpdateLaunchSettingsAsync(settings.UserDataDirectory, settings.Headless, forceReload: true);

            await screencastService.UpdateSettingsAsync(
                screencastService.RequestedEnabled,
                screencastService.MaxWidth,
                screencastService.MaxHeight,
                FpsToInterval(model.ScreencastFps));

            if (launchSettingsChanged)
                await screencastService.OnBrowserModeChangedAsync();

            var changes = new List<string>();
            if (userDataDirectoryChanged)
                changes.Add("浏览器用户数据目录已切换");
            if (headlessChanged)
                changes.Add("Headless 模式已切换");
            if (userAgentChanged || userAgentOverrideChanged)
                changes.Add("User-Agent 配置已同步到实例");

            if (changes.Count > 0)
                return $"设置已自动保存，{string.Join('，', changes)}。";

            return "设置已自动保存。";
        }

        /// <summary>
        /// 重定向到浏览器页面。
        /// </summary>
        public IActionResult Index()
            => RedirectToAction("Index", "Browser");

        // CustomSettings feature removed: custom keys are internal and exposed only via Contracts.

        /// <summary>
        /// 显示当前程序设置页面（GET）。
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
                CustomSettings = settings.CustomSettings ?? new System.Collections.Generic.Dictionary<string, string?>()
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

        /// <summary>
        /// 显示日志页面。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logs()
        {
            return View(new LogPageViewModel
            {
                LogDisplayPath = appLogService.LogDisplayPath,
                Entries = (await appLogService.ReadRecentEntriesAsync()).Select(e => ToLogEntryViewModel(e)).ToArray(),
                LastUpdatedUtc = appLogService.GetLastWriteTime()
            });
        }

        /// <summary>
        /// 获取日志页面的内容（JSON），供前端定期刷新使用。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> LogsContent()
        {
            var entries = (await appLogService.ReadRecentEntriesAsync()).Select(e => ToLogEntryViewModel(e)).ToArray();

            return Json(new
            {
                entries,
                lastUpdatedLocal = appLogService.GetLastWriteTime()?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        /// <summary>
        /// 清空日志文件并重定向回日志页面。
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLogs()
        {
            await appLogService.ClearAsync();
            TempData["StatusMessage"] = "日志已清空。";
            return RedirectToAction(nameof(Logs));
        }

        /// <summary>
        /// 自动保存设置的 AJAX 接口，返回 JSON 结果。
        /// </summary>
        /// <param name="model">一个包含程序设置的视图模型。</param>
        /// <returns></returns>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSaveSettings(ProgramSettingsViewModel model)
        {
            ValidateProgramSettings(model);

            if (!ModelState.IsValid)
            {
                var errorMessage = ModelState.Values
                    .SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
                    .FirstOrDefault() ?? "设置保存失败。";

                return Problem(detail: errorMessage, title: "InvalidRequest", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var message = await SaveProgramSettingsAsync(model);
                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return Problem(detail: ex.Message, title: "ServerError", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// 隐私页展示。
        /// </summary>
        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// 错误页面展示接口。
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
        }

        // Legacy parsing helper removed from controller — parsing is handled by AppLogService in Core.
    }
}
