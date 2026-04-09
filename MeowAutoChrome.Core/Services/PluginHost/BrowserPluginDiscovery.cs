using CoreModels = MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件发现辅助类型：负责执行发现扫描、加载单个程序集并维护最新发现快照的内部实现。<br/>
/// Helper responsible for performing discovery scans, loading single assemblies and maintaining the latest discovery snapshot.
/// </summary>
internal sealed class BrowserPluginDiscovery
{
    private readonly IPluginDiscoveryService _discovery;
    private readonly IPluginAssemblyLoader _assemblyLoader;
    private readonly ILogger _logger;
    private readonly IPluginInstanceManager _instanceManager;

    private CoreModels.PluginDiscoverySnapshot _latestSnapshot = new([], [], []);
    private readonly object _snapshotLock = new();
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 创建 BrowserPluginDiscovery 实例，立即执行一次发现以初始化快照。<br/>
    /// Create a BrowserPluginDiscovery instance and perform an initial discovery to initialize the snapshot.
    /// </summary>
    /// <param name="discovery">用于枚举与发现插件的发现服务 / discovery service to enumerate and discover plugins.</param>
    /// <param name="assemblyLoader">用于加载程序集的装载器 / assembly loader used to load plugin assemblies.</param>
    /// <param name="logger">日志记录器 / logger instance.</param>
    /// <param name="instanceManager">插件实例管理器 / plugin instance manager.</param>
    public BrowserPluginDiscovery(IPluginDiscoveryService discovery, IPluginAssemblyLoader assemblyLoader, ILogger logger, IPluginInstanceManager instanceManager)
    {
        _discovery = discovery;
        _assemblyLoader = assemblyLoader;
        _logger = logger;
        _instanceManager = instanceManager;

        try
        {
            _latestSnapshot = _discovery.DiscoverAll(_assemblyLoader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial plugin discovery failed.");
            _latestSnapshot = new CoreModels.PluginDiscoverySnapshot([], [], []);
        }
    }

    /// <summary>
    /// 插件根目录路径。<br/>
    /// Plugin root path.
    /// </summary>
    public string PluginRootPath => _discovery.PluginRootPath;

    /// <summary>
    /// 确保插件目录存在（若不存在则创建）。<br/>
    /// Ensure the plugin directory exists (create if missing).
    /// </summary>
    public void EnsurePluginDirectoryExists() => _discovery.EnsurePluginDirectoryExists();

    /// <summary>
    /// 返回最近一次发现的快照副本。<br/>
    /// Return the latest discovery snapshot.
    /// </summary>
    public CoreModels.PluginDiscoverySnapshot GetLatestSnapshot()
    {
        lock (_snapshotLock) return _latestSnapshot;
    }

    /// <summary>
    /// 扫描插件根目录并基于磁盘上的程序集构建发现快照。<br/>
    /// Scan the plugin root directory and build a discovery snapshot from assemblies on disk.
    /// </summary>
    public CoreModels.PluginDiscoverySnapshot DiscoverPluginsCore()
    {
        EnsurePluginDirectoryExists();

        var plugins = new List<CoreModels.RuntimeBrowserPlugin>();
        var errors = new List<string>();
        var errorsDetailed = new List<CoreModels.BrowserPluginErrorDescriptor>();

        // Use the discovery service to enumerate assemblies so that multiple
        // root paths (separated by ';' or '|') and other discovery policies
        // are consistently respected. PluginDiscoveryService.EnumeratePluginAssemblies
        // performs recursive enumeration under each configured root.
        foreach (var pluginPath in _discovery.EnumeratePluginAssemblies())
        {
            string[] candidateTypeNames;

            try
            {
                candidateTypeNames = PluginMetadataScanner.DiscoverPluginTypeNames(pluginPath);
            }
            catch (Exception ex)
            {
                var detail = ex.ToString();
                var message = $"插件程序集 {Path.GetFileName(pluginPath)} 元数据扫描失败：{detail}";
                _logger.LogError(ex, "插件程序集 {PluginAssembly} 元数据扫描失败。", pluginPath);
                errors.Add(message);
                errorsDetailed.Add(new CoreModels.BrowserPluginErrorDescriptor(Path.GetFileName(pluginPath), ex.Message, detail));
                continue;
            }

            if (candidateTypeNames.Length == 0)
                continue;

            var assembly = _assemblyLoader.Load(pluginPath, errors);
            if (assembly is null)
                continue;

            var discovered = DiscoverPlugins(assembly, pluginPath, candidateTypeNames, errors);
            plugins.AddRange(discovered);
            _assemblyLoader.RegisterPlugins(pluginPath, discovered.Select(p => p.Id));
        }

        var snapshot = new CoreModels.PluginDiscoverySnapshot(
            plugins
                .Where(plugin => plugin.Actions.Count > 0 || plugin.Controls.Count > 0)
                .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            errors.ToArray(),
            errorsDetailed.ToArray());

        lock (_snapshotLock)
        {
            _latestSnapshot = snapshot;
        }

        return snapshot;
    }

    /// <summary>
    /// 加载单个插件程序集并将发现结果合并入内部快照，返回加载后的目录响应。<br/>
    /// Load a single plugin assembly and merge discovery results into the internal snapshot, returning a catalog response.
    /// </summary>
    /// <param name="pluginPath">要加载的插件程序集文件路径 / plugin assembly file path to load.</param>
    public CoreModels.BrowserPluginCatalogResponse LoadPluginAssembly(string pluginPath)
    {
        var (plugins, errors, errorsDetailed) = _discovery.DiscoverFromAssembly(pluginPath, _assemblyLoader);

        lock (_snapshotLock)
        {
            var merged = _latestSnapshot.Plugins.Concat(plugins).ToArray();
            _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(merged, _latestSnapshot.Errors.Concat(errors).ToArray(), _latestSnapshot.ErrorsDetailed);
        }

        var outErrors = new List<string>(errors);
        var descriptors = plugins.Select(p => BrowserPluginHostCoreHelpers.CreatePluginDescriptor(p, outErrors, _instanceManager)).Where(d => d is not null).ToArray()!;
        return new CoreModels.BrowserPluginCatalogResponse(descriptors.Select(d => d!).ToArray(), outErrors, errorsDetailed.ToArray());
    }

    /// <summary>
    /// 卸载指定插件：移除实例、注销并卸载程序集，返回成功标志与错误列表。<br/>
    /// Unload the specified plugin: remove instances, unregister and unload the assembly, returning success flag and errors.
    /// </summary>
    /// <param name="pluginId">要卸载的插件 ID / plugin id to unload.</param>
    public (bool Success, IReadOnlyList<string> Errors) UnloadPlugin(string pluginId)
    {
        var errors = new List<string>();
        try
        {
            var path = _assemblyLoader.GetAssemblyPathForPluginId(pluginId);
            if (path is null)
            {
                errors.Add($"Plugin id {pluginId} not found or not loaded.");
                return (false, errors);
            }

            _instanceManager.RemoveInstanceByPluginId(pluginId);
            _assemblyLoader.UnregisterPlugins(path);
            _assemblyLoader.Unload(path);

            lock (_snapshotLock)
            {
                var remaining = _latestSnapshot.Plugins.Where(p => p.Id != pluginId).ToArray();
                _latestSnapshot = new CoreModels.PluginDiscoverySnapshot(remaining, _latestSnapshot.Errors, _latestSnapshot.ErrorsDetailed);
            }

            return (true, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload plugin {PluginId}", pluginId);
            errors.Add(ex.Message);
            return (false, errors);
        }
    }

    /// <summary>
    /// 在指定程序集内根据候选类型名称发现插件类型并返回运行时插件描述列表。<br/>
    /// Discover plugin types within the specified assembly based on candidate type names and return runtime plugin descriptions.
    /// </summary>
    /// <param name="assembly">要扫描的程序集 / assembly to scan.</param>
    /// <param name="pluginPath">程序集路径（用于日志/错误信息）/ assembly path (for logging/error context).</param>
    /// <param name="candidateTypeNames">候选类型全名列表 / candidate type full names to inspect.</param>
    /// <param name="errors">用于收集错误的可变列表 / list to append error messages to.</param>
    public List<CoreModels.RuntimeBrowserPlugin> DiscoverPlugins(Assembly assembly, string pluginPath, IReadOnlyList<string> candidateTypeNames, List<string> errors)
    {
        var plugins = new List<CoreModels.RuntimeBrowserPlugin>();

        foreach (var candidateTypeName in candidateTypeNames)
        {
            try
            {
                var type = assembly.GetType(candidateTypeName, throwOnError: false, ignoreCase: false);
                if (type is null)
                {
                    errors.Add($"插件类型 {candidateTypeName} 无法从程序集 {Path.GetFileName(pluginPath)} 中加载。");
                    continue;
                }

                if (type.IsAbstract || type.IsInterface)
                {
                    errors.Add($"插件类型 {candidateTypeName} 已标记为插件导出，但它是抽象类型或接口，无法实例化。");
                    continue;
                }

                if (!typeof(Contracts.IPlugin).IsAssignableFrom(type))
                {
                    errors.Add($"插件类型 {candidateTypeName} 已标记为插件导出，但未实现 {nameof(Contracts.IPlugin)}。继承 PluginBase 也可以满足该要求。");
                    continue;
                }

                var pluginAttribute = type.GetCustomAttribute<Contracts.Attributes.PluginAttribute>();
                if (pluginAttribute is null)
                    continue;

                plugins.Add(new CoreModels.RuntimeBrowserPlugin(
                    pluginAttribute.Id,
                    pluginAttribute.Name,
                    pluginAttribute.Description,
                    type,
                    BrowserPluginHostCoreHelpers.DiscoverControls(type),
                    BrowserPluginHostCoreHelpers.DiscoverActions(type)));
            }
            catch (Exception ex)
            {
                var detail = ex.ToString();
                var message = $"插件类型 {candidateTypeName} 发现失败：{detail}";
                _logger.LogError(ex, "插件类型 {PluginType} 发现失败。插件程序集：{PluginAssembly}", candidateTypeName, pluginPath);
                errors.Add(message);
            }
        }

        return plugins;
    }

    /// <summary>
    /// 后台扫描循环：定期重新扫描插件并更新内部快照直到取消。<br/>
    /// Background scan loop that periodically rescans plugins and updates the internal snapshot until cancelled.
    /// </summary>
    /// <param name="cancellationToken">取消令牌，用于在停止时终止循环 / cancellation token to stop the background scan loop.</param>
    public async Task ScanLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = _discovery.DiscoverAll(_assemblyLoader);
                lock (_snapshotLock)
                {
                    _latestSnapshot = snapshot;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Background plugin scan failed.");
            }

            try { await Task.Delay(_scanInterval, cancellationToken); } catch { }
        }
    }
}
