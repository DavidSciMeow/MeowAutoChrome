using Microsoft.AspNetCore.Mvc;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.WebAPI.Services;

namespace MeowAutoChrome.WebAPI.Controllers.Api;

[ApiController]
[Route("api/playwright")]
/// <summary>
/// Playwright 运行时管理 API，负责查询状态、安装和卸载应用使用的 Chromium 运行时。<br/>
/// Playwright runtime management API responsible for reading status, installing, and uninstalling the Chromium runtime used by the app.
/// </summary>
public class PlaywrightController(IPlaywrightRuntimeService playwrightRuntime, BrowserInstanceManager browserInstances, ScreencastServiceCore screencastService) : ControllerBase
{
    private const string RequiredBrowser = "chromium";
    private const string ManualArchiveExpectedFileName = "chrome-win64.zip";

    /// <summary>
    /// 读取当前 Playwright 运行时状态。<br/>
    /// Read the current Playwright runtime status.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(ToResponse(playwrightRuntime.GetStatus()));

    /// <summary>
    /// 校验用户手动选择的离线 Chromium 压缩包。<br/>
    /// Validate a user-selected offline Chromium archive.
    /// </summary>
    [HttpPost("validate-archive")]
    public IActionResult ValidateArchive([FromBody] PlaywrightArchiveValidationRequest? request)
        => Ok(ToValidationResponse(playwrightRuntime.ValidateOfflineArchive(request?.ArchivePath)));

    /// <summary>
    /// 安装 Playwright 浏览器运行时。<br/>
    /// Install Playwright browser runtimes.
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> Install([FromBody] PlaywrightInstallRequest? request = null, [FromQuery] string? mode = null, [FromQuery] string? browser = null, [FromQuery] string? archivePath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveArchivePath = request?.ArchivePath ?? archivePath;
            var status = await playwrightRuntime.InstallAsync(
                "manual-archive",
                RequiredBrowser,
                effectiveArchivePath,
                cancellationToken);
            var operationState = status.Message?.Contains("已停止重复安装", StringComparison.Ordinal) == true
                ? "skipped"
                : "completed";
            return Ok(ToResponse(status, operationState));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 卸载指定的 Playwright 浏览器条目。卸载前会先关闭所有浏览器实例。<br/>
    /// Uninstall a specific Playwright browser entry. All browser instances are closed before uninstall.
    /// </summary>
    [HttpPost("uninstall-browser")]
    public async Task<IActionResult> UninstallBrowser([FromBody] PlaywrightBrowserUninstallRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var instance in browserInstances.GetInstances())
            {
                await browserInstances.CloseBrowserInstanceAsync(instance.Id, cancellationToken);
            }

            await screencastService.RefreshTargetAsync();

            var status = await playwrightRuntime.UninstallBrowserAsync(request.Browser, request.RuntimeSource, cancellationToken);
            return Ok(ToResponse(status));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// 卸载 Playwright Chromium 运行时。卸载前会先关闭所有浏览器实例。<br/>
    /// Uninstall the Playwright Chromium runtime. All browser instances are closed before uninstall.
    /// </summary>
    [HttpPost("uninstall")]
    public async Task<IActionResult> Uninstall([FromQuery] bool all = false, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var instance in browserInstances.GetInstances())
            {
                await browserInstances.CloseBrowserInstanceAsync(instance.Id, cancellationToken);
            }

            await screencastService.RefreshTargetAsync();

            var status = all
                ? await playwrightRuntime.UninstallAllAsync(cancellationToken)
                : await playwrightRuntime.UninstallAsync(cancellationToken);
            return Ok(ToResponse(status));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private object ToResponse(PlaywrightRuntimeStatus status, string? operationState = null) => new
    {
        isInstalled = status.IsInstalled,
        installationRequired = status.InstallationRequired,
        browserInstallDirectory = status.BrowserInstallDirectory,
        browserExecutablePath = status.BrowserExecutablePath,
        scriptPath = status.ScriptPath,
        runtimeSource = status.RuntimeSource,
        requiredBrowser = RequiredBrowser,
        canUninstallFromApp = status.CanUninstallFromApp,
        offlinePackageAvailable = status.OfflinePackageAvailable,
        offlinePackagePath = status.OfflinePackagePath,
        installedBrowsers = playwrightRuntime.GetInstalledBrowsers().Select(item => new
        {
            id = item.Id,
            browser = item.Browser,
            label = item.Label,
            version = item.Version,
            runtimeSource = item.RuntimeSource,
            installDirectory = item.InstallDirectory,
            executablePath = item.ExecutablePath,
            canUninstallFromApp = item.CanUninstallFromApp,
            isActive = item.IsActive
        }).ToArray(),
        manualArchive = new
        {
            supportedBrowser = RequiredBrowser,
            expectedFileName = ManualArchiveExpectedFileName,
            description = "当前只支持手动导入 Chrome for Testing 的 Windows x64 压缩包。"
        },
        downloadLinks = new[]
        {
            new { label = "Chrome for Testing 下载页", url = "https://googlechromelabs.github.io/chrome-for-testing/", description = "手动下载 chrome-win64.zip" },
            new { label = "Playwright 浏览器文档", url = "https://playwright.dev/dotnet/docs/browsers", description = "查看 chromium 运行时与 chrome-win64.zip 相关说明" }
        },
        operationState,
        message = status.Message,
        output = status.OperationOutput
    };

    private static object ToValidationResponse(PlaywrightArchiveValidationResult result) => new
    {
        isValid = result.IsValid,
        code = result.Code,
        archivePath = result.ArchivePath,
        exists = result.Exists,
        fileNameMatches = result.FileNameMatches,
        archiveReadable = result.ArchiveReadable,
        containsExpectedLayout = result.ContainsExpectedLayout,
        summary = result.Summary,
        detail = result.Detail
    };
}

public sealed record PlaywrightInstallRequest(string? Mode, string? Browser, string? ArchivePath);
public sealed record PlaywrightArchiveValidationRequest(string? ArchivePath);
public sealed record PlaywrightBrowserUninstallRequest(string Browser, string? RuntimeSource);