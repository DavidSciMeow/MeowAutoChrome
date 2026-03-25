using MeowAutoChrome.Contracts.BrowserPlugin;
using MeowAutoChrome.Contracts.Abstractions;
using System.Linq;
using MeowAutoChrome.Contracts.Interface;
using MeowAutoChrome.Core.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Interface;
using MeowAutoChrome.Core.Services.PluginDiscovery;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Contracts.BrowserPlugin;
using MeowAutoChrome.Contracts.Interface;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Core implementation of plugin host logic moved from Web.
/// This class does not depend on ASP.NET types and can run in CLI environments.
/// It exposes discovery, execution and publishing via IPluginOutputPublisher.
/// </summary>
public sealed class BrowserPluginHostCore : MeowAutoChrome.Core.Interface.IPluginHostCore
{
    private readonly BrowserInstanceManagerCore _browserInstances;
    private readonly MeowAutoChrome.Core.Services.PluginDiscovery.IPluginDiscoveryService _discovery;
    private readonly IPluginOutputPublisher _publisher;
    private readonly ILogger<BrowserPluginHostCore> _logger;

    // runtime caches
    private readonly IPluginInstanceManager _instanceManager;
    // assembly loader/executor injected via DI to allow testing and replacement
    private readonly IPluginAssemblyLoader _assemblyLoader;
    private readonly IPluginExecutor _executor;
    // cached discovery snapshot maintained by background scanner
    private PluginDiscoverySnapshot _latestSnapshot = new PluginDiscoverySnapshot(Array.Empty<RuntimeBrowserPlugin>(), Array.Empty<string>(), Array.Empty<BrowserPluginErrorDescriptor>());
    private readonly object _snapshotLock = new();
    private readonly CancellationTokenSource _scanCts = new();
    private readonly TimeSpan _scanInterval = TimeSpan.FromSeconds(30);
    private Task? _scanTask;

    public BrowserPluginHostCore(
        BrowserInstanceManagerCore browserInstances,
        MeowAutoChrome.Core.Services.PluginDiscovery.IPluginDiscoveryService discovery,
        IPluginOutputPublisher publisher,
        ILogger<BrowserPluginHostCore> logger,
        IPluginInstanceManager instanceManager,
        IPluginAssemblyLoader assemblyLoader,
        IPluginExecutor executor)
    {
        _browserInstances = browserInstances;
        _discovery = discovery;
        _publisher = publisher;
        _logger = logger;
        _instanceManager = instanceManager;
        _assemblyLoader = assemblyLoader;
        _executor = executor;

        // initial scan and start background scanning loop
        try
        {
            _latestSnapshot = _discovery.DiscoverAll(_assemblyLoader);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial plugin discovery failed.");
            _latestSnapshot = new PluginDiscoverySnapshot(Array.Empty<RuntimeBrowserPlugin>(), Array.Empty<string>(), Array.Empty<BrowserPluginErrorDescriptor>());
        }

        _scanTask = Task.Run(() => ScanLoopAsync(_scanCts.Token));
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
    public BrowserPluginCatalogResponse GetPluginCatalog()
    {
        var discovery = GetLatestSnapshot();
        var errors = new List<string>(discovery.Errors);
        var plugins = discovery.Plugins
            .Select(plugin => BrowserPluginHostCoreHelpers.CreatePluginDescriptor(plugin, errors, _instanceManager))
            .Where(plugin => plugin is not null)
            .ToArray()!;
        var errorsDetailed = discovery.ErrorsDetailed ?? Array.Empty<BrowserPluginErrorDescriptor>();
        return new BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    public Task<BrowserPluginCatalogResponse> LoadPluginAssemblyAsync(string pluginPath, CancellationToken cancellationToken = default)
        => Task.FromResult(LoadPluginAssembly(pluginPath));

    public Task<(bool Success, IReadOnlyList<string> Errors)> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default)
        => Task.FromResult(UnloadPlugin(pluginId));

    public async Task<BrowserPluginCatalogResponse> ScanPluginsAsync(CancellationToken cancellationToken = default)
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
            .ToArray()!;
        var errorsDetailed = snapshot.ErrorsDetailed ?? Array.Empty<BrowserPluginErrorDescriptor>();
        return new BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    public IReadOnlyList<BrowserPluginDescriptor?> GetPlugins() => GetPluginCatalog().Plugins;

    public async Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = DiscoverPlugins();
        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;
        var instance = _instanceManager.GetOrCreateInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        _instanceManager.EnsureFreshLifecycleToken(plugin);
        var instanceCtn = _instanceManager.GetOrCreateInstance(plugin);
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, instanceCtn.LifecycleCancellationToken);
            var hostContext = new PluginHostContext(
            _browserInstances.BrowserContext,
            _browserInstances.ActivePage,
            _browserInstances.CurrentInstanceId,
            (MeowAutoChrome.Contracts.Interface.IBrowserInstanceManager)_browserInstances,
            normalizedArguments,
            plugin.Id,
            command,
            combinedCts.Token,
            (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, command, message, data, openModal, connectionId, combinedCts.Token));
        if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
        {
            try { _instanceManager.CancelLifecycle(plugin); } catch { }
        }
        var result = await _executor.ExecuteAsync(
            instance,
            hostContext,
            pluginInstance => command.ToLowerInvariant() switch
            {
                "start" => pluginInstance.StartAsync(),
                "stop" => pluginInstance.StopAsync(),
                "pause" => pluginInstance.PauseAsync(),
                "resume" => pluginInstance.ResumeAsync(),
                _ => Task.FromResult(new MeowAutoChrome.Contracts.Abstractions.PluginActionResult($"不支持的插件控制命令：{command}", null))
            },
            combinedCts.Token);
        combinedCts.Dispose();
        return new BrowserPluginExecutionResponse(plugin.Id, command, result.Message, instance.Instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
    }

