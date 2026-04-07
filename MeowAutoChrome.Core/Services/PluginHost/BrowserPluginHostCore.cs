using CoreModels = MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件宿主核心实现：包含插件发现、执行与发布逻辑，已从 Web 层迁移以支持在 CLI 环境中运行。<br/>
/// Core implementation of plugin host logic moved from Web. This class does not depend on ASP.NET and can run in CLI environments. It exposes discovery, execution and publishing via IPluginOutputPublisher.
/// </summary>
[SuppressMessage("Maintainability", "Avoid types too big", Justification = "Planned refactor; suppressed temporarily to complete migration.")]
public sealed class BrowserPluginHostCore : IPluginHostCore
{
    private readonly ICoreBrowserInstanceManager _browserInstances;
    private readonly PluginDiscovery.IPluginDiscoveryService _discovery;
    private readonly IPluginOutputPublisher _publisher;
    private readonly ILogger<BrowserPluginHostCore> _logger;
    private readonly AppLogService _appLogService;

    // runtime caches
    private readonly IPluginInstanceManager _instanceManager;
    private readonly PluginExecutionService _executionService;
    private readonly PluginPublishingService _publishingService;
    // assembly loader/executor injected via DI to allow testing and replacement
    private readonly ICorePluginAssemblyLoader _assemblyLoader;
    private readonly IPluginExecutor _executor;
    private readonly BrowserPluginDiscovery _pluginDiscovery;
    private readonly IProgramSettingsProvider? _settingsProvider;
    // cached discovery snapshot maintained by background scanner
    private CoreModels.PluginDiscoverySnapshot _latestSnapshot = new([], [], []);
    private readonly object _snapshotLock = new();
    private readonly CancellationTokenSource _scanCts = new();
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(30);
    private Task? _scanTask;

