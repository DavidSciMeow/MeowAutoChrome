namespace MeowAutoChrome.Core.Models;

/// <summary>
/// Playwright 运行时状态快照。<br/>
/// Snapshot of the Playwright runtime state.
/// </summary>
/// <param name="IsInstalled">是否已检测到 Chromium 可执行文件。<br/>Whether a Chromium executable was detected.</param>
/// <param name="InstallationRequired">当前是否必须先安装。<br/>Whether installation is required before use.</param>
/// <param name="BrowserInstallDirectory">浏览器安装目录。<br/>Browser installation directory.</param>
/// <param name="BrowserExecutablePath">检测到的浏览器可执行文件路径。<br/>Detected browser executable path.</param>
/// <param name="ScriptPath">用于安装/卸载的 Playwright 脚本路径。<br/>Playwright script path used for install/uninstall.</param>
/// <param name="RuntimeSource">当前生效运行时来源。<br/>Source of the active runtime.</param>
/// <param name="CanUninstallFromApp">普通应用内卸载是否可直接删除当前来源。<br/>Whether the regular in-app uninstall can directly remove the current source.</param>
/// <param name="OfflinePackageAvailable">是否检测到可用于离线安装的浏览器压缩包。<br/>Whether an offline browser archive is available for installation.</param>
/// <param name="OfflinePackagePath">离线安装包路径。<br/>Path of the offline installation archive.</param>
/// <param name="Message">状态说明。<br/>Status message.</param>
/// <param name="OperationOutput">最近一次安装/卸载脚本输出。<br/>Output of the most recent install/uninstall script run.</param>
public sealed record PlaywrightRuntimeStatus(
    bool IsInstalled,
    bool InstallationRequired,
    string BrowserInstallDirectory,
    string? BrowserExecutablePath,
    string? ScriptPath,
    string RuntimeSource,
    bool CanUninstallFromApp,
    bool OfflinePackageAvailable,
    string? OfflinePackagePath,
    string? Message,
    string? OperationOutput);