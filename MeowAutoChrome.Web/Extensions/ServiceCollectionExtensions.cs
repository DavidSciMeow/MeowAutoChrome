using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.Core.Services;
using MeowAutoChrome.Contracts.SignalR;
using MeowAutoChrome.Core;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Web.Abstractions;
using MeowAutoChrome.Web.Hubs;

namespace MeowAutoChrome.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMeowAutoChromeServices(this IServiceCollection services)
    {
        // Ensure SignalR services are registered so IHubContext<T> can be resolved by DI
        services.AddSignalR();

        services.AddSingleton<AppLogService>();
        services.AddSingleton<BrowserInstanceManagerCore>();
        services.AddSingleton<BrowserInstanceManager>(sp => new BrowserInstanceManager(sp.GetRequiredService<BrowserInstanceManagerCore>(), sp.GetRequiredService<IProgramSettingsProvider>(), sp.GetRequiredService<ILogger<BrowserInstanceManager>>() ));
        // Note: Web provides BrowserInstanceManager directly; no longer registering obsolete Contracts interface.
        // Keep registration for BrowserInstanceManager so other services can consume it by concrete type.
        services.AddSingleton<IProgramSettingsProvider, FileProgramSettingsProvider>();
        services.AddSingleton<IPluginDiscoveryService, PluginDiscoveryService>();
        services.AddSingleton<IPluginOutputPublisher>(sp => new SignalRPluginOutputPublisher(sp.GetRequiredService<IHubContext<BrowserHub>>())); 

        // Plugin host dependencies
        services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader, MeowAutoChrome.Core.Services.PluginHost.PluginAssemblyLoader>();
        services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.IPluginInstanceManager, MeowAutoChrome.Core.Services.PluginHost.PluginInstanceManager>();
        services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.IPluginExecutor, MeowAutoChrome.Core.Services.PluginHost.PluginExecutor>();
        services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.PluginExecutionService>();
        services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.PluginPublishingService>(sp => new MeowAutoChrome.Core.Services.PluginHost.PluginPublishingService(sp.GetRequiredService<MeowAutoChrome.Core.Interface.IPluginOutputPublisher>()));
        services.AddSingleton<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>(sp =>
            new MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore(new MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostDependencies
            {
                BrowserInstances = sp.GetRequiredService<BrowserInstanceManagerCore>(),
                Discovery = sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginDiscovery.IPluginDiscoveryService>(),
                Publisher = sp.GetRequiredService<MeowAutoChrome.Core.Interface.IPluginOutputPublisher>(),
                InstanceManager = sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.IPluginInstanceManager>(),
                AssemblyLoader = sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.IPluginAssemblyLoader>(),
                Executor = sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.IPluginExecutor>(),
                ExecutionService = sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.PluginExecutionService>(),
                PublishingService = sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.PluginPublishingService>(),
                SettingsProvider = sp.GetRequiredService<IProgramSettingsProvider>()
            }, sp.GetRequiredService<ILogger<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>>()));
        services.AddSingleton<MeowAutoChrome.Core.Interface.IPluginHostCore>(sp => sp.GetRequiredService<MeowAutoChrome.Core.Services.PluginHost.BrowserPluginHostCore>());

        services.AddSingleton<IScreencastService, ScreencastService>();
        services.AddSingleton<ScreencastService>();
        services.AddSingleton<ScreenshotService>();
        services.AddSingleton<IScreencastFrameSink>(sp => new SignalRScreencastFrameSink(sp.GetRequiredService<IHubContext<BrowserHub, IBrowserClient>>()));
        services.AddSingleton<ScreencastServiceCore>();
        services.AddSingleton<ResourceMetricsService>();
        services.AddHostedService<ChromeShellService>();

        return services;
    }
}
