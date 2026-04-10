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

/// <summary>
/// 手动导入的 Playwright Chromium 压缩包校验结果。<br/>
/// Validation result for a manually selected Playwright Chromium archive.
/// </summary>
/// <param name="IsValid">整体是否可用于导入。<br/>Whether the archive is valid for import.</param>
/// <param name="Code">校验结果代码。<br/>Validation result code.</param>
/// <param name="ArchivePath">被校验的路径。<br/>Validated path.</param>
/// <param name="Exists">文件是否存在。<br/>Whether the file exists.</param>
/// <param name="FileNameMatches">文件名是否为 chrome-win64.zip。<br/>Whether the file name matches chrome-win64.zip.</param>
/// <param name="ArchiveReadable">压缩包是否可正常读取。<br/>Whether the archive can be opened successfully.</param>
/// <param name="ContainsExpectedLayout">内容是否包含 chrome-win64/chrome.exe。<br/>Whether the archive contains chrome-win64/chrome.exe.</param>
/// <param name="Summary">简短总结。<br/>Short summary.</param>
/// <param name="Detail">详细说明。<br/>Detailed description.</param>
public sealed record PlaywrightArchiveValidationResult(
    bool IsValid,
    string Code,
    string? ArchivePath,
    bool Exists,
    bool FileNameMatches,
    bool ArchiveReadable,
    bool ContainsExpectedLayout,
    string Summary,
    string Detail);

/// <summary>
/// 单个已安装 Playwright 浏览器条目。<br/>
/// Single installed Playwright browser entry.
/// </summary>
/// <param name="Id">条目唯一标识。<br/>Unique entry id.</param>
/// <param name="Browser">浏览器名称，例如 chromium、firefox、webkit。<br/>Browser name such as chromium, firefox, webkit.</param>
/// <param name="Label">显示名称。<br/>Display label.</param>
/// <param name="RuntimeSource">来源，例如 managed、global。<br/>Runtime source such as managed or global.</param>
/// <param name="InstallDirectory">安装根目录。<br/>Installation root directory.</param>
/// <param name="ExecutablePath">检测到的可执行文件路径（若适用）。<br/>Detected executable path when applicable.</param>
/// <param name="CanUninstallFromApp">是否允许在应用内直接删除。<br/>Whether the entry can be removed directly from the app.</param>
/// <param name="IsActive">当前是否为应用命中的有效运行时。<br/>Whether the entry is currently the active runtime used by the app.</param>
public sealed record PlaywrightInstalledBrowser(
    string Id,
    string Browser,
    string Label,
    string? Version,
    string RuntimeSource,
    string InstallDirectory,
    string? ExecutablePath,
    bool CanUninstallFromApp,
    bool IsActive);