    public async Task<BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = DiscoverPlugins();
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
        var hostContext = new PluginHostContext(
            _browserInstances.BrowserContext,
            _browserInstances.ActivePage,
            _browserInstances.CurrentInstanceId,
            (MeowAutoChrome.Contracts.Interface.IBrowserInstanceManager)_browserInstances,
            normalizedArguments,
            plugin.Id,
            action.Id,
            combinedCts.Token,
            (message, data, openModal) => _publisher.PublishPluginOutputAsync(plugin.Id, action.Id, message, data, openModal, connectionId, combinedCts.Token));
        var result = await _executor.ExecuteAsync(
            instance,
            hostContext,
            async pluginInstance =>
            {
                var invocation = action.Method.Invoke(pluginInstance, PluginParameterBinder.BuildInvocationArguments(action.Method, hostContext));
                if (invocation is Task<MeowAutoChrome.Contracts.Abstractions.PluginActionResult> task)
                    return await task.ConfigureAwait(false);
                if (invocation is Task genericTask)
                {
                    await genericTask.ConfigureAwait(false);
                    var resultProp = genericTask.GetType().GetProperty("Result");
                    if (resultProp is not null)
                    {
                        var res = resultProp.GetValue(genericTask);
                        if (res is MeowAutoChrome.Contracts.Abstractions.PluginActionResult par)
                            return par;
                    }
                }
                throw new InvalidOperationException($"插件动作返回类型无效：{plugin.Type.FullName}.{action.Method.Name}");
            },
            combinedCts.Token);
        return new BrowserPluginExecutionResponse(plugin.Id, action.Id, result.Message, instance.Instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
    }

    // 其余 DiscoverPluginsCore、GetOrCreatePluginInstance、ExecuteWithHostContextAsync 等方法请从 Web 迁移并整理到此处
    private IReadOnlyList<RuntimeBrowserPlugin> DiscoverPlugins()
        => DiscoverPluginsCore().Plugins;

    private PluginDiscoverySnapshot DiscoverPluginsCore()
    {
        EnsurePluginDirectoryExists();

        var plugins = new List<RuntimeBrowserPlugin>();
        var errors = new List<string>();
        var errorsDetailed = new List<BrowserPluginErrorDescriptor>();

        foreach (var pluginPath in Directory.EnumerateFiles(PluginRootPath, "*.dll", SearchOption.AllDirectories))
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
                errorsDetailed.Add(new BrowserPluginErrorDescriptor(Path.GetFileName(pluginPath), ex.Message, detail));
                continue;
            }

            if (candidateTypeNames.Length == 0)
                continue;

            var assembly = _assemblyLoader.Load(pluginPath, errors);
            if (assembly is null)
                continue;

