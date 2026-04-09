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
    /// <summary>
    /// 读取当前 Playwright 运行时状态。<br/>
    /// Read the current Playwright runtime status.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(ToResponse(playwrightRuntime.GetStatus()));

    /// <summary>
    /// 安装 Playwright Chromium 运行时。<br/>
    /// Install the Playwright Chromium runtime.
    /// </summary>
    [HttpPost("install")]
    public async Task<IActionResult> Install([FromQuery] string? mode = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await playwrightRuntime.InstallAsync(mode ?? "auto", cancellationToken);
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

    private static object ToResponse(PlaywrightRuntimeStatus status) => new
    {
        isInstalled = status.IsInstalled,
        installationRequired = status.InstallationRequired,
        browserInstallDirectory = status.BrowserInstallDirectory,
        browserExecutablePath = status.BrowserExecutablePath,
        scriptPath = status.ScriptPath,
        runtimeSource = status.RuntimeSource,
        canUninstallFromApp = status.CanUninstallFromApp,
        offlinePackageAvailable = status.OfflinePackageAvailable,
        offlinePackagePath = status.OfflinePackagePath,
        message = status.Message,
        output = status.OperationOutput
    };
}