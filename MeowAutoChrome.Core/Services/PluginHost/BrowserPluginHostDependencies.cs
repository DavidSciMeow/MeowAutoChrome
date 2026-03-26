using MeowAutoChrome.Core.Services.PluginDiscovery;

namespace MeowAutoChrome.Core.Services.PluginHost;

public sealed class BrowserPluginHostDependencies
{
    public MeowAutoChrome.Core.Interface.ICoreBrowserInstanceManager BrowserInstances { get; init; } = null!;
    public IPluginDiscoveryService Discovery { get; init; } = null!;
    public MeowAutoChrome.Core.Interface.IPluginOutputPublisher Publisher { get; init; } = null!;
    public IPluginInstanceManager InstanceManager { get; init; } = null!;
    public MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader AssemblyLoader { get; init; } = null!;
    public IPluginExecutor Executor { get; init; } = null!;
    public PluginExecutionService ExecutionService { get; init; } = null!;
    public PluginPublishingService PublishingService { get; init; } = null!;
}
