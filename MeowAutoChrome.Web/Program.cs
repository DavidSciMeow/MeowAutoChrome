using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Warpper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSingleton<PlayWrightWarpper>();
builder.Services.AddSingleton<ScreencastService>();
builder.Services.AddSingleton<BrowserPluginHost>();
builder.Services.AddSingleton<ResourceMetricsService>();
builder.Services.AddSingleton<ProgramSettingsService>();

var app = builder.Build();

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


