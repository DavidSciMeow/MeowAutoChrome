using MeowAutoChrome.Contracts.BrowserPlugin;
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
    private readonly PluginDiscoveryService _discovery;
    private readonly IPluginOutputPublisher _publisher;
    private readonly ILogger<BrowserPluginHostCore> _logger;

    // runtime caches
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _pluginAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginLoadContext> _pluginLoadContexts = new(StringComparer.OrdinalIgnoreCase);

    public BrowserPluginHostCore(BrowserInstanceManagerCore browserInstances, PluginDiscoveryService discovery, IPluginOutputPublisher publisher, ILogger<BrowserPluginHostCore> logger)
    {
        _browserInstances = browserInstances;
        _discovery = discovery;
        _publisher = publisher;
        _logger = logger;
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
        var discovery = DiscoverPluginsCore();
        var errors = new List<string>(discovery.Errors);
        var plugins = discovery.Plugins
            .Select(plugin => CreatePluginDescriptor(plugin, errors))
            .Where(plugin => plugin is not null)
            .ToArray()!;
        var errorsDetailed = discovery.ErrorsDetailed ?? Array.Empty<BrowserPluginErrorDescriptor>();
        return new BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    public IReadOnlyList<BrowserPluginDescriptor?> GetPlugins() => GetPluginCatalog().Plugins;

    public async Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = DiscoverPlugins();
        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;
        var instance = GetOrCreatePluginInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        var instanceCtn = GetOrCreatePluginInstance(plugin);
        instanceCtn.EnsureFreshLifecycleToken();
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
            try { instanceCtn.CancelLifecycle(); } catch { }
        }
        var result = await ExecuteWithHostContextAsync(
            instance,
            hostContext,
            pluginInstance => command.ToLowerInvariant() switch
            {
                "start" => pluginInstance.StartAsync(),
                "stop" => pluginInstance.StopAsync(),
                "pause" => pluginInstance.PauseAsync(),
                "resume" => pluginInstance.ResumeAsync(),
                _ => throw new InvalidOperationException($"不支持的插件控制命令：{command}")
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
        var instance = GetOrCreatePluginInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        instance.EnsureFreshLifecycleToken();
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
        var result = await ExecuteWithHostContextAsync(
            instance,
            hostContext,
            pluginInstance =>
            {
                var invocation = action.Method.Invoke(pluginInstance, BuildInvocationArguments(action.Method, hostContext));
                if (invocation is not Task<PluginActionResult> task)
                    throw new InvalidOperationException($"插件动作返回类型无效：{plugin.Type.FullName}.{action.Method.Name}");
                return task;
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
                candidateTypeNames = DiscoverPluginTypeNames(pluginPath);
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

            var assembly = TryLoadPluginAssembly(pluginPath, errors);
            if (assembly is null)
                continue;

            plugins.AddRange(DiscoverPlugins(assembly, pluginPath, candidateTypeNames, errors));
        }

        return new PluginDiscoverySnapshot(
            [.. plugins
            .Where(plugin => plugin.Actions.Count > 0 || plugin.Controls.Count > 0)
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)],
            [.. errors],
            [.. errorsDetailed]);
    }

    private BrowserPluginDescriptor? CreatePluginDescriptor(RuntimeBrowserPlugin plugin, List<string> errors)
    {
        try
        {
            var instance = GetOrCreatePluginInstance(plugin);

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

    private RuntimeBrowserPluginInstance GetOrCreatePluginInstance(RuntimeBrowserPlugin plugin)
    {
        // simple lock using type's static lock
        lock (this)
        {
            // store instances in a simple dictionary attached to this class
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current;

            if (Activator.CreateInstance(plugin.Type) is not IPlugin instance)
                throw new InvalidOperationException($"无法创建插件实例：{plugin.Type.FullName}");

            current = new RuntimeBrowserPluginInstance(plugin.Type, instance);
            _instances[plugin.Id] = current;
            return current;
        }
    }

    private static async Task<PluginActionResult> ExecuteWithHostContextAsync(RuntimeBrowserPluginInstance instance, IHostContext hostContext, Func<IPlugin, Task<PluginActionResult>> execute, CancellationToken cancellationToken)
    {
        await instance.ExecutionLock.WaitAsync(cancellationToken);

        try
        {
            instance.Instance.HostContext = hostContext;

            try
            {
                return await execute(instance.Instance);
            }
            finally
            {
                instance.Instance.HostContext = null;
            }
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }

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
                    DiscoverControls(type),
                    DiscoverActions(type)));
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

    private static string[] DiscoverPluginTypeNames(string pluginPath)
    {
        using var stream = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream);

        if (!peReader.HasMetadata)
            return Array.Empty<string>();

        var metadataReader = peReader.GetMetadataReader();
        var typeNames = new List<string>();

        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var typeDefinition = metadataReader.GetTypeDefinition(typeHandle);
            var typeName = GetTypeFullName(metadataReader, typeDefinition);
            if (string.IsNullOrWhiteSpace(typeName) || string.Equals(typeName, "<Module>", StringComparison.Ordinal))
                continue;

            if (!HasCustomAttribute(metadataReader, typeDefinition.GetCustomAttributes(), typeof(PluginAttribute).FullName ?? nameof(PluginAttribute)))
                continue;

            typeNames.Add(typeName);
        }

        return [.. typeNames.Distinct(StringComparer.Ordinal)];
    }

    private static bool HasCustomAttribute(MetadataReader metadataReader, CustomAttributeHandleCollection attributes, string attributeFullName)
        => attributes.Any(attributeHandle => string.Equals(GetAttributeTypeFullName(metadataReader, attributeHandle), attributeFullName, StringComparison.Ordinal));

    // Metadata helpers (single copy kept above)

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
            .Where(p => !IsHostParameter(p))
            .Select(p => CreateActionParameter(p, p.GetCustomAttributes<MeowAutoChrome.Contracts.Attributes.PInputAttribute>().LastOrDefault(), null))
            .ToArray();

        controls.Add(new RuntimeBrowserPluginControl(command, name, description, parameters));
    }

    private static object?[] BuildInvocationArguments(MethodInfo method, IHostContext hostContext)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            if (IsHostParameter(p))
            {
                args[i] = hostContext;
                continue;
            }

            hostContext.Arguments.TryGetValue(p.Name ?? string.Empty, out var rawValue);

            if (rawValue is null)
            {
                if (p.HasDefaultValue)
                    args[i] = p.DefaultValue;
                else
                    args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                continue;
            }

            try
            {
                var targetType = p.ParameterType;
                if (targetType == typeof(string))
                    args[i] = rawValue;
                else if (targetType.IsEnum)
                    args[i] = Enum.Parse(targetType, rawValue, ignoreCase: true);
                else
                    args[i] = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                args[i] = rawValue;
            }
        }

        return args;
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
                .Where(parameter => !IsHostParameter(parameter))
                .Select(parameter => CreateActionParameter(
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
        return typeof(Task<PluginActionResult>).IsAssignableFrom(method.ReturnType);
    }

    private static bool IsHostParameter(ParameterInfo parameter)
        => typeof(IHostContext).IsAssignableFrom(parameter.ParameterType);

    private static RuntimeBrowserPluginParameter CreateActionParameter(ParameterInfo parameter, MeowAutoChrome.Contracts.Attributes.PInputAttribute? attribute, MeowAutoChrome.Contracts.Attributes.PInputAttribute? legacy)
    {
        var param = new RuntimeBrowserPluginParameter
        {
            Name = parameter.Name ?? string.Empty,
            Label = attribute?.Label ?? legacy?.Label ?? parameter.Name ?? string.Empty,
            Description = attribute?.Description ?? legacy?.Description,
            DefaultValue = attribute?.DefaultValue ?? legacy?.DefaultValue,
            Required = attribute?.Required ?? legacy?.Required ?? false,
            InputType = attribute?.InputType ?? legacy?.InputType ?? "text",
            Options = Array.Empty<(string, string)>()
        };

        return param;
    }

    private Assembly? TryLoadPluginAssembly(string pluginPath, List<string> errors)
    {
        try
        {
            var fullPath = Path.GetFullPath(pluginPath);

            lock (this)
            {
                if (_pluginAssemblies.TryGetValue(fullPath, out var loaded))
                    return loaded;
            }

            var loadContext = new PluginLoadContext(fullPath);
            var assembly = loadContext.LoadFromAssemblyPath(fullPath);

            lock (this)
            {
                _pluginLoadContexts[fullPath] = loadContext;
                _pluginAssemblies[fullPath] = assembly;
            }

            return assembly;
        }
        catch (Exception ex)
        {
            var detail = ex.ToString();
            var message = $"插件程序集加载失败：{Path.GetFileName(pluginPath)} -> {detail}";
            _logger.LogError(ex, "加载插件程序集失败：{PluginAssembly}", pluginPath);
            errors.Add(message);
            return null;
        }
    }

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
