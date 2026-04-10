using System.Diagnostics;
using System.IO.Compression;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Struct;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 由 Core 直接管理 Chromium 运行时，并将浏览器文件固定到应用自己的 AppData 目录。<br/>
/// Manage the Chromium runtime directly from Core and pin browser files to the app-owned AppData directory.
/// </summary>
public sealed class PlaywrightRuntimeService(ILogger<PlaywrightRuntimeService> logger) : IPlaywrightRuntimeService
{
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly string _managedBrowserInstallDirectory = ProgramSettings.GetPlaywrightBrowserDirectoryPath();
    private readonly string _bundledBrowserInstallDirectory = Path.Combine(AppContext.BaseDirectory, "playwright-browsers");
    private static readonly string _globalBrowserInstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
    private const string BrowserChromium = "chromium";
    private const string BrowserFirefox = "firefox";
    private const string BrowserWebkit = "webkit";
    private const string RuntimeSourceBundled = "bundled";
    private const string RuntimeSourceManaged = "managed";
    private const string RuntimeSourceManagedOffline = "managed-offline";
    private const string RuntimeSourceGlobal = "global";
    private const string RuntimeSourceMissing = "missing";
    private const string InstallModeAuto = "auto";
    private const string InstallModeOnline = "online";
    private const string InstallModeManualArchive = "manual-archive";
    private const string InstallModeOfflineLegacy = "offline";
    private const string OfflineArchiveFileName = "chrome-win64.zip";
    private readonly string _managedRuntimeMarkerPath = Path.Combine(ProgramSettings.GetPlaywrightBrowserDirectoryPath(), ".meow-install-source");

    public PlaywrightRuntimeStatus GetStatus()
    {
        var (activeBrowserInstallDirectory, runtimeSource) = ResolveActiveBrowserInstallDirectory();
        ConfigureEnvironment(activeBrowserInstallDirectory);

        var installerPath = ResolveInstallerPath();
        var browserExecutablePath = FindInstalledBrowserExecutable(activeBrowserInstallDirectory);
        var isInstalled = !string.IsNullOrWhiteSpace(browserExecutablePath);
        var canUninstallFromApp = runtimeSource is RuntimeSourceManaged or RuntimeSourceManagedOffline or RuntimeSourceBundled;
        var message = isInstalled
            ? runtimeSource switch
            {
                RuntimeSourceBundled => "Playwright Chromium 已安装，当前使用的是随应用打包的离线浏览器。",
                RuntimeSourceManaged => "Playwright Chromium 已安装，当前使用的是应用私有运行时。",
                RuntimeSourceManagedOffline => "Playwright Chromium 已安装，当前使用的是从离线压缩包解压到应用私有目录的浏览器。",
                RuntimeSourceGlobal => "Playwright Chromium 已安装，但当前命中的是系统全局缓存。普通卸载不会删除它。",
                _ => "Playwright Chromium 已安装。"
            }
            : installerPath is null
                ? "未找到可用的 Playwright 安装器入口。"
                : "Playwright Chromium 尚未安装。请选择你手动下载的 chrome-win64.zip 进行导入，下方提供了相关下载链接。";

        return new PlaywrightRuntimeStatus(
            isInstalled,
            !isInstalled,
            activeBrowserInstallDirectory,
            browserExecutablePath,
            installerPath,
            runtimeSource,
            canUninstallFromApp,
            false,
            null,
            message,
            null);
    }

