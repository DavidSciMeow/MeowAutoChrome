using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Services;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core;

var appLogService = new AppLogService();
var originalConsoleOut = Console.Out;
var originalConsoleError = Console.Error;

Console.SetOut(new MeowAutoChrome.Core.Services.ConsoleLogTextWriter(originalConsoleOut, appLogService, LogLevel.Information, "Console.Out")); 
Console.SetError(new MeowAutoChrome.Core.Services.ConsoleLogTextWriter(originalConsoleError, appLogService, LogLevel.Error, "Console.Error")); 

AppDomain.CurrentDomain.UnhandledException += (_, args) => appLogService.WriteEntry(LogLevel.Critical, args.ExceptionObject?.ToString() ?? "Unknown unhandled exception.", "UnhandledException");
TaskScheduler.UnobservedTaskException += (_, args) => appLogService.WriteEntry(LogLevel.Error, args.Exception.ToString(), "UnobservedTaskException");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new AppLogLoggerProvider(appLogService));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton(appLogService);
builder.Services.AddSingleton<BrowserInstanceManagerCore>();
builder.Services.AddSingleton(sp => new BrowserInstanceManager(sp.GetRequiredService<BrowserInstanceManagerCore>(), sp.GetRequiredService<IProgramSettingsProvider>(), sp.GetRequiredService<ILogger<BrowserInstanceManager>>() ));
builder.Services.AddSingleton(sp => sp.GetRequiredService<BrowserInstanceManager>() as IBrowserInstanceManager ?? throw new InvalidOperationException("Failed to resolve IBrowserInstanceManager"));
builder.Services.AddSingleton<IProgramSettingsProvider, FileProgramSettingsProvider>();
builder.Services.AddSingleton<AppLogService>();
builder.Services.AddSingleton<PluginDiscoveryService>();
builder.Services.AddSingleton<IPluginOutputPublisher>(sp => new SignalRPluginOutputPublisher(sp.GetRequiredService<IHubContext<BrowserHub>>())); 
builder.Services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>();
builder.Services.AddSingleton<BrowserPluginHost>();
builder.Services.AddSingleton<ScreencastService>();
builder.Services.AddSingleton<ScreenshotService>();
builder.Services.AddSingleton<IScreencastFrameSink>(sp => new SignalRScreencastFrameSink(sp.GetRequiredService<IHubContext<BrowserHub>>()));
builder.Services.AddSingleton<ScreencastServiceCore>();
builder.Services.AddSingleton<ResourceMetricsService>();
builder.Services.AddHostedService<ChromeShellService>(); // auto pull up chrome (Web-hosted implementation)


var app = builder.Build();
app.Lifetime.ApplicationStarted.Register(() => appLogService.WriteEntry(LogLevel.Information, "Application started.", "System"));
app.Lifetime.ApplicationStopping.Register(() => appLogService.WriteEntry(LogLevel.Information, "Application stopping.", "System"));

app.UseMiddleware<MeowAutoChrome.Web.Middleware.ProblemDetailsExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHub<BrowserHub>("/browserHub");

app.MapControllerRoute("default", "{controller=Browser}/{action=Index}/{id?}").WithStaticAssets();

try
{
    app.Services.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>().EnsurePluginDirectoryExists();
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Error, ex.ToString(), "Startup");
}

app.Run();


