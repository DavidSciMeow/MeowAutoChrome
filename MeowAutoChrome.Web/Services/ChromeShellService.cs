using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 在 Windows 平台上为应用启动一个外部 Chrome 窗口（仅在非开发环境下），用于将 Web UI 在桌面上以浏览器窗口打开。
/// 通过 IHostedService 生命周期钩子在启动时自动启动 Chrome（若找到可执行文件）并在应用停止时关闭。
/// </summary>
public sealed class ChromeShellService(
    IHostApplicationLifetime hostApplicationLifetime,
    IServer server,
    IWebHostEnvironment environment,
    Core.Services.AppLogService appLogService) : IHostedService, IDisposable
{
    private readonly Lock _syncRoot = new();
    private Process? _chromeProcess;
    private bool _applicationStopping;
    private int _restartAttempt;

    /// <summary>
    /// 用于在应用启动时自动打开 Chrome 浏览器窗口指向应用的 URL（仅在生产环境且 Windows 平台上）。在应用停止时会尝试关闭该浏览器窗口。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only skip on non-Windows platforms. Start Chrome in development as well so the shell
        // is pulled up by default on Windows.
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

        hostApplicationLifetime.ApplicationStarted.Register(OnApplicationStarted);
        hostApplicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
        return Task.CompletedTask;
    }
    /// <summary>
    /// 用于在应用停止时尝试关闭之前启动的 Chrome 浏览器窗口。会设置一个标志以避免在浏览器窗口被用户手动关闭时触发应用停止逻辑。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        OnApplicationStopping();
        return Task.CompletedTask;
    }
    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            _chromeProcess?.Dispose();
            _chromeProcess = null;
        }
    }

    private void OnApplicationStarted()
    {
        if (!TryGetApplicationUrl(out var applicationUrl))
        {
            appLogService.WriteEntry(LogLevel.Warning, "Unable to determine application URL for Chrome shell startup.", nameof(ChromeShellService));
            return;
        }
        // Ensure profile dir exists
        var profileDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoChrome", "ChromeShellProfile");
        try
        {
            Directory.CreateDirectory(profileDirectoryPath);
        }
        catch (Exception ex)
        {
            appLogService.WriteEntry(LogLevel.Error, $"Failed to create Chrome profile directory: {ex}", nameof(ChromeShellService));
            return;
        }

        // Start chrome and attach a restart-on-exit guard
        TryStartChromeWithGuard(applicationUrl, profileDirectoryPath);
    }

    private void TryStartChromeWithGuard(string applicationUrl, string profileDirectoryPath)
    {
        // Run start logic on threadpool to avoid blocking startup. The heavy work is moved to RunStartLoopAsync
        var stoppingToken = hostApplicationLifetime.ApplicationStopping;
        _ = Task.Run(() => RunStartLoopAsync(applicationUrl, profileDirectoryPath, stoppingToken), stoppingToken);
    }

    private async Task RunStartLoopAsync(string applicationUrl, string profileDirectoryPath, CancellationToken stoppingToken)
    {
        var attempt = 0;
        var maxAttempts = 5;
        var backoffMs = 1000;

        while (!stoppingToken.IsCancellationRequested && attempt < maxAttempts)
        {
            attempt++;
            try
            {
                var chromeExecutablePath = FindChromeExecutablePath();
                if (chromeExecutablePath == null)
                {
                    appLogService.WriteEntry(LogLevel.Warning, "Chrome executable was not found. Retry will be attempted.", nameof(ChromeShellService));
                    try { await Task.Delay(backoffMs, stoppingToken); } catch (OperationCanceledException) { break; }
                    backoffMs = Math.Min(10000, backoffMs * 2);
                    continue;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = chromeExecutablePath,
                    Arguments = $"--new-window --user-data-dir=\"{profileDirectoryPath}\" \"{applicationUrl}\"",
                    UseShellExecute = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    appLogService.WriteEntry(LogLevel.Warning, "Chrome process did not start. Retrying...", nameof(ChromeShellService));
                    try { await Task.Delay(backoffMs, stoppingToken); } catch (OperationCanceledException) { break; }
                    backoffMs = Math.Min(10000, backoffMs * 2);
                    continue;
                }

                // Successfully started
                lock (_syncRoot)
                {
                    _chromeProcess?.Dispose();
                    _chromeProcess = process;
                }

                appLogService.WriteEntry(LogLevel.Information, $"Chrome shell started at {applicationUrl}. ProcessId={process.Id}", nameof(ChromeShellService));

                AttachExitHandler(process);

                // Exit the start loop because process is running and handler will manage restarts
                return;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                appLogService.WriteEntry(LogLevel.Error, $"Failed to start Chrome shell: {ex}", nameof(ChromeShellService));
                try { await Task.Delay(backoffMs, stoppingToken); } catch (OperationCanceledException) { break; }
                backoffMs = Math.Min(10000, backoffMs * 2);
            }
        }

        appLogService.WriteEntry(LogLevel.Error, "Exceeded attempts to start Chrome shell or application stopping; giving up.", nameof(ChromeShellService));
    }

    private void AttachExitHandler(Process process)
    {
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                try
                {
                    if (_applicationStopping)
                        return;

                    var exitCode = -1;
                    try { exitCode = process.HasExited ? process.ExitCode : -1; } catch { }
                    appLogService.WriteEntry(LogLevel.Warning, $"Chrome shell exited. ProcessId={process.Id}; ExitCode={exitCode}", nameof(ChromeShellService));

                    // When the main Chrome shell launched by this program is closed, treat it as intent to stop the whole application.
                    try
                    {
                        hostApplicationLifetime.StopApplication();
                    }
                    catch (Exception ex)
                    {
                        try { appLogService.WriteEntry(LogLevel.Error, $"Failed to stop application after Chrome exit: {ex}", nameof(ChromeShellService)); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    try { appLogService.WriteEntry(LogLevel.Error, $"Error in Chrome shell exit handler: {ex}", nameof(ChromeShellService)); } catch { }
                }
            };
        }
        catch (Exception ex)
        {
            appLogService.WriteEntry(LogLevel.Warning, $"Failed to attach exit handler to Chrome process: {ex}", nameof(ChromeShellService));
        }
    }

    private void OnApplicationStopping()
    {
        _applicationStopping = true;

        lock (_syncRoot)
        {
            if (_chromeProcess == null)
                return;

            try
            {
                if (!_chromeProcess.HasExited)
                    _chromeProcess.CloseMainWindow();
            }
            catch
            {
            }
        }
    }

    private bool TryGetApplicationUrl(out string? applicationUrl)
    {
        applicationUrl = server.Features
            .Get<IServerAddressesFeature>()?
            .Addresses
            .Select(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri != null)
            .OrderBy(uri => uri!.Scheme == Uri.UriSchemeHttps ? 1 : 0)
            .Select(uri => uri!.AbsoluteUri)
            .FirstOrDefault();

        return applicationUrl != null;
    }

    private static string? FindChromeExecutablePath()
    {
        foreach (var path in GetChromeCandidatePaths())
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                return path;
        }

        return null;
    }

    private static IEnumerable<string> GetChromeCandidatePaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        yield return Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe");
        yield return Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe");

        var pathEnvironmentVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnvironmentVariable))
            yield break;

        foreach (var pathSegment in pathEnvironmentVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return Path.Combine(pathSegment, "chrome.exe");
    }
}