    public IReadOnlyList<PlaywrightInstalledBrowser> GetInstalledBrowsers()
    {
        var (activeBrowserInstallDirectory, activeRuntimeSource) = ResolveActiveBrowserInstallDirectory();
        var entries = new List<PlaywrightInstalledBrowser>();

        foreach (var candidate in GetRuntimeCandidates())
        {
            if (!Directory.Exists(candidate.Directory))
                continue;

            entries.AddRange(DetectInstalledBrowsers(candidate.Directory, candidate.RuntimeSource, activeBrowserInstallDirectory, activeRuntimeSource));
        }

        return entries
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.RuntimeSource, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public PlaywrightArchiveValidationResult ValidateOfflineArchive(string? archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return new PlaywrightArchiveValidationResult(
                false,
                "missing-path",
                null,
                false,
                false,
                false,
                false,
                "尚未选择压缩包",
                "请先选择你手动下载的 chrome-win64.zip。当前离线导入只支持 Chrome for Testing 的 Windows x64 压缩包。");
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(archivePath.Trim());
        }
        catch (Exception ex)
        {
            return new PlaywrightArchiveValidationResult(
                false,
                "invalid-path",
                archivePath,
                false,
                false,
                false,
                false,
                "路径无效",
                $"提供的文件路径无法解析。{ex.Message}");
        }

        if (!File.Exists(normalizedPath))
        {
            return new PlaywrightArchiveValidationResult(
                false,
                "file-not-found",
                normalizedPath,
                false,
                false,
                false,
                false,
                "找不到该文件",
                "所选路径下不存在文件。请确认文件没有被移动、删除，且当前账号有读取权限。");
        }

        var fileNameMatches = string.Equals(Path.GetFileName(normalizedPath), OfflineArchiveFileName, StringComparison.OrdinalIgnoreCase);
        if (!fileNameMatches)
        {
            return new PlaywrightArchiveValidationResult(
                false,
                "file-name-mismatch",
                normalizedPath,
                true,
                false,
                false,
                false,
                "文件名不匹配",
                "当前离线导入只接受 Chrome for Testing 的 chrome-win64.zip。你选择的文件存在，但文件名不是 chrome-win64.zip。");
        }

        try
        {
            using var archive = ZipFile.OpenRead(normalizedPath);
            var hasChromeExecutable = archive.Entries.Any(entry =>
                entry.FullName.Replace('\\', '/').StartsWith("chrome-win64/", StringComparison.OrdinalIgnoreCase)
                && entry.FullName.Replace('\\', '/').EndsWith("/chrome.exe", StringComparison.OrdinalIgnoreCase));

            if (!hasChromeExecutable)
            {
                return new PlaywrightArchiveValidationResult(
                    false,
                    "archive-layout-mismatch",
                    normalizedPath,
                    true,
                    true,
                    true,
                    false,
                    "文件名正确，但内容不对",
                    "这个文件名虽然是 chrome-win64.zip，但压缩包内部没有找到 chrome-win64/chrome.exe，看起来不是有效的 Chrome for Testing Windows x64 压缩包。");
            }

            return new PlaywrightArchiveValidationResult(
                true,
                "ok",
                normalizedPath,
                true,
                true,
                true,
                true,
                "校验通过，可用于导入",
                "已确认文件存在、文件名正确，且压缩包内部包含 chrome-win64/chrome.exe。可以直接执行手动导入。");
        }
        catch (InvalidDataException ex)
        {
            return new PlaywrightArchiveValidationResult(
                false,
                "archive-unreadable",
                normalizedPath,
                true,
                true,
                false,
                false,
                "文件名正确，但压缩包无法读取",
                $"这个文件名是正确的，但它不是有效的 zip 文件，或者文件已经损坏。{ex.Message}");
        }
        catch (Exception ex)
        {
            return new PlaywrightArchiveValidationResult(
                false,
                "archive-open-failed",
                normalizedPath,
                true,
                true,
                false,
                false,
                "无法读取压缩包",
                $"尝试打开压缩包时失败。{ex.Message}");
        }
    }

