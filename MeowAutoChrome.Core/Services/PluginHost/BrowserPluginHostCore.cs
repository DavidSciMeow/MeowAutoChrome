using System.Diagnostics.CodeAnalysis;
using MeowAutoChrome.Contracts.Abstractions;
using System.Linq;
using MeowAutoChrome.Contracts;
using MeowAutoChrome.Core.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;
using CoreModels = MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MeowAutoChrome.Contracts.Attributes;
// Restored original Interface namespace to maintain API compatibility

namespace MeowAutoChrome.Core.Services.PluginHost;
/// <summary>
/// Core implementation of plugin host logic moved from Web.
/// This class does not depend on ASP.NET types and can run in CLI environments.
/// It exposes discovery, execution and publishing via IPluginOutputPublisher.
/// </summary>
[SuppressMessage("Maintainability", "Avoid types too big", Justification = "Planned refactor; suppressed temporarily to complete migration.")]
public sealed class BrowserPluginHostCore : MeowAutoChrome.Core.Interface.IPluginHostCore
{
    private readonly MeowAutoChrome.Core.Interface.ICoreBrowserInstanceManager _browserInstances;
    private readonly MeowAutoChrome.Core.Services.PluginDiscovery.IPluginDiscoveryService _discovery;
    private readonly IPluginOutputPublisher _publisher;
    private readonly ILogger<BrowserPluginHostCore> _logger;

    // runtime caches
    private readonly IPluginInstanceManager _instanceManager;
    private readonly PluginExecutionService _executionService;
    private readonly PluginPublishingService _publishingService;
    // assembly loader/executor injected via DI to allow testing and replacement
        private readonly MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader _assemblyLoader;
    private readonly IPluginExecutor _executor;
    private readonly BrowserPluginDiscovery _pluginDiscovery;
    // cached discovery snapshot maintained by background scanner
    private CoreModels.PluginDiscoverySnapshot _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(Array.Empty<CoreModels.RuntimeBrowserPlugin>(), Array.Empty<string>(), Array.Empty<CoreModels.BrowserPluginErrorDescriptor>());
    private readonly object _snapshotLock = new();
    private readonly CancellationTokenSource _scanCts = new();
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(30);
    private Task? _scanTask;

