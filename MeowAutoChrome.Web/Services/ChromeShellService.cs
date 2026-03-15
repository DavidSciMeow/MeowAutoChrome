using System.Diagnostics;
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
    AppLogService appLogService) : IHostedService, IDisposable
{
    private readonly object _syncRoot = new();
    private Process? _chromeProcess;
    private bool _applicationStopping;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsDevelopment() || !OperatingSystem.IsWindows())
            return Task.CompletedTask;

        hostApplicationLifetime.ApplicationStarted.Register(OnApplicationStarted);
        hostApplicationLifetime.ApplicationStopping.Register(OnApplicationStopping);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        OnApplicationStopping();
        return Task.CompletedTask;
    }

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

        var chromeExecutablePath = FindChromeExecutablePath();
        if (chromeExecutablePath == null)
        {
            appLogService.WriteEntry(LogLevel.Warning, "Chrome executable was not found. Skipping browser auto-start.", nameof(ChromeShellService));
            return;
        }

        try
        {
            var profileDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeowAutoChrome", "ChromeShellProfile");
            Directory.CreateDirectory(profileDirectoryPath);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = chromeExecutablePath,
                Arguments = $"--new-window --user-data-dir=\"{profileDirectoryPath}\" \"{applicationUrl}\"",
                UseShellExecute = false
            });

            if (process == null)
            {
                appLogService.WriteEntry(LogLevel.Warning, "Chrome process did not start. Skipping browser auto-start.", nameof(ChromeShellService));
                return;
            }

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                if (_applicationStopping)
                    return;

                appLogService.WriteEntry(LogLevel.Information, "Chrome shell exited. Stopping application.", nameof(ChromeShellService));
                hostApplicationLifetime.StopApplication();
            };

            lock (_syncRoot)
            {
                _chromeProcess?.Dispose();
                _chromeProcess = process;
            }

            appLogService.WriteEntry(LogLevel.Information, $"Chrome shell started at {applicationUrl}.", nameof(ChromeShellService));
        }
        catch (Exception ex)
        {
            appLogService.WriteEntry(LogLevel.Error, $"Failed to start Chrome shell: {ex}", nameof(ChromeShellService));
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
