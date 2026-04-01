using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Contracts.SignalR;
using MeowAutoChrome.Core;
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

        services.AddSingleton<AppLogService>();
        services.AddSingleton<BrowserInstanceManagerCore>();
        services.AddSingleton(sp => new BrowserInstanceManager(sp.GetRequiredService<BrowserInstanceManagerCore>(), sp.GetRequiredService<IProgramSettingsProvider>(), sp.GetRequiredService<ILogger<BrowserInstanceManager>>()));
        services.AddSingleton<IProgramSettingsProvider, FileProgramSettingsProvider>();
        services.AddSingleton<IPluginDiscoveryService, PluginDiscoveryService>();
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
                SettingsProvider = sp.GetRequiredService<IProgramSettingsProvider>()
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
