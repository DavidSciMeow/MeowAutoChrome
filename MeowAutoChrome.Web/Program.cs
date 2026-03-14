using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;
using Microsoft.Extensions.Logging;

var appLogService = new AppLogService();
var originalConsoleOut = Console.Out;
var originalConsoleError = Console.Error;
Console.SetOut(new ConsoleLogTextWriter(originalConsoleOut, appLogService, LogLevel.Information, "Console.Out"));
Console.SetError(new ConsoleLogTextWriter(originalConsoleError, appLogService, LogLevel.Error, "Console.Error"));

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
    appLogService.WriteEntry(LogLevel.Critical, args.ExceptionObject?.ToString() ?? "Unknown unhandled exception.", "UnhandledException");

TaskScheduler.UnobservedTaskException += (_, args) =>
    appLogService.WriteEntry(LogLevel.Error, args.Exception.ToString(), "UnobservedTaskException");

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new AppLogLoggerProvider(appLogService));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton(appLogService);
builder.Services.AddSingleton<PlayWrightWarpper>();
builder.Services.AddSingleton<ScreencastService>();
builder.Services.AddSingleton<BrowserPluginHost>();
builder.Services.AddSingleton<ResourceMetricsService>();
builder.Services.AddSingleton<ProgramSettingsService>();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
    appLogService.WriteEntry(LogLevel.Information, "Application started.", "System"));

app.Lifetime.ApplicationStopping.Register(() =>
    appLogService.WriteEntry(LogLevel.Information, "Application stopping.", "System"));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();

app.MapHub<BrowserHub>("/browserHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Browser}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Services.GetRequiredService<BrowserPluginHost>().EnsurePluginDirectoryExists();
app.Services.GetRequiredService<PlayWrightWarpper>();

app.Run();


