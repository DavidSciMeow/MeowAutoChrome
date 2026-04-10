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
    /// 枚举当前已检测到的 Playwright 浏览器条目。<br/>
    /// Enumerate currently detected Playwright browser entries.
    /// </summary>
    IReadOnlyList<PlaywrightInstalledBrowser> GetInstalledBrowsers();

    /// <summary>
    /// 校验手动选择的 Chromium 离线压缩包是否可用于导入。<br/>
    /// Validate whether a manually selected Chromium offline archive can be imported.
    /// </summary>
    PlaywrightArchiveValidationResult ValidateOfflineArchive(string? archivePath);

    /// <summary>
    /// 导入 Playwright Chromium 运行时压缩包。<br/>
    /// Import a Playwright Chromium runtime archive.
    /// </summary>
    /// <param name="mode">保留的兼容参数；当前只支持 manual-archive。<br/>Reserved compatibility parameter; only manual-archive is supported now.</param>
    /// <param name="browser">保留的兼容参数；当前只支持 chromium。<br/>Reserved compatibility parameter; only chromium is supported now.</param>
    /// <param name="archivePath">手动选择的离线压缩包路径。<br/>Manually selected offline archive path.</param>
    Task<PlaywrightRuntimeStatus> InstallAsync(string mode = "auto", string browser = "chromium", string? archivePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 卸载应用管理的 Playwright Chromium 运行时。<br/>
    /// Uninstall the Playwright Chromium runtime managed by the application.
    /// </summary>
    Task<PlaywrightRuntimeStatus> UninstallAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 卸载某个指定来源的 Playwright 浏览器条目。<br/>
    /// Uninstall a specific Playwright browser entry from the specified source.
    /// </summary>
    Task<PlaywrightRuntimeStatus> UninstallBrowserAsync(string browser, string? runtimeSource = null, CancellationToken cancellationToken = default);

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