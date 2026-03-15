using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;

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

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton(appLogService);
builder.Services.AddSingleton<BrowserInstanceManager>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<BrowserInstanceManager>().PrimaryInstance);
builder.Services.AddSingleton<ScreencastService>();
builder.Services.AddSingleton<BrowserPluginHost>();
builder.Services.AddSingleton<ResourceMetricsService>();
builder.Services.AddSingleton<ProgramSettingsService>();
builder.Services.AddHostedService<ChromeShellService>(); //auto pull up chrome

var app = builder.Build();
app.Lifetime.ApplicationStarted.Register(() => appLogService.WriteEntry(LogLevel.Information, "Application started.", "System"));
app.Lifetime.ApplicationStopping.Register(() => appLogService.WriteEntry(LogLevel.Information, "Application stopping.", "System"));

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHub<BrowserHub>("/browserHub");

app.MapControllerRoute("default", "{controller=Browser}/{action=Index}/{id?}").WithStaticAssets();

app.Services.GetRequiredService<BrowserPluginHost>().EnsurePluginDirectoryExists();
app.Services.GetRequiredService<PlayWrightWarpper>();

app.Run();


