using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Contracts.SignalR;
using MeowAutoChrome.Core;
using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.WebAPI.Hubs;

namespace MeowAutoChrome.WebAPI.Extensions;

/// <summary>
/// 为 WebAPI 宿主注册 MeowAutoChrome 所需的核心服务、插件宿主和 SignalR 组件。<br/>
/// Register the core services, plugin host, and SignalR components required by the WebAPI host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 向依赖注入容器添加 WebAPI 宿主所需的全部服务。<br/>
    /// Add all services required by the WebAPI host to the dependency injection container.
    /// </summary>
    /// <param name="services">服务集合。<br/>Service collection.</param>
    /// <returns>可继续链式调用的服务集合。<br/>The service collection for further chaining.</returns>
    public static IServiceCollection AddMeowAutoChromeServices(this IServiceCollection services)
    {
        services.AddSignalR();

        // Ensure AppLogService is registered only once. If the caller already
        // registered an instance (e.g. Program created one for early logging),
        // avoid adding another transient instance here.
        try
        {
            if (!services.Any(sd => sd.ServiceType == typeof(AppLogService)))
            {
                services.AddSingleton<AppLogService>();
            }
        }
        catch
        {
            // In case LINQ/Any isn't available for some reason, fall back to adding.
            services.AddSingleton<AppLogService>();
        }
        services.AddSingleton<BrowserInstanceManagerCore>();
        services.AddSingleton(sp =>
        {
            var core = sp.GetRequiredService<BrowserInstanceManagerCore>();
            var mgr = new BrowserInstanceManager(core, sp.GetRequiredService<IProgramSettingsProvider>(), sp.GetRequiredService<ILogger<BrowserInstanceManager>>());
            var hub = sp.GetRequiredService<IHubContext<BrowserHub>>();
            try
            {
                // capture additional services needed to build full status
                var screencast = sp.GetRequiredService<ScreencastServiceCore>();
                var metrics = sp.GetRequiredService<ResourceMetricsService>();
                var settingsProv = sp.GetRequiredService<IProgramSettingsProvider>();

                core.TabClosed += (tabId) =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Build full status similar to StatusController.BuildStatusAsync
                            var snapshot = metrics.GetSnapshot();
                            core.TabOpened += (tabId) =>
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        // Build full status similar to StatusController.BuildStatusAsync
                                        var snapshot = metrics.GetSnapshot();
                                        var settings = await settingsProv.GetAsync();
                                        var hasInstance = core.GetInstances().Count > 0;
                                        var supportsScreencast = hasInstance && core.IsHeadless;
                                        var screencastEnabled = supportsScreencast && screencast.Enabled;

                                        var currentUrl = mgr.CurrentUrl;
                                        var title = await mgr.GetTitleAsync();
                                        var tabs = await mgr.GetTabsAsync();
                                        var viewport = await mgr.GetCurrentInstanceViewportSettingsAsync();

                                        var status = new BrowserStatusResponse(
                                            currentUrl,
                                            title,
                                            null,
                                            supportsScreencast,
                                            screencastEnabled,
                                            screencast.MaxWidth,
                                            screencast.MaxHeight,
                                            screencast.FrameIntervalMs,
                                            snapshot.CpuUsagePercent,
                                            snapshot.MemoryUsageMb,
                                            mgr.TotalPageCount,
                                            settings.PluginPanelWidth,
                                            tabs,
                                            mgr.CurrentInstanceId,
                                            viewport,
                                            mgr.IsHeadless);

                                        await hub.Clients.All.SendAsync("StatusUpdated", status);
                                    }
                                    catch { }
                                });
                            };
                            var settings = await settingsProv.GetAsync();
                            var hasInstance = core.GetInstances().Count > 0;
                            var supportsScreencast = hasInstance && core.IsHeadless;
                            var screencastEnabled = supportsScreencast && screencast.Enabled;

                            var currentUrl = mgr.CurrentUrl;
                            var title = await mgr.GetTitleAsync();
                            var tabs = await mgr.GetTabsAsync();
                            var viewport = await mgr.GetCurrentInstanceViewportSettingsAsync();

                            var status = new BrowserStatusResponse(
                                currentUrl,
                                title,
                                null,
                                supportsScreencast,
                                screencastEnabled,
                                screencast.MaxWidth,
                                screencast.MaxHeight,
                                screencast.FrameIntervalMs,
                                snapshot.CpuUsagePercent,
                                snapshot.MemoryUsageMb,
                                mgr.TotalPageCount,
                                settings.PluginPanelWidth,
                                tabs,
                                mgr.CurrentInstanceId,
                                viewport,
                                mgr.IsHeadless);

                            await hub.Clients.All.SendAsync("StatusUpdated", status);
                        }
                        catch { }
                    });
                };
            }
            catch { }

            return mgr;
        });
        services.AddSingleton<IProgramSettingsProvider, FileProgramSettingsProvider>();
        // Allow overriding plugin root(s) via environment variable MEOW_PLUGIN_ROOTS (semicolon- or pipe-separated).
        var envRoots = Environment.GetEnvironmentVariable("MEOW_PLUGIN_ROOTS");
        if (!string.IsNullOrWhiteSpace(envRoots))
        {
            services.AddSingleton<IPluginDiscoveryService>(sp => new PluginDiscoveryService(envRoots));
        }
        else
        {
            services.AddSingleton<IPluginDiscoveryService, PluginDiscoveryService>();
        }
        services.AddSingleton<IPluginOutputPublisher>(sp => new SignalRPluginOutputPublisher(sp.GetRequiredService<IHubContext<BrowserHub>>()));

        // Plugin host dependencies
        services.AddSingleton<Core.Services.PluginHost.IPluginAssemblyLoader, Core.Services.PluginHost.PluginAssemblyLoader>();
        services.AddSingleton<Core.Services.PluginHost.IPluginInstanceManager, Core.Services.PluginHost.PluginInstanceManager>();
        services.AddSingleton<Core.Services.PluginHost.IPluginExecutor, Core.Services.PluginHost.PluginExecutor>();
        services.AddSingleton<Core.Services.PluginHost.PluginExecutionService>();
        services.AddSingleton(sp => new Core.Services.PluginHost.PluginPublishingService(sp.GetRequiredService<IPluginOutputPublisher>()));
        services.AddSingleton(sp =>
            new Core.Services.PluginHost.BrowserPluginHostCore(new Core.Services.PluginHost.BrowserPluginHostDependencies
            {
                BrowserInstances = sp.GetRequiredService<BrowserInstanceManagerCore>(),
                Discovery = sp.GetRequiredService<IPluginDiscoveryService>(),
                Publisher = sp.GetRequiredService<IPluginOutputPublisher>(),
                InstanceManager = sp.GetRequiredService<Core.Services.PluginHost.IPluginInstanceManager>(),
                AssemblyLoader = sp.GetRequiredService<Core.Services.PluginHost.IPluginAssemblyLoader>(),
                Executor = sp.GetRequiredService<Core.Services.PluginHost.IPluginExecutor>(),
                ExecutionService = sp.GetRequiredService<Core.Services.PluginHost.PluginExecutionService>(),
                PublishingService = sp.GetRequiredService<Core.Services.PluginHost.PluginPublishingService>(),
                SettingsProvider = sp.GetRequiredService<IProgramSettingsProvider>(),
                AppLogService = sp.GetRequiredService<AppLogService>()
            }, sp.GetRequiredService<ILogger<Core.Services.PluginHost.BrowserPluginHostCore>>()));
        services.AddSingleton<IPluginHostCore>(sp => sp.GetRequiredService<Core.Services.PluginHost.BrowserPluginHostCore>());

        // Screencast + screenshot + metrics
        services.AddSingleton<IScreencastFrameSink>(sp => new SignalRScreencastFrameSink(sp.GetRequiredService<IHubContext<BrowserHub, IBrowserClient>>()));
        services.AddSingleton<ScreencastServiceCore>();
        services.AddSingleton<ScreenshotService>();
        services.AddSingleton<ResourceMetricsService>();

        // Web-layer settings helper used by some controllers
        services.AddSingleton<SettingsService>();

        return services;
    }
}