    public BrowserPluginHostCore(BrowserPluginHostDependencies deps, ILogger<BrowserPluginHostCore> logger)
    {
        _browserInstances = deps.BrowserInstances;
        _discovery = deps.Discovery;
        _publisher = deps.Publisher;
        _logger = logger;
        _instanceManager = deps.InstanceManager;
        _assemblyLoader = deps.AssemblyLoader;
        _executor = deps.Executor;
        _executionService = deps.ExecutionService;
        _publishingService = deps.PublishingService;

        // initialize discovery helper and start background scanning loop
        _pluginDiscovery = new BrowserPluginDiscovery(_discovery, _assemblyLoader, _logger, _instanceManager);
        _scanTask = Task.Run(() => _pluginDiscovery.ScanLoopAsync(_scanCts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _scanCts.Cancel();
        }
        catch { }

        if (_scanTask is not null)
        {
            try { await _scanTask.ConfigureAwait(false); } catch { }
        }
    }

    public string PluginRootPath => _discovery.PluginRootPath;

    public void EnsurePluginDirectoryExists() => _discovery.EnsurePluginDirectoryExists();
    // Minimal wrappers to the previous BrowserPluginHost API used by Web.
    // Implementations are intentionally thin delegations that the Web-level
    // BrowserPluginHost adapter will call into when it needs richer environment
    // (IWebHostEnvironment, SignalR, etc.). This keeps Core independent.
    // 插件目录下可用插件描述及错误
    public CoreModels.BrowserPluginCatalogResponse GetPluginCatalog()
    {
        var discovery = _pluginDiscovery.GetLatestSnapshot();
        var errors = new List<string>(discovery.Errors);
        var plugins = discovery.Plugins
            .Select(plugin => BrowserPluginHostCoreHelpers.CreatePluginDescriptor(plugin, errors, _instanceManager))
            .Where(plugin => plugin is not null)
            .Select(p => p!).ToArray()!;
        var errorsDetailed = discovery.ErrorsDetailed ?? Array.Empty<CoreModels.BrowserPluginErrorDescriptor>();
        return new CoreModels.BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    public Task<CoreModels.BrowserPluginCatalogResponse> LoadPluginAssemblyAsync(string pluginPath, CancellationToken cancellationToken = default)
        => Task.FromResult(_pluginDiscovery.LoadPluginAssembly(pluginPath));

    public Task<(bool Success, IReadOnlyList<string> Errors)> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        => Task.FromResult(_pluginDiscovery.UnloadPlugin(pluginId));

    public async Task<CoreModels.BrowserPluginCatalogResponse> ScanPluginsAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = _discovery.DiscoverAll(_assemblyLoader);

        lock (_snapshotLock)
        {
            _latestSnapshot = snapshot;
        }

        var errors = new List<string>(snapshot.Errors);
        var plugins = snapshot.Plugins
            .Select(plugin => BrowserPluginHostCoreHelpers.CreatePluginDescriptor(plugin, errors, _instanceManager))
            .Where(plugin => plugin is not null)
            .Select(p => p!).ToArray();
        var errorsDetailed = snapshot.ErrorsDetailed ?? Array.Empty<CoreModels.BrowserPluginErrorDescriptor>();
        return new CoreModels.BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    public IReadOnlyList<CoreModels.BrowserPluginDescriptor?> GetPlugins() => GetPluginCatalog().Plugins;

    public async Task<CoreModels.BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = _pluginDiscovery.GetLatestSnapshot().Plugins;
        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;
        var instance = _instanceManager.GetOrCreateInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        _instanceManager.EnsureFreshLifecycleToken(plugin);
        var instanceCtn = _instanceManager.GetOrCreateInstance(plugin);
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, instanceCtn.LifecycleCancellationToken);
        var hostContext = new PluginHostContextCore(_browserInstances.BrowserContext, _browserInstances.ActivePage, _browserInstances.CurrentInstanceId, normalizedArguments, plugin.Id, command, combinedCts.Token, (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, command, message, data, openModal, connectionId, combinedCts.Token));
        if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            try { _instanceManager.CancelLifecycle(plugin); } catch { }
        }
        var result = await _executionService.ExecuteControlAsync(instance, command, hostContext, combinedCts.Token);
        combinedCts.Dispose();
        return new CoreModels.BrowserPluginExecutionResponse(plugin.Id, command, result.Message, instance.Instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
    }

    public async Task<CoreModels.BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = _pluginDiscovery.GetLatestSnapshot().Plugins;
        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;
        var action = plugin.Actions.FirstOrDefault(item => string.Equals(item.Id, functionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
            return null;
        var instance = _instanceManager.GetOrCreateInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        _instanceManager.EnsureFreshLifecycleToken(plugin);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, instance.LifecycleCancellationToken);
        var hostContext = new MeowAutoChrome.Core.Services.PluginHost.PluginHostContextCore(_browserInstances.BrowserContext, _browserInstances.ActivePage, _browserInstances.CurrentInstanceId, normalizedArguments, plugin.Id, action.Id, combinedCts.Token, (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, action.Id, message, data, openModal, connectionId, combinedCts.Token));
        var result = await _executionService.ExecuteActionAsync(instance, action, hostContext, combinedCts.Token);
        return new CoreModels.BrowserPluginExecutionResponse(plugin.Id, action.Id, result.Message, instance.Instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
    }

    // 其余 DiscoverPluginsCore、GetOrCreatePluginInstance、ExecuteWithHostContextAsync 等方法请从 Web 迁移并整理到此处
    // Discovery and metadata helpers have been moved to BrowserPluginDiscovery and PluginMetadataScanner

}