    /// <summary>
    /// 构造函数：注入依赖并启动后台插件扫描循环。<br/>
    /// Constructor: injects dependencies and starts the background plugin scanning loop.
    /// </summary>
    /// <param name="deps">封装的依赖项集合 / bundled dependencies.</param>
    /// <param name="logger">日志记录器 / logger.</param>
    public BrowserPluginHostCore(BrowserPluginHostDependencies deps, ILogger<BrowserPluginHostCore> logger)
    {
        _browserInstances = deps.BrowserInstances;
        _discovery = deps.Discovery;
        _publisher = deps.Publisher;
        _logger = logger;
        _appLogService = deps.AppLogService;
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

    private Task<PluginBrowserInstanceInfo?> GetBrowserInstanceInfoAsync(string instanceId, CancellationToken ct)
    {
        var inst = _browserInstances.GetInstance(instanceId);
        if (inst is null) return Task.FromResult<PluginBrowserInstanceInfo?>(null);
        var info = new PluginBrowserInstanceInfo(inst.InstanceId, inst.DisplayName, inst.UserDataDirectoryPath, inst.OwnerId, string.Equals(inst.InstanceId, _browserInstances.CurrentInstanceId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<PluginBrowserInstanceInfo?>(info);
    }

    /// <summary>
    /// 异步释放：取消后台扫描并等待任务完成。<br/>
    /// Asynchronously disposes resources: cancels background scanning and awaits the scanning task.
    /// </summary>
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

    /// <summary>
    /// 插件根目录路径（来自发现服务）。<br/>
    /// Plugin root path as provided by the discovery service.
    /// </summary>
    public string PluginRootPath => _discovery.PluginRootPath;

    /// <summary>
    /// 确保插件目录存在（若不存在则创建）。<br/>
    /// Ensure the plugin directory exists (create if missing).
    /// </summary>
    public void EnsurePluginDirectoryExists() => _discovery.EnsurePluginDirectoryExists();
    // Minimal wrappers to the previous BrowserPluginHost API used by Web.
    // Implementations are intentionally thin delegations that the Web-level
    // BrowserPluginHost adapter will call into when it needs richer environment
    // (IWebHostEnvironment, SignalR, etc.). This keeps Core independent.
    // 插件目录下可用插件描述及错误
    /// <summary>
    /// 获取插件目录（可用插件描述与错误信息）。<br/>
    /// Get the plugin catalog containing available plugin descriptors and discovery errors.
    /// </summary>
    public CoreModels.BrowserPluginCatalogResponse GetPluginCatalog()
    {
        var discovery = _pluginDiscovery.GetLatestSnapshot();
        var errors = new List<string>(discovery.Errors);
        var plugins = discovery.Plugins
            .Select(plugin => BrowserPluginHostCoreHelpers.CreatePluginDescriptor(plugin, errors, _instanceManager))
            .Where(plugin => plugin is not null)
            .Select(p => p!).ToArray()!;
        var errorsDetailed = discovery.ErrorsDetailed ?? [];
        return new CoreModels.BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    /// <summary>
    /// 加载指定路径的插件程序集并返回加载后的目录响应（同步包装为任务）。<br/>
    /// Load a plugin assembly at the specified path and return a catalog response (synchronously wrapped as a task).
    /// </summary>
    /// <param name="pluginPath">插件程序集路径 / plugin assembly path.</param>
    public Task<CoreModels.BrowserPluginCatalogResponse> LoadPluginAssemblyAsync(string pluginPath, CancellationToken cancellationToken = default)
        => Task.FromResult(_pluginDiscovery.LoadPluginAssembly(pluginPath));

    /// <summary>
    /// 卸载指定插件并返回操作结果与错误。<br/>
    /// Unload the specified plugin and return success and error list.
    /// </summary>
    /// <param name="pluginId">要卸载的插件 Id / plugin id to unload.</param>
    public Task<(bool Success, IReadOnlyList<string> Errors)> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        => Task.FromResult(_pluginDiscovery.UnloadPlugin(pluginId));

    /// <summary>
    /// 执行一次插件扫描并返回发现的插件快照。<br/>
    /// Perform a plugin discovery scan and return the resulting snapshot.
    /// </summary>
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
        var errorsDetailed = snapshot.ErrorsDetailed ?? [];
        return new CoreModels.BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    /// <summary>
    /// 更新插件根目录并立即触发一次扫描，返回新的目录响应。<br/>
    /// Update the plugin root path and trigger an immediate scan, returning the resulting catalog.
    /// </summary>
    /// <param name="newRoot">新的插件根路径 / new plugin root path.</param>
    public async Task<CoreModels.BrowserPluginCatalogResponse> UpdatePluginRootPathAsync(string newRoot)
    {
        // Normalize input and default
        var separators = new[] { ';', '|' };
        var candidate = string.IsNullOrWhiteSpace(newRoot) ? ProgramSettings.GetDefaultPluginDirectoryPath() : newRoot.Trim();

        // Only allow single directory values
        if (candidate.IndexOfAny(separators) >= 0)
            throw new ArgumentException("只允许设置单个插件根目录。");

        var newRootFull = Path.GetFullPath(candidate);

        // Only allow paths under application data base to prevent arbitrary paths
        var appDataBase = ProgramSettings.GetAppDataDirectoryPath();
        var appDataBaseFull = Path.GetFullPath(appDataBase).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var newRootCheck = newRootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!newRootCheck.StartsWith(appDataBaseFull, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"只允许在应用数据目录下设置插件目录：{appDataBase}");

        // Ensure destination exists
        try { Directory.CreateDirectory(newRootFull); } catch { }

        // Collect existing roots and all dll paths under them before moving
        var rawRoots = _discovery.PluginRootPath ?? string.Empty;
        var oldRoots = rawRoots.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => Path.GetFullPath(r)).ToArray();
        var oldDlls = new List<string>();
        foreach (var root in oldRoots)
        {
            try { if (Directory.Exists(root)) oldDlls.AddRange(Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories)); } catch { }
        }

        // Copy-then-delete files from old roots to new root preserving relative paths. Skip files already under new root.
        // This ensures if the process is interrupted originals remain; subsequent attempts will overwrite the destination.
        var newRootDirWithSep = newRootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var oldRoot in oldRoots)
        {
            try
            {
                if (!Directory.Exists(oldRoot)) continue;
                // don't operate if oldRoot equals newRoot
                if (string.Equals(Path.GetFullPath(oldRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), newRootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var file in Directory.GetFiles(oldRoot, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fileFull = Path.GetFullPath(file);
                        if (fileFull.StartsWith(newRootDirWithSep, StringComparison.OrdinalIgnoreCase))
                            continue; // already in new root

                        var rel = Path.GetRelativePath(oldRoot, fileFull);
                        var dest = Path.Combine(newRootFull, rel);
                        var destDir = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrWhiteSpace(destDir)) Directory.CreateDirectory(destDir);

                        var finalDest = dest;
                        // Copy and overwrite to make the operation idempotent. Only delete original after successful copy.
                        try
                        {
                            File.Copy(fileFull, finalDest, true);
                            try { File.Delete(fileFull); }
                            catch (Exception exDel) { _logger.LogWarning(exDel, "删除原始插件文件失败（保留原件）：{Src}", fileFull); }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "复制插件文件失败：{Src} -> {Dest}", fileFull, finalDest);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "处理插件文件时出错：{File}", file);
                    }
                }

                // Attempt to delete empty subdirectories under oldRoot (best-effort)
                try
                {
                    var dirs = Directory.GetDirectories(oldRoot, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToArray();
                    foreach (var d in dirs)
                    {
                        try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { }
                    }
                    try { if (Directory.Exists(oldRoot) && !Directory.EnumerateFileSystemEntries(oldRoot).Any()) Directory.Delete(oldRoot); } catch { }
                }
                catch { }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "复制插件目录内容时出错：{Root}", oldRoot);
            }
        }

        // Attempt to unregister/unload assemblies by their old paths so the loader doesn't keep stale contexts
        try
        {
            foreach (var oldDll in oldDlls)
            {
                try { _assemblyLoader.UnregisterPlugins(oldDll); } catch { }
                try { _assemblyLoader.Unload(oldDll); } catch { }
            }
        }
        catch { }

        // Finally update discovery root and rescan
        try { _discovery.SetPluginRootPath(newRootFull); } catch { }

        return await ScanPluginsAsync();
    }

