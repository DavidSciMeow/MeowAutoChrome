using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// BrowserPluginHost 的依赖注入汇总类型，用于构造 <see cref="BrowserPluginHostCore"/>。<br/>
/// Aggregates dependencies required by the BrowserPluginHost, used to construct <see cref="BrowserPluginHostCore"/>.
/// </summary>
public sealed class BrowserPluginHostDependencies
{
    /// <summary>
    /// 浏览器实例管理器 / browser instance manager.
    /// </summary>
    public Interface.ICoreBrowserInstanceManager BrowserInstances { get; init; } = null!;

    /// <summary>
    /// 插件发现服务 / plugin discovery service.
    /// </summary>
    public IPluginDiscoveryService Discovery { get; init; } = null!;

    /// <summary>
    /// 插件输出发布器 / plugin output publisher.
    /// </summary>
    public Interface.IPluginOutputPublisher Publisher { get; init; } = null!;

    /// <summary>
    /// 插件实例管理器 / plugin instance manager.
    /// </summary>
    public IPluginInstanceManager InstanceManager { get; init; } = null!;

    /// <summary>
    /// 核心层的程序集装载器接口 / core layer assembly loader interface.
    /// </summary>
    public IPluginAssemblyLoader AssemblyLoader { get; init; } = null!;

    /// <summary>
    /// 插件执行器 / plugin executor.
    /// </summary>
    public IPluginExecutor Executor { get; init; } = null!;

    /// <summary>
    /// 插件执行服务 / plugin execution service.
    /// </summary>
    public PluginExecutionService ExecutionService { get; init; } = null!;

    /// <summary>
    /// 插件发布服务 / plugin publishing service.
    /// </summary>
    public PluginPublishingService PublishingService { get; init; } = null!;

    /// <summary>
    /// 可选的程序设置提供者 / optional program settings provider.
    /// </summary>
    public IProgramSettingsProvider SettingsProvider { get; init; } = null!;

    /// <summary>
    /// 应用日志服务，供插件宿主将插件日志写入应用级日志。/ App log service used by plugin host to write plugin logs.
    /// </summary>
    public AppLogService AppLogService { get; init; } = null!;

    /// <summary>
    /// 宿主基础地址提供者。/ Host base-address provider.
    /// </summary>
    public IHostAddressProvider? HostAddressProvider { get; init; }
}