            var discovered = DiscoverPlugins(assembly, pluginPath, candidateTypeNames, errors);
            plugins.AddRange(discovered);
            // register plugin ids to assembly map for potential unload
            _assemblyLoader.RegisterPlugins(pluginPath, discovered.Select(p => p.Id));
        }

        return new PluginDiscoverySnapshot(
            [.. plugins
            .Where(plugin => plugin.Actions.Count > 0 || plugin.Controls.Count > 0)
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)],
            [.. errors],
            [.. errorsDetailed]);
    }

    private async Task ScanLoopAsync(CancellationToken cancellationToken)
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

    // Public access to latest snapshot so Web can query without triggering discovery
    public PluginDiscoverySnapshot GetLatestSnapshot()
    {
        lock (_snapshotLock)
        {
            return _latestSnapshot;
        }
    }

    /// <summary>
    /// Attempt to load a single plugin assembly path and register discovered plugins.
    /// Returns catalog response with any errors and loaded plugin descriptors.
    /// </summary>
    public BrowserPluginCatalogResponse LoadPluginAssembly(string pluginPath)
    {
        var (plugins, errors, errorsDetailed) = _discovery.DiscoverFromAssembly(pluginPath, _assemblyLoader);

        // merge into snapshot
        lock (_snapshotLock)
        {
            var merged = _latestSnapshot.Plugins.Concat(plugins).ToArray();
            _latestSnapshot = new PluginDiscoverySnapshot(merged, _latestSnapshot.Errors.Concat(errors).ToArray(), _latestSnapshot.ErrorsDetailed);
        }

        var outErrors = new List<string>(errors);
        var descriptors = plugins.Select(p => CreatePluginDescriptor(p, outErrors)).Where(d => d is not null).ToArray()!;
        return new BrowserPluginCatalogResponse(descriptors, outErrors, errorsDetailed.ToArray());
    }

    /// <summary>
    /// Attempt to unload plugin(s) by plugin id. If plugin id maps to an assembly, unload assembly and remove instances.
    /// Returns success or errors.
    /// </summary>
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

            // remove instances
            _instanceManager.RemoveInstanceByPluginId(pluginId);

            // unregister plugins and unload assembly
            _assemblyLoader.UnregisterPlugins(path);
            _assemblyLoader.Unload(path);

            // refresh snapshot
            lock (_snapshotLock)
            {
                var remaining = _latestSnapshot.Plugins.Where(p => p.Id != pluginId).ToArray();
                _latestSnapshot = new PluginDiscoverySnapshot(remaining, _latestSnapshot.Errors, _latestSnapshot.ErrorsDetailed);
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

    private BrowserPluginDescriptor? CreatePluginDescriptor(RuntimeBrowserPlugin plugin, List<string> errors)
    {
        try
        {
            var instance = _instanceManager.GetOrCreateInstance(plugin);

            return new BrowserPluginDescriptor(
                plugin.Id,
                plugin.Name,
                plugin.Description,
                instance.Instance.State.ToString(),
                instance.Instance.SupportsPause,
                [.. plugin.Controls
                    .Where(control => instance.Instance.SupportsPause || (control.Command != "pause" && control.Command != "resume"))
                    .Select(control => new BrowserPluginControlDescriptor(
                        control.Command,
                        control.Name,
                        control.Description,
                        [.. control.Parameters
                            .Select(parameter => new BrowserPluginActionParameterDescriptor(
                                parameter.Name,
                                parameter.Label,
                                parameter.Description,
                                parameter.DefaultValue,
                                parameter.Required,
                                parameter.InputType,
                                [.. parameter.Options.Select(option => new BrowserPluginActionParameterOptionDescriptor(option.Value, option.Label))]))]))],
                [.. plugin.Actions
                    .Select(action => new BrowserPluginFunctionDescriptor(
                        action.Id,
                        action.Name,
                        action.Description,
                        [.. action.Parameters
                            .Select(parameter => new BrowserPluginActionParameterDescriptor(
                                parameter.Name,
                                parameter.Label,
                                parameter.Description,
                                parameter.DefaultValue,
                                parameter.Required,
                                parameter.InputType,
                                [.. parameter.Options.Select(option => new BrowserPluginActionParameterOptionDescriptor(option.Value, option.Label))]))]))]);
        }
        catch (Exception ex)
        {
            var message = $"插件 {plugin.Type.FullName} 初始化失败：{ex.Message}";
            _logger.LogError(ex, "插件 {PluginType} 初始化失败。", plugin.Type.FullName);
            errors.Add(message);
            return null;
        }
    }

    // Instance management moved to PluginInstanceManager

    // Execution logic moved to PluginExecutor

    private List<RuntimeBrowserPlugin> DiscoverPlugins(Assembly assembly, string pluginPath, IReadOnlyList<string> candidateTypeNames, List<string> errors)
    {
        var plugins = new List<RuntimeBrowserPlugin>();

        foreach (var candidateTypeName in candidateTypeNames)
        {
            try
            {
                var type = assembly.GetType(candidateTypeName, throwOnError: false, ignoreCase: false);
                if (type is not { IsAbstract: false, IsInterface: false } || !typeof(IPlugin).IsAssignableFrom(type))
                    continue;

                var pluginAttribute = type.GetCustomAttribute<PluginAttribute>();
                if (pluginAttribute is null)
                    continue;

                plugins.Add(new RuntimeBrowserPlugin(
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

    // Metadata helpers have been moved to PluginMetadataScanner to reduce the size of this type.

    private static List<RuntimeBrowserPluginControl> DiscoverControls(Type type)
    {
        var controls = new List<RuntimeBrowserPluginControl>();

        AddControl(type, controls, "start", "启动", "执行插件启动逻辑。", nameof(IPlugin.StartAsync));
        AddControl(type, controls, "stop", "停止", "执行插件停止逻辑。", nameof(IPlugin.StopAsync));
        AddControl(type, controls, "pause", "暂停", "执行插件暂停逻辑。", nameof(IPlugin.PauseAsync));
        AddControl(type, controls, "resume", "恢复", "执行插件恢复逻辑。", nameof(IPlugin.ResumeAsync));

        return controls;
    }

    private static void AddControl(Type type, List<RuntimeBrowserPluginControl> controls, string command, string name, string description, string methodName)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (method is null)
            return;

        // method signature must be supported (Task<PluginActionResult>)
        if (!HasSupportedSignature(method))
            return;

        var parameters = method.GetParameters()
            .Where(p => !PluginParameterBinder.IsHostParameter(p))
            .Select(p => PluginParameterBinder.CreateActionParameter(p, p.GetCustomAttributes<MeowAutoChrome.Contracts.Attributes.PInputAttribute>().LastOrDefault(), null))
            .ToArray();

        controls.Add(new RuntimeBrowserPluginControl(command, name, description, parameters));
    }

    private static object?[] BuildInvocationArguments(MethodInfo method, IHostContext hostContext)
    {
        return PluginParameterBinder.BuildInvocationArguments(method, hostContext);
    }

    private static List<RuntimeBrowserPluginAction> DiscoverActions(Type type)
    {
        var actions = new List<RuntimeBrowserPluginAction>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = method.GetCustomAttribute<MeowAutoChrome.Contracts.Attributes.PActionAttribute>();
            if (attribute is null || !HasSupportedSignature(method))
                continue;

            var legacyParameterMetadata = method
                .GetCustomAttributes<MeowAutoChrome.Contracts.Attributes.PInputAttribute>()
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Label))
                .GroupBy(item => item.Name ?? item.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            var parameters = method
                .GetParameters()
                .Where(parameter => !PluginParameterBinder.IsHostParameter(parameter))
                .Select(parameter => PluginParameterBinder.CreateActionParameter(
                    parameter,
                    parameter.GetCustomAttributes<MeowAutoChrome.Contracts.Attributes.PInputAttribute>().LastOrDefault(),
                    legacyParameterMetadata.GetValueOrDefault(parameter.Name ?? string.Empty)))
                .ToArray();

            var baseId = string.IsNullOrWhiteSpace(attribute.Id) ? method.Name : attribute.Id.Trim();
            var actionId = EnsureUniqueActionId(baseId, usedIds);
            var actionName = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name.Trim();

            actions.Add(new RuntimeBrowserPluginAction(
                actionId,
                actionName,
                attribute.Description,
                method,
                parameters));
        }

        return actions;
    }

    private static string EnsureUniqueActionId(string baseId, HashSet<string> usedIds)
    {
        var id = baseId;
        var suffix = 1;
        while (usedIds.Contains(id))
            id = baseId + "_" + (++suffix).ToString(CultureInfo.InvariantCulture);
        usedIds.Add(id);
        return id;
    }

    private static bool HasSupportedSignature(MethodInfo method)
    {
        var target = typeof(Task<>).MakeGenericType(typeof(MeowAutoChrome.Contracts.Abstractions.PluginActionResult));
        return target.IsAssignableFrom(method.ReturnType);
    }

    // Parameter binding helpers have been moved to PluginParameterBinder to reduce the size of this type.

    // Assembly loading moved to PluginAssemblyLoader

    // Metadata helpers (single copy)
    private static string? GetAttributeTypeFullName(MetadataReader metadataReader, CustomAttributeHandle attributeHandle)
    {
        var attribute = metadataReader.GetCustomAttribute(attributeHandle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
            return null;

        var constructor = metadataReader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        return constructor.Parent.Kind switch
        {
            HandleKind.TypeReference => GetTypeFullName(metadataReader, metadataReader.GetTypeReference((TypeReferenceHandle)constructor.Parent)),
            HandleKind.TypeDefinition => GetTypeFullName(metadataReader, metadataReader.GetTypeDefinition((TypeDefinitionHandle)constructor.Parent)),
            _ => null
        };
    }

    private static string GetTypeFullName(MetadataReader metadataReader, TypeDefinition typeDefinition)
    {
        var typeName = metadataReader.GetString(typeDefinition.Name);
        var typeNamespace = metadataReader.GetString(typeDefinition.Namespace);
        return string.IsNullOrWhiteSpace(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }

    private static string GetTypeFullName(MetadataReader metadataReader, TypeReference typeReference)
    {
        var typeName = metadataReader.GetString(typeReference.Name);
        var typeNamespace = metadataReader.GetString(typeReference.Namespace);
        return string.IsNullOrWhiteSpace(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }

}
