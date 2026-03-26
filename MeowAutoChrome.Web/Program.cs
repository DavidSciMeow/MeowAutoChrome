using MeowAutoChrome.Web;
using MeowAutoChrome.Web.Extensions;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;

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

builder.Services.AddMeowAutoChromeServices();


var app = builder.Build();
app.Lifetime.ApplicationStarted.Register(() => appLogService.WriteEntry(LogLevel.Information, "Application started.", "System"));
app.Lifetime.ApplicationStopping.Register(() => appLogService.WriteEntry(LogLevel.Information, "Application stopping.", "System"));
// ensure BrowserPluginHostCore stops background tasks on shutdown
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

app.UseMiddleware<MeowAutoChrome.Web.Middleware.ProblemDetailsExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHub<BrowserHub>("/browserHub");

app.MapControllerRoute("default", "{controller=Browser}/{action=Index}/{id?}").WithStaticAssets();

try
{
    app.Services.GetRequiredService<MeowAutoChrome.Core.Interface.IPluginHostCore>().EnsurePluginDirectoryExists();
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Error, ex.ToString(), "Startup");
}
// Inject Web-level custom settings into Core program settings provider at startup if configured
try
{
    var provider = app.Services.GetRequiredService<MeowAutoChrome.Core.Interface.IProgramSettingsProvider>();
    var configSection = builder.Configuration.GetSection("Web:CustomSettings");
    if (configSection.Exists())
    {
        var dict = configSection.Get<System.Collections.Generic.Dictionary<string, string?>>() ?? new();
        provider.InjectCustomSettingsAsync(dict).GetAwaiter().GetResult();
        appLogService.WriteEntry(LogLevel.Information, "Injected Web custom settings into Core.", "Startup");
    }
}
catch (Exception ex)
{
    appLogService.WriteEntry(LogLevel.Warning, ex.ToString(), "Startup.InjectCustomSettings");
}

app.Run();