    /// <summary>
    /// 返回当前插件目录中可见的插件描述列表。<br/>
    /// Return the list of visible plugin descriptors from the current catalog.
    /// </summary>
    public IReadOnlyList<CoreModels.BrowserPluginDescriptor?> GetPlugins() => GetPluginCatalog().Plugins;

    /// <summary>
    /// 预览为指定宿主创建新浏览器实例时的实例 Id 与用户数据目录（不实际创建）。<br/>
    /// Preview instance id and user data directory that would be used when creating a new browser instance for the specified owner.
    /// </summary>
    public Task<(string InstanceId, string? UserDataDirectory)?> PreviewNewInstanceAsync(string ownerId, string? root = null) => Task.FromResult<(string InstanceId, string? UserDataDirectory)?>(_browserInstances.PreviewNewInstanceAsync(ownerId, root).GetAwaiter().GetResult());

    /// <summary>
    /// 关闭指定的浏览器实例。<br/>
    /// Close the specified browser instance.
    /// </summary>
    public Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default) => _browserInstances.CloseInstanceAsync(instanceId);

    /// <summary>
    /// 执行插件的控制命令（例如 start/stop/pause/resume），并返回执行响应或 null（当插件未找到时）。<br/>
    /// Execute a control command for a plugin (e.g. start/stop/pause/resume) and return the execution response or null when the plugin is not found.
    /// </summary>
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
        var hostContext = new PluginHostContextCore(
            _browserInstances.BrowserContext ?? throw new InvalidOperationException("Browser context is not available"),
            _browserInstances.ActivePage,
            _browserInstances.CurrentInstanceId,
            normalizedArguments,
            plugin.Id,
            command,
            combinedCts.Token,
            (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, command, message, data, openModal, connectionId, combinedCts.Token),
            RequestNewBrowserInstanceAsync,
            GetBrowserInstanceInfoAsync,
            (level, msg, cat) => { try { _appLogService.WriteEntry(level, msg, cat ?? plugin.Id); } catch { } return Task.CompletedTask; }
        );
        if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            try { _instanceManager.CancelLifecycle(plugin); } catch { }
        }
        var result = await _executionService.ExecuteControlAsync(instance, command, hostContext, combinedCts.Token);
        combinedCts.Dispose();
        return new CoreModels.BrowserPluginExecutionResponse(plugin.Id, command, result.Message, instance.Instance.State.ToString(), result.Data);
    }

    /// <summary>
    /// 执行插件动作（指定 functionId），并返回执行响应或 null（当插件或动作未找到时）。<br/>
    /// Execute a plugin action (specified by functionId) and return the execution response or null when not found.
    /// </summary>
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
        var hostContext = new PluginHostContextCore(
            _browserInstances.BrowserContext,
            _browserInstances.ActivePage,
            _browserInstances.CurrentInstanceId,
            normalizedArguments,
            plugin.Id,
            action.Id,
            combinedCts.Token,
            (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, action.Id, message, data, openModal, connectionId, combinedCts.Token),
            RequestNewBrowserInstanceAsync,
            GetBrowserInstanceInfoAsync,
            (level, msg, cat) => { try { _appLogService.WriteEntry(level, msg, cat ?? plugin.Id); } catch { } return Task.CompletedTask; }
        );


        var result = await _executionService.ExecuteActionAsync(instance, action, hostContext, combinedCts.Token);
        return new CoreModels.BrowserPluginExecutionResponse(plugin.Id, action.Id, result.Message, instance.Instance.State.ToString(), result.Data);
    }

    // 其余 DiscoverPluginsCore、GetOrCreatePluginInstance、ExecuteWithHostContextAsync 等方法请从 Web 迁移并整理到此处
    // Discovery and metadata helpers have been moved to BrowserPluginDiscovery and PluginMetadataScanner

    // Factory helper to satisfy PluginHostContextCore delegated creation request
    /// <summary>
    /// 工厂辅助方法：根据选项请求创建新的浏览器实例（由宿主执行具体创建）。<br/>
    /// Factory helper to request creation of a new browser instance based on provided options.
    /// </summary>
    private async Task<string?> RequestNewBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken ct)
    {
        var browserType = (options.BrowserType ?? "chromium").ToLowerInvariant();
        if (browserType != "chromium" && browserType != "firefox" && browserType != "webkit")
            return null;
        var ownerId = string.IsNullOrWhiteSpace(options.OwnerId) ? "plugin" : options.OwnerId;
        var display = string.IsNullOrWhiteSpace(options.DisplayName) ? ownerId : options.DisplayName;

        // Load settings possibly provided by host
        var settings = new Struct.ProgramSettings();
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