    public async Task<PlaywrightRuntimeStatus> InstallAsync(string mode = InstallModeAuto, string browser = BrowserChromium, string? archivePath = null, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var normalizedMode = NormalizeInstallMode(mode);
            var normalizedBrowser = NormalizeBrowserName(browser);

            if (string.IsNullOrWhiteSpace(archivePath))
                throw new InvalidOperationException("在线安装已移除。请先下载 chrome-win64.zip，然后选择本地压缩包导入。");

            if (!string.Equals(normalizedMode, InstallModeManualArchive, StringComparison.Ordinal))
                throw new InvalidOperationException("当前只支持手动压缩包导入。请先下载 chrome-win64.zip，然后选择本地压缩包导入。");

            if (!string.Equals(normalizedBrowser, BrowserChromium, StringComparison.Ordinal))
                throw new InvalidOperationException("当前只支持导入 Chromium 的 chrome-win64.zip。请使用 Chrome for Testing 的 Windows x64 压缩包。");

            var existingInstall = GetInstalledBrowsers()
                .FirstOrDefault(item => string.Equals(item.Browser, normalizedBrowser, StringComparison.OrdinalIgnoreCase));
            if (existingInstall is not null)
            {
                var current = GetStatus();
                var versionSuffix = string.IsNullOrWhiteSpace(existingInstall.Version)
                    ? string.Empty
                    : $"（版本 {existingInstall.Version}）";
                return current with
                {
                    Message = $"已检测到 {GetBrowserDisplayName(normalizedBrowser)} 已安装{versionSuffix}，已停止重复安装。"
                };
            }

            ConfigureEnvironment(_managedBrowserInstallDirectory);
            return await InstallFromOfflineArchiveAsync(archivePath, cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PlaywrightRuntimeStatus> UninstallAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var current = GetStatus();
            var activeInstallDirectory = current.BrowserInstallDirectory;

            if (string.Equals(current.RuntimeSource, RuntimeSourceGlobal, StringComparison.OrdinalIgnoreCase))
            {
                return current with
                {
                    Message = "当前使用的是系统全局 Playwright 缓存。普通卸载只会删除应用自己的运行时；如果你要测试纯离线重装，请执行“彻底卸载全部缓存”。"
                };
            }

            ConfigureEnvironment(activeInstallDirectory);

            string? output = null;
            if (string.Equals(current.RuntimeSource, RuntimeSourceManaged, StringComparison.OrdinalIgnoreCase)
                && string.Equals(activeInstallDirectory, _managedBrowserInstallDirectory, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(current.ScriptPath))
            {
                logger.LogInformation("Uninstalling Playwright Chromium from {BrowserInstallDirectory}", activeInstallDirectory);

                var result = await RunPlaywrightCliAsync(activeInstallDirectory, ["uninstall"], cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Playwright 卸载失败。{result.Output}".Trim());
                }

                output = result.Output;
            }

            try
            {
                if (Directory.Exists(activeInstallDirectory))
                    Directory.Delete(activeInstallDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete Playwright browser directory {BrowserInstallDirectory}", activeInstallDirectory);
            }

            var updated = GetStatus();
            return updated with
            {
                Message = updated.IsInstalled ? "卸载后仍检测到其他位置的 Chromium 浏览器文件。" : "Playwright Chromium 已卸载。",
                OperationOutput = output
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PlaywrightRuntimeStatus> UninstallBrowserAsync(string browser, string? runtimeSource = null, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var normalizedBrowser = NormalizeBrowserName(browser);
            var entries = GetInstalledBrowsers();
            var target = entries.FirstOrDefault(entry =>
                string.Equals(entry.Browser, normalizedBrowser, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(runtimeSource) || string.Equals(entry.RuntimeSource, runtimeSource, StringComparison.OrdinalIgnoreCase)));

            if (target is null)
                throw new InvalidOperationException($"未找到可卸载的 {GetBrowserDisplayName(normalizedBrowser)} 安装项。");

            if (!target.CanUninstallFromApp)
                throw new InvalidOperationException($"当前来源 {target.RuntimeSource} 不支持在应用内直接删除 {GetBrowserDisplayName(normalizedBrowser)}。");

            logger.LogInformation("Uninstalling Playwright browser {Browser} from source {RuntimeSource}", normalizedBrowser, target.RuntimeSource);

            string? output = null;
            if ((string.Equals(target.RuntimeSource, RuntimeSourceManaged, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(target.RuntimeSource, RuntimeSourceGlobal, StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(ResolveInstallerPath()))
            {
                var result = await RunPlaywrightCliAsync(target.InstallDirectory, ["uninstall", normalizedBrowser], cancellationToken);
                if (result.ExitCode != 0)
                    throw new InvalidOperationException($"卸载 {GetBrowserDisplayName(normalizedBrowser)} 失败。{result.Output}".Trim());

                output = result.Output;
            }

            DeleteBrowserArtifacts(target.InstallDirectory, normalizedBrowser);

            if (string.Equals(target.InstallDirectory, _managedBrowserInstallDirectory, StringComparison.OrdinalIgnoreCase))
            {
                var remainingManagedBrowsers = DetectInstalledBrowsers(_managedBrowserInstallDirectory, ResolveManagedRuntimeSource(), _managedBrowserInstallDirectory, ResolveManagedRuntimeSource());
                if (remainingManagedBrowsers.Count == 0)
                {
                    try
                    {
                        if (File.Exists(_managedRuntimeMarkerPath))
                            File.Delete(_managedRuntimeMarkerPath);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to remove Playwright runtime marker {MarkerPath}", _managedRuntimeMarkerPath);
                    }
                }
                else if (remainingManagedBrowsers.Any(item => string.Equals(item.Browser, BrowserChromium, StringComparison.OrdinalIgnoreCase) && item.ExecutablePath?.Contains("chrome-win64", StringComparison.OrdinalIgnoreCase) == true))
                {
                    WriteManagedRuntimeMarker(RuntimeSourceManagedOffline);
                }
                else
                {
                    WriteManagedRuntimeMarker(RuntimeSourceManaged);
                }
            }

            var status = GetStatus();
            return status with
            {
                Message = $"{GetBrowserDisplayName(normalizedBrowser)} 已卸载。",
                OperationOutput = output
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<PlaywrightRuntimeStatus> UninstallAllAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var current = GetStatus();
            string? output = null;

            if (!string.IsNullOrWhiteSpace(current.ScriptPath))
            {
                logger.LogInformation("Fully uninstalling Playwright browsers from all known locations");

                var result = await RunPlaywrightCliAsync(_globalBrowserInstallDirectory, ["uninstall", "--all"], cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Playwright 彻底卸载失败。{result.Output}".Trim());
                }

                output = result.Output;
            }

            DeleteDirectoryIfExists(_bundledBrowserInstallDirectory);
            DeleteDirectoryIfExists(_managedBrowserInstallDirectory);
            DeleteDirectoryIfExists(_globalBrowserInstallDirectory);

            var updated = GetStatus();
            return updated with
            {
                Message = updated.IsInstalled ? "已执行彻底卸载，但系统中仍检测到可用浏览器文件。请检查是否存在其他自定义 PLAYWRIGHT_BROWSERS_PATH 目录。" : "Playwright 浏览器缓存已全部卸载，可用于重新验证离线安装。",
                OperationOutput = output
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public Task EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = GetStatus();
        if (status.IsInstalled)
            return Task.CompletedTask;

        throw new InvalidOperationException(status.Message ?? "Playwright Chromium 尚未安装，请先完成安装。");
    }

    private void ConfigureEnvironment(string browserInstallDirectory)
    {
        if (!string.IsNullOrWhiteSpace(browserInstallDirectory) && !Directory.Exists(browserInstallDirectory))
            Directory.CreateDirectory(browserInstallDirectory);

        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserInstallDirectory);
    }

    private string? ResolveInstallerPath()
    {
        var assemblyLocation = typeof(IPlaywright).Assembly.Location;
        if (string.IsNullOrWhiteSpace(assemblyLocation))
            return null;

        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
            return null;

        return File.Exists(Path.Combine(assemblyDirectory, "Microsoft.Playwright.dll")) ? assemblyDirectory : null;
    }

    private (string Directory, string RuntimeSource) ResolveActiveBrowserInstallDirectory()
    {
        var candidates = GetRuntimeCandidates();

        foreach (var candidate in candidates)
        {
            if (!Directory.Exists(candidate.Directory))
                continue;

            var executablePath = FindInstalledBrowserExecutable(candidate.Directory);
            if (!string.IsNullOrWhiteSpace(executablePath))
                return candidate;
        }

        return Directory.Exists(_bundledBrowserInstallDirectory)
            ? (_bundledBrowserInstallDirectory, RuntimeSourceBundled)
            : (_managedBrowserInstallDirectory, RuntimeSourceMissing);
    }

    private string? FindInstalledBrowserExecutable(string browserInstallDirectory)
    {
        if (!Directory.Exists(browserInstallDirectory))
            return null;

        var executableNames = OperatingSystem.IsWindows()
            ? new[] { "chrome.exe" }
            : OperatingSystem.IsMacOS()
                ? new[] { "Chromium", "chrome" }
                : new[] { "chrome" };

        try
        {
            return Directory
                .EnumerateFiles(browserInstallDirectory, "*", SearchOption.AllDirectories)
                .FirstOrDefault(path => executableNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to scan Playwright browser directory {BrowserInstallDirectory}", browserInstallDirectory);
            return null;
        }
    }

    private string NormalizeInstallMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? InstallModeAuto : mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            InstallModeAuto => InstallModeOnline,
            InstallModeOnline => InstallModeOnline,
            InstallModeManualArchive => InstallModeManualArchive,
            InstallModeOfflineLegacy => InstallModeManualArchive,
            _ => throw new InvalidOperationException($"Unsupported Playwright install mode '{mode}'.")
        };
    }

    private async Task<PlaywrightRuntimeStatus> InstallFromOfflineArchiveAsync(string? archivePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = ValidateOfflineArchive(archivePath);
        if (!validation.IsValid)
            throw new InvalidOperationException($"{validation.Summary}：{validation.Detail}");

        var offlinePackagePath = validation.ArchivePath!;

        logger.LogInformation("Installing Chromium from offline archive {OfflineArchivePath} into {BrowserInstallDirectory}", offlinePackagePath, _managedBrowserInstallDirectory);

        DeleteDirectoryIfExists(_managedBrowserInstallDirectory);
        Directory.CreateDirectory(_managedBrowserInstallDirectory);
        ZipFile.ExtractToDirectory(offlinePackagePath, _managedBrowserInstallDirectory, overwriteFiles: true);
        WriteManagedRuntimeMarker(RuntimeSourceManagedOffline);

        var updated = GetStatus();
        if (!updated.IsInstalled)
            throw new InvalidOperationException("离线压缩包已解压，但未检测到可用的 Chromium 可执行文件。请检查 chrome-win64.zip 内容是否正确。");

        return updated with
        {
            Message = "已从手动选择的 chrome-win64.zip 导入 Chromium。",
            OperationOutput = $"Extracted offline archive: {offlinePackagePath}"
        };
    }

    private string NormalizeBrowserName(string? browser)
    {
        var normalized = string.IsNullOrWhiteSpace(browser) ? BrowserChromium : browser.Trim().ToLowerInvariant();
        return normalized switch
        {
            BrowserChromium => BrowserChromium,
            BrowserFirefox => BrowserFirefox,
            BrowserWebkit => BrowserWebkit,
            _ => throw new InvalidOperationException($"Unsupported Playwright browser '{browser}'.")
        };
    }

    private string GetBrowserDisplayName(string browser)
        => browser switch
        {
            BrowserChromium => "Chromium",
            BrowserFirefox => "Firefox",
            BrowserWebkit => "WebKit",
            _ => browser
        };

    private (string Directory, string RuntimeSource)[] GetRuntimeCandidates()
        =>
        [
            (Directory: _bundledBrowserInstallDirectory, RuntimeSource: RuntimeSourceBundled),
            (Directory: _managedBrowserInstallDirectory, RuntimeSource: ResolveManagedRuntimeSource()),
            (Directory: _globalBrowserInstallDirectory, RuntimeSource: RuntimeSourceGlobal)
        ];

    private List<PlaywrightInstalledBrowser> DetectInstalledBrowsers(string directory, string runtimeSource, string activeBrowserInstallDirectory, string activeRuntimeSource)
    {
        var entries = new List<PlaywrightInstalledBrowser>();
        var canUninstallFromApp = runtimeSource is RuntimeSourceManaged or RuntimeSourceManagedOffline or RuntimeSourceGlobal or RuntimeSourceBundled;

        if (HasOfflineChromium(directory, out var offlineChromiumExecutable))
        {
            entries.Add(new PlaywrightInstalledBrowser(
                $"{runtimeSource}:chromium:offline",
                BrowserChromium,
                GetBrowserDisplayName(BrowserChromium),
                TryGetBrowserVersion(offlineChromiumExecutable, directory, BrowserChromium),
                runtimeSource,
                directory,
                offlineChromiumExecutable,
                canUninstallFromApp,
                string.Equals(directory, activeBrowserInstallDirectory, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(runtimeSource, activeRuntimeSource, StringComparison.OrdinalIgnoreCase)));
        }

        foreach (var browser in new[] { BrowserChromium, BrowserFirefox, BrowserWebkit })
        {
            if (!HasPlaywrightBrowserArtifacts(directory, browser, out var executablePath))
                continue;

            var id = $"{runtimeSource}:{browser}:playwright";
            if (entries.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))
                continue;

            entries.Add(new PlaywrightInstalledBrowser(
                id,
                browser,
                GetBrowserDisplayName(browser),
                TryGetBrowserVersion(executablePath, directory, browser),
                runtimeSource,
                directory,
                executablePath,
                canUninstallFromApp,
                string.Equals(browser, BrowserChromium, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(directory, activeBrowserInstallDirectory, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(runtimeSource, activeRuntimeSource, StringComparison.OrdinalIgnoreCase)));
        }

        return entries;
    }

    private bool HasOfflineChromium(string directory, out string? executablePath)
    {
        executablePath = null;
        var candidate = Path.Combine(directory, "chrome-win64", OperatingSystem.IsWindows() ? "chrome.exe" : "chrome");
        if (File.Exists(candidate))
        {
            executablePath = candidate;
            return true;
        }

        executablePath = FindInstalledBrowserExecutable(directory)?.Contains("chrome-win64", StringComparison.OrdinalIgnoreCase) == true
            ? FindInstalledBrowserExecutable(directory)
            : null;
        return !string.IsNullOrWhiteSpace(executablePath);
    }

    private bool HasPlaywrightBrowserArtifacts(string directory, string browser, out string? executablePath)
    {
        executablePath = null;
        var prefix = browser + "-";
        try
        {
            var browserDirectories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (!browserDirectories.Any())
                return false;

            executablePath = browser switch
            {
                BrowserChromium => browserDirectories
                    .Select(path => Directory.EnumerateFiles(path, OperatingSystem.IsWindows() ? "chrome.exe" : "chrome", SearchOption.AllDirectories).FirstOrDefault())
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)),
                BrowserFirefox => browserDirectories
                    .Select(path => Directory.EnumerateFiles(path, OperatingSystem.IsWindows() ? "firefox.exe" : "firefox", SearchOption.AllDirectories).FirstOrDefault())
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)),
                _ => browserDirectories.FirstOrDefault()
            };

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to detect Playwright browser artifacts for {Browser} in {Directory}", browser, directory);
            return false;
        }
    }

    private string? TryGetBrowserVersion(string? executablePath, string rootDirectory, string browser)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(executablePath);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                    return info.ProductVersion;
                if (!string.IsNullOrWhiteSpace(info.FileVersion))
                    return info.FileVersion;
            }
            catch
            {
            }
        }

        try
        {
            var prefix = browser + "-";
            var folder = Directory.EnumerateDirectories(rootDirectory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name) && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(folder) && folder.Length > prefix.Length)
                return folder[prefix.Length..];
        }
        catch
        {
        }

        return null;
    }

    private void DeleteBrowserArtifacts(string directory, string browser)
    {
        var targets = new List<string>();
        if (string.Equals(browser, BrowserChromium, StringComparison.OrdinalIgnoreCase))
        {
            var offlineDirectory = Path.Combine(directory, "chrome-win64");
            if (Directory.Exists(offlineDirectory))
                targets.Add(offlineDirectory);
        }

        try
        {
            targets.AddRange(Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(browser + "-", StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enumerate browser artifact directories for {Browser} in {Directory}", browser, directory);
        }

        foreach (var target in targets.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(target))
                    Directory.Delete(target, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete browser artifact directory {BrowserArtifactDirectory}", target);
            }
        }
    }

    private string ResolveManagedRuntimeSource()
    {
        try
        {
            if (!File.Exists(_managedRuntimeMarkerPath))
                return RuntimeSourceManaged;

            var marker = File.ReadAllText(_managedRuntimeMarkerPath).Trim();
            return string.Equals(marker, RuntimeSourceManagedOffline, StringComparison.OrdinalIgnoreCase)
                ? RuntimeSourceManagedOffline
                : RuntimeSourceManaged;
        }
        catch
        {
            return RuntimeSourceManaged;
        }
    }

    private void WriteManagedRuntimeMarker(string marker)
    {
        var markerDirectory = Path.GetDirectoryName(_managedRuntimeMarkerPath);
        if (!string.IsNullOrWhiteSpace(markerDirectory) && !Directory.Exists(markerDirectory))
            Directory.CreateDirectory(markerDirectory);

        File.WriteAllText(_managedRuntimeMarkerPath, marker);
    }

    private async Task<(int ExitCode, string Output)> RunPlaywrightCliAsync(string browserInstallDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var previousDriverSearchPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH");
        var previousBrowsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        var previousCurrentDirectory = Environment.CurrentDirectory;
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var writer = new StringWriter();

        try
        {
            var installerPath = ResolveInstallerPath();
            if (string.IsNullOrWhiteSpace(installerPath))
                throw new InvalidOperationException("未找到可用的 Playwright 安装器入口。请确认 Microsoft.Playwright 已复制到运行目录。");

            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", installerPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browserInstallDirectory);
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            Console.SetOut(TextWriter.Synchronized(writer));
            Console.SetError(TextWriter.Synchronized(writer));

            var exitCode = await Task.Run(() => Program.Main(arguments.ToArray()), CancellationToken.None);
            var output = writer.ToString().Trim();

            if (exitCode == 0)
                logger.LogInformation("Playwright installer completed successfully. {Output}", output);
            else
                logger.LogWarning("Playwright installer failed with exit code {ExitCode}. {Output}", exitCode, output);

            cancellationToken.ThrowIfCancellationRequested();
            return (exitCode, output);
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", previousDriverSearchPath);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", previousBrowsersPath);
            Environment.CurrentDirectory = previousCurrentDirectory;
        }
    }

    private void DeleteDirectoryIfExists(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, recursive: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete Playwright browser directory {BrowserInstallDirectory}", directoryPath);
        }
    }
}