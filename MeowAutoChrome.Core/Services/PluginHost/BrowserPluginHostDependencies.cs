using MeowAutoChrome.Core.Services.PluginDiscovery;

namespace MeowAutoChrome.Core.Services.PluginHost;

public sealed class BrowserPluginHostDependencies
{
    public Interface.ICoreBrowserInstanceManager BrowserInstances { get; init; } = null!;
    public IPluginDiscoveryService Discovery { get; init; } = null!;
    public Interface.IPluginOutputPublisher Publisher { get; init; } = null!;
    public IPluginInstanceManager InstanceManager { get; init; } = null!;
    public Interface.ICorePluginAssemblyLoader AssemblyLoader { get; init; } = null!;
    public IPluginExecutor Executor { get; init; } = null!;
    public PluginExecutionService ExecutionService { get; init; } = null!;
    public PluginPublishingService PublishingService { get; init; } = null!;
    public Interface.IProgramSettingsProvider SettingsProvider { get; init; } = null!;
}
