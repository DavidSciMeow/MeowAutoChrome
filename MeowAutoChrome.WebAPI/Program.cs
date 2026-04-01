using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.WebAPI.Hubs;
using MeowAutoChrome.WebAPI.Extensions;
using MeowAutoChrome.WebAPI.Services;
using Microsoft.Extensions.DependencyInjection;

var appLogService = new AppLogService();
var originalConsoleOut = Console.Out;
var originalConsoleError = Console.Error;

Console.SetOut(new ConsoleLogTextWriter(originalConsoleOut, appLogService, LogLevel.Information, "Console.Out"));
Console.SetError(new ConsoleLogTextWriter(originalConsoleError, appLogService, LogLevel.Error, "Console.Error"));

AppDomain.CurrentDomain.UnhandledException += (_, args) => appLogService.WriteEntry(LogLevel.Critical, args.ExceptionObject?.ToString() ?? "Unknown unhandled exception.", "UnhandledException");
TaskScheduler.UnobservedTaskException += (_, args) => appLogService.WriteEntry(LogLevel.Error, args.Exception.ToString(), "UnobservedTaskException");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new AppLogLoggerProvider(appLogService));

// 注册 WebAPI 所需的核心服务与 SignalR。 / Register the core services and SignalR infrastructure for WebAPI.
builder.Services.AddMeowAutoChromeServices();
// 仅注册 API 控制器；桌面前端由 Electron 承担。 / Register API controllers only; the desktop UI is hosted by Electron.
builder.Services.AddControllers();

var app = builder.Build();
app.Lifetime.ApplicationStarted.Register(() => appLogService.WriteEntry(LogLevel.Information, "WebAPI started.", "System"));
app.Lifetime.ApplicationStopping.Register(() => appLogService.WriteEntry(LogLevel.Information, "WebAPI stopping.", "System"));
// 在应用停止时释放插件宿主后台资源。 / Release plugin host background resources during shutdown.
app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        var host = app.Services.GetService<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>();
        if (host is not null)
        {
            host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
    catch { }
});

app.UseRouting();
app.UseAuthorization();
app.UseStaticFiles();

app.MapHub<BrowserHub>("/browserHub");
app.MapControllers();

try
{
    app.Services.GetRequiredService<IPluginHostCore>().EnsurePluginDirectoryExists();
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Error, ex.ToString(), "Startup");
}

// Electron 宿主模式下强制启用 Headless，避免弹出额外浏览器窗口。 / Force headless mode when hosted by Electron to avoid opening an external browser window.
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MEOW_ELECTRON")))
{
    try
    {
        var provider = app.Services.GetRequiredService<IProgramSettingsProvider>();
        var settings = provider.GetAsync().GetAwaiter().GetResult();
        var browserManager = app.Services.GetService<BrowserInstanceManagerCore>();
        if (browserManager != null)
        {
            browserManager.UpdateLaunchSettingsAsync(settings.UserDataDirectory, true, forceReload: true).GetAwaiter().GetResult();
            appLogService.WriteEntry(LogLevel.Information, "Electron host: forced headless mode and disabled Chrome shell.", "Startup");
        }
    }
    catch (Exception ex)
    {
        appLogService.WriteEntry(LogLevel.Warning, ex.ToString(), "Startup.ForceHeadless");
    }
}

// 启动时将配置中的自定义设置注入 Core。 / Inject configured custom settings into Core during startup.
try
{
    var provider = app.Services.GetRequiredService<IProgramSettingsProvider>();
    var configSection = builder.Configuration.GetSection("Web:CustomSettings");
    if (configSection.Exists())
    {
        var dict = configSection.Get<Dictionary<string, string?>>() ?? new();
        provider.InjectCustomSettingsAsync(dict).GetAwaiter().GetResult();
        appLogService.WriteEntry(LogLevel.Information, "Injected Web custom settings into Core.", "Startup");
    }
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Warning, ex.ToString(), "Startup.InjectCustomSettings");
}

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
