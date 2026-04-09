using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Playwright 运行时管理服务，负责检测、安装与卸载应用使用的浏览器运行时。<br/>
/// Playwright runtime management service responsible for detecting, installing, and uninstalling the browser runtime used by the application.
/// </summary>
public interface IPlaywrightRuntimeService
{
    /// <summary>
    /// 读取当前 Playwright 运行时状态。<br/>
    /// Read the current Playwright runtime status.
    /// </summary>
    PlaywrightRuntimeStatus GetStatus();

    /// <summary>
    /// 安装应用所需的 Playwright Chromium 运行时。<br/>
    /// Install the Playwright Chromium runtime required by the application.
    /// </summary>
    /// <param name="mode">安装模式，支持 auto、online 或 offline。<br/>Installation mode, supported values are auto, online, or offline.</param>
    Task<PlaywrightRuntimeStatus> InstallAsync(string mode = "auto", CancellationToken cancellationToken = default);

    /// <summary>
    /// 卸载应用管理的 Playwright Chromium 运行时。<br/>
    /// Uninstall the Playwright Chromium runtime managed by the application.
    /// </summary>
    Task<PlaywrightRuntimeStatus> UninstallAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 彻底卸载机器上 Playwright 当前版本关联的全部浏览器缓存。<br/>
    /// Fully uninstall all browser caches associated with the current Playwright version on this machine.
    /// </summary>
    Task<PlaywrightRuntimeStatus> UninstallAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保 Playwright Chromium 运行时已经安装，否则抛出友好的异常。<br/>
    /// Ensure the Playwright Chromium runtime is installed or throw a user-friendly exception.
    /// </summary>
    Task EnsureInstalledAsync(CancellationToken cancellationToken = default);
}