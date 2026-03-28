using System.Diagnostics.CodeAnalysis;
using MeowAutoChrome.Contracts;
using Microsoft.Extensions.Logging;
using CoreModels = MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;

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
    private readonly MeowAutoChrome.Core.Interface.IProgramSettingsProvider? _settingsProvider;
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
        _settingsProvider = deps.SettingsProvider;

        // initialize discovery helper and start background scanning loop
        _pluginDiscovery = new BrowserPluginDiscovery(_discovery, _assemblyLoader, _logger, _instanceManager);
        _scanTask = Task.Run(() => _pluginDiscovery.ScanLoopAsync(_scanCts.Token));
    }

    private Task<MeowAutoChrome.Contracts.PluginBrowserInstanceInfo?> GetBrowserInstanceInfoAsync(string instanceId, CancellationToken ct)
    {
        var inst = _browserInstances.GetInstance(instanceId);
        if (inst is null) return Task.FromResult<MeowAutoChrome.Contracts.PluginBrowserInstanceInfo?>(null);
        var info = new MeowAutoChrome.Contracts.PluginBrowserInstanceInfo(inst.InstanceId, inst.DisplayName, inst.UserDataDirectoryPath, inst.OwnerId, string.Equals(inst.InstanceId, _browserInstances.CurrentInstanceId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<MeowAutoChrome.Contracts.PluginBrowserInstanceInfo?>(info);
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

    public Task<(string InstanceId, string? UserDataDirectory)?> PreviewNewInstanceAsync(string ownerId, string? root = null)
        => Task.FromResult<(string InstanceId, string? UserDataDirectory)?>(_browserInstances.PreviewNewInstanceAsync(ownerId, root).GetAwaiter().GetResult());

    public Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default)
        => _browserInstances.CloseInstanceAsync(instanceId);

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
        var hostContext = new PluginHostContextCore(_browserInstances.BrowserContext, _browserInstances.ActivePage, _browserInstances.CurrentInstanceId, normalizedArguments, plugin.Id, command, combinedCts.Token, (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, command, message, data, openModal, connectionId, combinedCts.Token), RequestNewBrowserInstanceAsync, GetBrowserInstanceInfoAsync);
        if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            try { _instanceManager.CancelLifecycle(plugin); } catch { }
        }
        var result = await _executionService.ExecuteControlAsync(instance, command, hostContext, combinedCts.Token);
        combinedCts.Dispose();
        return new CoreModels.BrowserPluginExecutionResponse(plugin.Id, command, result.Message, instance.Instance.State.ToString(), result.Data);
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
        var hostContext = new MeowAutoChrome.Core.Services.PluginHost.PluginHostContextCore(_browserInstances.BrowserContext, _browserInstances.ActivePage, _browserInstances.CurrentInstanceId, normalizedArguments, plugin.Id, action.Id, combinedCts.Token, (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, action.Id, message, data, openModal, connectionId, combinedCts.Token), RequestNewBrowserInstanceAsync, GetBrowserInstanceInfoAsync);


        var result = await _executionService.ExecuteActionAsync(instance, action, hostContext, combinedCts.Token);
        return new CoreModels.BrowserPluginExecutionResponse(plugin.Id, action.Id, result.Message, instance.Instance.State.ToString(), result.Data);
    }

    // 其余 DiscoverPluginsCore、GetOrCreatePluginInstance、ExecuteWithHostContextAsync 等方法请从 Web 迁移并整理到此处
    // Discovery and metadata helpers have been moved to BrowserPluginDiscovery and PluginMetadataScanner

    // Factory helper to satisfy PluginHostContextCore delegated creation request
    private async Task<string?> RequestNewBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken ct)
    {
        var browserType = (options.BrowserType ?? "chromium").ToLowerInvariant();
        if (browserType != "chromium" && browserType != "firefox" && browserType != "webkit")
            return null;
        var ownerId = string.IsNullOrWhiteSpace(options.OwnerId) ? "plugin" : options.OwnerId;
        var display = string.IsNullOrWhiteSpace(options.DisplayName) ? ownerId : options.DisplayName;

        // Load settings possibly provided by host
        var settings = new MeowAutoChrome.Core.Struct.ProgramSettings();
        try { settings = _settingsProvider is not null ? await _settingsProvider.GetAsync() : settings; } catch { }

        var maxInstances = settings.MaxInstancesPerPlugin;
        if (maxInstances > 0)
        {
            var existing = _browserInstances.GetPluginInstanceIds(ownerId);
            if (existing.Count >= maxInstances)
            {
                _logger.LogWarning("Plugin {Plugin} exceeded max instances quota ({Max})", ownerId, maxInstances);
                return null;
            }
        }

        var defaultRoot = settings.UserDataDirectory;
        string userData;
        if (string.IsNullOrWhiteSpace(options.UserDataDirectory))
        {
            userData = defaultRoot;
        }
        else
        {
            try { userData = Path.GetFullPath(options.UserDataDirectory!); } catch { _logger.LogWarning("Invalid userDataDirectory requested by {Plugin}", ownerId); return null; }
            var rootFull = Path.GetFullPath(defaultRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!userData.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Plugin {Plugin} requested disallowed user data directory {Path}", ownerId, userData);
                return null;
            }
        }

        if (options.Args is not null && settings.DisallowedBrowserArgs is not null)
        {
            foreach (var a in options.Args)
            {
                if (string.IsNullOrWhiteSpace(a)) continue;
                var low = a.ToLowerInvariant();
                if (settings.DisallowedBrowserArgs.Any(d => low.Contains(d)))
                {
                    _logger.LogWarning("Plugin {Plugin} attempted to use disallowed arg {Arg}", ownerId, a);
                    return null;
                }
            }
        }

        try
        {
            var instId = await _browserInstances.CreateAsync(ownerId, display, userData, options.Headless, previewInstanceId: null);
            _logger.LogInformation("Created plugin instance {Inst} for {Plugin}", instId, ownerId);
            return instId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create instance for plugin {Plugin}", ownerId);
        }

        return null;
    }

    // Helper to get a settings provider from DI via dependencies (best-effort)
    private IProgramSettingsProvider? depsAsSettingsProvider()
    {
        try
        {
            // _publisher was set from dependencies; Broker dependencies are not DI here. Try to access via AssemblyLoader's service provider if available - no reliable way here.
            return null;
        }
        catch { return null; }
    }

}
