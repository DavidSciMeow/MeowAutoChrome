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
    private const string RuntimeSourceBundled = "bundled";
    private const string RuntimeSourceManaged = "managed";
    private const string RuntimeSourceManagedOffline = "managed-offline";
    private const string RuntimeSourceGlobal = "global";
    private const string RuntimeSourceMissing = "missing";
    private const string InstallModeAuto = "auto";
    private const string InstallModeOnline = "online";
    private const string InstallModeOffline = "offline";
    private const string OfflineArchiveFileName = "chrome-win64.zip";
    private readonly string _managedRuntimeMarkerPath = Path.Combine(ProgramSettings.GetPlaywrightBrowserDirectoryPath(), ".meow-install-source");

    public PlaywrightRuntimeStatus GetStatus()
    {
        var (activeBrowserInstallDirectory, runtimeSource) = ResolveActiveBrowserInstallDirectory();
        ConfigureEnvironment(activeBrowserInstallDirectory);

        var installerPath = ResolveInstallerPath();
        var offlinePackagePath = ResolveOfflinePackagePath();
        var offlinePackageAvailable = !string.IsNullOrWhiteSpace(offlinePackagePath);
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
                : offlinePackageAvailable
                    ? "Playwright Chromium 尚未安装。安装时会优先使用随包提供的离线压缩包；如需联网下载，可手动切换到在线安装。"
                    : "Playwright Chromium 尚未安装，使用前需要先完成安装。";

        return new PlaywrightRuntimeStatus(
            isInstalled,
            !isInstalled,
            activeBrowserInstallDirectory,
            browserExecutablePath,
            installerPath,
            runtimeSource,
            canUninstallFromApp,
            offlinePackageAvailable,
            offlinePackagePath,
            message,
            null);
    }

    public async Task<PlaywrightRuntimeStatus> InstallAsync(string mode = InstallModeAuto, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var current = GetStatus();
            if (current.IsInstalled)
                return current with { Message = "Playwright Chromium 已安装，无需重复安装。" };

            var normalizedMode = NormalizeInstallMode(mode, current.OfflinePackageAvailable);
            ConfigureEnvironment(_managedBrowserInstallDirectory);

            if (string.Equals(normalizedMode, InstallModeOffline, StringComparison.Ordinal))
            {
                return await InstallFromOfflineArchiveAsync(cancellationToken);
            }

            var installerPath = current.ScriptPath;
            if (string.IsNullOrWhiteSpace(installerPath))
                throw new InvalidOperationException("未找到可用的 Playwright 安装器入口。请确认发布输出包含 Microsoft.Playwright 运行时文件。");

            logger.LogInformation("Installing Playwright Chromium into {BrowserInstallDirectory}", _managedBrowserInstallDirectory);

            var result = await RunPlaywrightCliAsync(_managedBrowserInstallDirectory, ["install", "chromium"], cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Playwright 安装失败。{result.Output}".Trim());
            }

            WriteManagedRuntimeMarker(RuntimeSourceManaged);

            var updated = GetStatus();
            if (!updated.IsInstalled)
                throw new InvalidOperationException("Playwright 安装命令执行完成，但未检测到 Chromium 可执行文件。");

            logger.LogInformation("Playwright Chromium installed at {BrowserExecutablePath}", updated.BrowserExecutablePath);
            return updated with { Message = "Playwright Chromium 安装完成。", OperationOutput = result.Output };
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
        var candidates = new[]
        {
            (Directory: _bundledBrowserInstallDirectory, RuntimeSource: RuntimeSourceBundled),
            (Directory: _managedBrowserInstallDirectory, RuntimeSource: ResolveManagedRuntimeSource()),
            (Directory: _globalBrowserInstallDirectory, RuntimeSource: RuntimeSourceGlobal)
        };

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

    private string NormalizeInstallMode(string? mode, bool offlinePackageAvailable)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? InstallModeAuto : mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            InstallModeAuto => offlinePackageAvailable ? InstallModeOffline : InstallModeOnline,
            InstallModeOnline => InstallModeOnline,
            InstallModeOffline => InstallModeOffline,
            _ => throw new InvalidOperationException($"Unsupported Playwright install mode '{mode}'.")
        };
    }

    private async Task<PlaywrightRuntimeStatus> InstallFromOfflineArchiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var offlinePackagePath = ResolveOfflinePackagePath();
        if (string.IsNullOrWhiteSpace(offlinePackagePath) || !File.Exists(offlinePackagePath))
            throw new InvalidOperationException("未找到可用的离线浏览器压缩包 chrome-win64.zip。请使用离线安装包，或切换到在线安装模式。");

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
            Message = "离线 Chromium 安装完成。",
            OperationOutput = $"Extracted offline archive: {offlinePackagePath}"
        };
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

    private string? ResolveOfflinePackagePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, OfflineArchiveFileName),
            Path.Combine(AppContext.BaseDirectory, "playwright-offline", OfflineArchiveFileName)
        };

        return candidates.FirstOrDefault(File.Exists);
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