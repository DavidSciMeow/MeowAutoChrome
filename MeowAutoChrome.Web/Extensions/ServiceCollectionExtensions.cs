using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Contracts.SignalR;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Hubs;

namespace MeowAutoChrome.Web.Extensions;

/// <summary>
/// 注册 MeowAutoChrome 所需的依赖服务（例如插件宿主、Screencast、SignalR 等）。<br/>
/// Registers MeowAutoChrome required services (plugin host, screencast, SignalR, etc.).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 将 MeowAutoChrome 的服务添加到 DI 容器并返回服务集合。<br/>
    /// Adds MeowAutoChrome services to the DI container and returns the service collection.
    /// </summary>
    /// <param name="services">目标服务集合 / target service collection.</param>
    /// <returns>更新后的 IServiceCollection / updated IServiceCollection.</returns>
    public static IServiceCollection AddMeowAutoChromeServices(this IServiceCollection services)
    {
        // Ensure SignalR services are registered so IHubContext<T> can be resolved by DI
        services.AddSignalR();

        services.AddSingleton<AppLogService>();
        services.AddSingleton<BrowserInstanceManagerCore>();
        services.AddSingleton(sp => new BrowserInstanceManager(sp.GetRequiredService<BrowserInstanceManagerCore>(), sp.GetRequiredService<IProgramSettingsProvider>(), sp.GetRequiredService<ILogger<BrowserInstanceManager>>()));
        // Note: Web provides BrowserInstanceManager directly; no longer registering obsolete Contracts interface.
        // Keep registration for BrowserInstanceManager so other services can consume it by concrete type.
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

        // Register core screencast and the SignalR frame sink implementation
        services.AddSingleton<IScreencastFrameSink>(sp => new SignalRScreencastFrameSink(sp.GetRequiredService<IHubContext<BrowserHub, IBrowserClient>>()));
        services.AddSingleton<ScreencastServiceCore>();
        services.AddSingleton<ScreenshotService>();
        services.AddSingleton<ResourceMetricsService>();
        services.AddHostedService<ChromeShellService>();
        // Web-layer settings helper used by HomeController and Settings UI
        services.AddSingleton<SettingsService>();

        return services;
    }
}
