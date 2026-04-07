using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.WebAPI.Hubs;
using MeowAutoChrome.WebAPI.Extensions;
using MeowAutoChrome.WebAPI.Services;

var appLogService = new AppLogService();

Console.SetOut(new ConsoleLogTextWriter(Console.Out, appLogService, LogLevel.Debug, "Console.Out"));
Console.SetError(new ConsoleLogTextWriter(Console.Error, appLogService, LogLevel.Error, "Console.Error"));

AppDomain.CurrentDomain.UnhandledException += (_, args) => appLogService.WriteEntry(LogLevel.Critical, args.ExceptionObject?.ToString() ?? "Unknown unhandled exception.", "UnhandledException");
TaskScheduler.UnobservedTaskException += (_, args) => appLogService.WriteEntry(LogLevel.Error, args.Exception.ToString(), "UnobservedTaskException");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new AppLogLoggerProvider(appLogService));
builder.Services.AddSingleton(appLogService);
builder.Services.AddMeowAutoChromeServices();
builder.Services.AddControllers();
builder.Services.AddHostedService<LogTailHostedService>();

var app = builder.Build();
app.Lifetime.ApplicationStarted.Register(() => appLogService.WriteEntry(LogLevel.Debug, "WebAPI started.", "System"));
app.Lifetime.ApplicationStopping.Register(() => appLogService.WriteEntry(LogLevel.Debug, "WebAPI stopping.", "System"));
app.Lifetime.ApplicationStopping.Register(() => app.Services.GetService<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>()?.DisposeAsync().AsTask().GetAwaiter().GetResult());

app.UseRouting();
app.UseAuthorization();
app.UseStaticFiles();

app.MapHub<BrowserHub>("/browserHub");
app.MapHub<LogHub>("/logHub");
app.MapControllers();

// Diagnostic endpoint to list registered endpoints (temporary, safe for dev).
app.MapGet("/__endpoints", (HttpContext ctx) =>
{
    try
    {
        var ds = ctx.RequestServices.GetService<EndpointDataSource>();
        if (ds == null) return Results.NotFound(new { error = "EndpointDataSource not available" });
        var list = ds.Endpoints.Select(e => new
        {
            displayName = e.DisplayName,
            endpointType = e.GetType().FullName,
            metadata = e.Metadata?.Select(m => m.GetType().FullName).ToArray()
        }).ToArray();
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

try
{
    app.Services.GetRequiredService<IPluginHostCore>().EnsurePluginDirectoryExists();
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Error, ex.ToString(), "Startup");
}

try
{
    var settingsProvider = app.Services.GetRequiredService<IProgramSettingsProvider>();
    var settings = settingsProvider.GetAsync().GetAwaiter().GetResult();
    var currentPluginDir = settings.PluginDirectory ?? string.Empty;
    var appDataBase = MeowAutoChrome.Core.Struct.ProgramSettings.GetAppDataDirectoryPath();
    var appDataBaseFull = Path.GetFullPath(appDataBase).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    var currentFull = string.Empty;
    try { currentFull = Path.GetFullPath(currentPluginDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; } catch { currentFull = currentPluginDir; }
    if (!string.IsNullOrWhiteSpace(currentFull) && !currentFull.StartsWith(appDataBaseFull, StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var pluginHost = app.Services.GetRequiredService<IPluginHostCore>();
            var defaultPluginDir = MeowAutoChrome.Core.Struct.ProgramSettings.GetDefaultPluginDirectoryPath();
            pluginHost.UpdatePluginRootPathAsync(defaultPluginDir).GetAwaiter().GetResult();
            settings.PluginDirectory = defaultPluginDir;
            settingsProvider.SaveAsync(settings).GetAwaiter().GetResult();
            appLogService.WriteEntry(LogLevel.Debug, $"PluginDirectory migrated to default AppData path: {defaultPluginDir}", "Startup.PluginDir");
        }
        catch (Exception ex)
        {
            appLogService.WriteEntry(LogLevel.Warning, ex.ToString(), "Startup.PluginDir.Migrate");
        }
    }
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Warning, ex.ToString(), "Startup.PluginDir.Check");
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
            appLogService.WriteEntry(LogLevel.Debug, "Electron host: forced headless mode and disabled Chrome shell.", "Startup");
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
        appLogService.WriteEntry(LogLevel.Debug, "Injected Web custom settings into Core.", "Startup");
    }
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Warning, ex.ToString(), "Startup.InjectCustomSettings");
}

app.MapGet("/health", () => Results.Ok("ok"));

// After application started, record the resolved endpoints into the app log for diagnostics.
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var ds = app.Services.GetService<EndpointDataSource>();
        if (ds != null)
        {
            foreach (var ep in ds.Endpoints)
            {
                try
                {
                    appLogService.WriteEntry(LogLevel.Debug, $"Endpoint registered: {ep.DisplayName ?? ep.ToString()} ({ep.GetType().Name})", "Startup.Endpoints");
                }
                catch { }
            }
        }
    }
    catch { }
});

app.Run();
