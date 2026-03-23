using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using MeowAutoChrome.Contracts.BrowserPlugin;
using MeowAutoChrome.Contracts.Interface;

namespace MeowAutoChrome.Web.Services;

/// <summary>
/// 插件宿主服务：负责发现、加载与执行运行时浏览器插件，并将插件的输出通过 SignalR 广播给前端。
/// 支持插件的元数据扫描、依赖解析与隔离加载（AssemblyLoadContext）。
/// </summary>
/// <param name="browserInstances">浏览器实例管理器，用于将插件与浏览器上下文关联。</param>
/// <param name="environment">ASP.NET 主机环境（用于解析路径等）。</param>
/// <param name="browserHub">SignalR Hub 上下文，用于向前端广播插件输出。</param>
/// <param name="logger">日志记录器。</param>
public sealed class BrowserPluginHost(Core.Services.PluginHost.BrowserPluginHostCore coreHost, IHubContext<BrowserHub> browserHub, ILogger<BrowserPluginHost> logger)
{
    private readonly BrowserInstanceManagerCore browserInstances = coreHost is null ? throw new InvalidOperationException() : coreHost.GetType().GetField("_browserInstances", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(coreHost) as BrowserInstanceManagerCore ?? throw new InvalidOperationException();
    /// <summary>
    /// 插件属性特性全名，用于在程序集元数据中识别标记为浏览器插件的类型。
    /// </summary>
    private static readonly string BrowserPluginAttributeFullName = typeof(PluginAttribute).FullName ?? nameof(PluginAttribute);

    /// <summary>
    /// 插件根目录的物理路径（默认为应用目录下的 "Plugins" 子目录）。
    /// </summary>
    private readonly string _pluginRootPath = Path.Combine(AppContext.BaseDirectory, "Plugins");

    /// <summary>
    /// 线程同步对象，用于保护对插件实例及装载上下文字典的并发访问。
    /// </summary>
    private readonly Lock _syncRoot = new();

    /// <summary>
    /// 已创建的插件运行时实例缓存，键为插件 ID。
    /// </summary>
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 已加载的插件程序集缓存，键为插件程序集的完整路径。
    /// </summary>
    private readonly Dictionary<string, Assembly> _pluginAssemblies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 插件对应的自定义 AssemblyLoadContext 缓存，键为插件程序集的完整路径。
    /// </summary>
    private readonly Dictionary<string, PluginLoadContext> _pluginLoadContexts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 用于解析参数可空性信息的上下文实例。
    /// </summary>
    private static readonly NullabilityInfoContext NullabilityContext = new();

    /// <summary>
    /// 插件所在根目录路径（默认为应用目录下的 "Plugins" 子目录）。
    /// </summary>
    public string PluginRootPath => _pluginRootPath;

    /// <summary>
    /// ASP.NET 主机环境（用于解析路径等）。
    /// </summary>
    public object? Environment { get; } = null; // Environment is part of Web host; Web adapter uses local IWebHostEnvironment when needed.


    /// <summary>
    /// 确保插件根目录存在（如果不存在则创建该目录）。
    /// </summary>
    public void EnsurePluginDirectoryExists() => Directory.CreateDirectory(_pluginRootPath);

    /// <summary>
    /// 获取插件目录下可用的插件描述列表及扫描过程中产生的错误信息。
    /// </summary>
    /// <returns>包含插件描述与扫描错误列表的目录响应对象。</returns>
    public BrowserPluginCatalogResponse GetPluginCatalog()
    {
        var discovery = DiscoverPluginsCore();
        var errors = new List<string>(discovery.Errors);
        var plugins = discovery.Plugins
            .Select(plugin => CreatePluginDescriptor(plugin, errors))
            .Where(plugin => plugin is not null)
            .ToArray()!;

        // Map detailed errors into response
        var errorsDetailed = discovery.ErrorsDetailed ?? Array.Empty<BrowserPluginErrorDescriptor>();

        return new BrowserPluginCatalogResponse(plugins, errors, errorsDetailed);
    }

    /// <summary>
    /// 扫描并返回插件目录下可用的插件描述列表（不包含错误信息）。
    /// </summary>
    /// <returns>插件描述列表。</returns>
    public IReadOnlyList<BrowserPluginDescriptor?> GetPlugins()  => GetPluginCatalog().Plugins;

    /// <summary>
    /// 对指定插件执行控制命令（如 start/stop/pause/resume），并返回执行结果。
    /// </summary>
    /// <param name="pluginId">插件 ID。</param>
    /// <param name="command">控制命令（start/stop/pause/resume）。</param>
    /// <param name="arguments">可选的命令参数字典。</param>
    /// <param name="connectionId">可选的 SignalR 连接 ID，用于将输出仅发送给指定客户端。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果或 null（若插件未找到）。</returns>
    public async Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = DiscoverPlugins();

        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;

        var instance = GetOrCreatePluginInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        // 使用插件实例级别的生命周期 CancellationTokenSource，使 Stop 可以显式取消正在运行的插件任务。
        var instanceCtn = GetOrCreatePluginInstance(plugin);
        instanceCtn.EnsureFreshLifecycleToken();
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, instanceCtn.LifecycleCancellationToken);

        var hostContext = new PluginHostContext(
            browserInstances.BrowserContext,
            browserInstances.ActivePage,
            browserInstances.CurrentInstanceId,
            browserInstances,
            normalizedArguments,
            plugin.Id,
            command,
            combinedCts.Token,
            (message, data, openModal) => PublishPluginOutputAsync(plugin.Id, command, message, data, openModal, connectionId, combinedCts.Token));

            // 如果是 stop 命令，先触发生命周期取消，通知插件内部使用的后台任务尽快退出。
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

    /// <summary>
    /// 对指定插件执行动作函数，并返回执行结果。
    /// </summary>
    /// <param name="pluginId">插件 ID。</param>
    /// <param name="functionId">动作函数 ID。</param>
    /// <param name="arguments">可选的函数参数字典。</param>
    /// <param name="connectionId">可选的 SignalR 连接 ID，用于将输出仅发送给指定客户端。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行结果或 null（若插件或函数未找到）。</returns>
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
            browserInstances.BrowserContext,
            browserInstances.ActivePage,
            browserInstances.CurrentInstanceId,
            browserInstances,
            normalizedArguments,
            plugin.Id,
            action.Id,
            combinedCts.Token,
            (message, data, openModal) => PublishPluginOutputAsync(plugin.Id, action.Id, message, data, openModal, connectionId, combinedCts.Token));

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

    /// <summary>
    /// 将插件输出封装为 <see cref="BrowserPluginOutputUpdate"/> 并通过 SignalR 推送到前端。
    /// 若提供了 <paramref name="connectionId"/> 则只发送到指定连接，否则广播到所有连接。
    /// </summary>
    /// <param name="pluginId">来源插件的 ID。</param>
    /// <param name="targetId">输出所属的目标 ID（动作或控制命令）。</param>
    /// <param name="message">要显示的文本消息（可空）。</param>
    /// <param name="data">可选的键值对数据负载。</param>
    /// <param name="openModal">指示前端是否应以模态方式展示该输出。</param>
    /// <param name="connectionId">可选的 SignalR 连接 ID，仅发送到该连接时使用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>代表异步发送操作的任务。</returns>
    private Task PublishPluginOutputAsync(string pluginId, string targetId, string? message, IReadOnlyDictionary<string, string?>? data, bool openModal, string? connectionId, CancellationToken cancellationToken)
    {
        var payload = new BrowserPluginOutputUpdate(
            pluginId,
            targetId,
            message,
            data ?? new Dictionary<string, string?>(),
            openModal,
            DateTimeOffset.UtcNow);

        var clients = string.IsNullOrWhiteSpace(connectionId)
            ? browserHub.Clients.All
            : browserHub.Clients.Client(connectionId);

        return clients.SendAsync("ReceivePluginOutput", payload, cancellationToken);
    }

    /// <summary>
    /// 返回当前已发现的运行时代插件列表（不包含扫描错误信息）。
    /// 该方法是对 <see cref="DiscoverPluginsCore"/> 的简单包装，直接返回插件集合。
    /// </summary>
    /// <returns>已发现的插件描述集合。</returns>
    private IReadOnlyList<RuntimeBrowserPlugin> DiscoverPlugins()
        => DiscoverPluginsCore().Plugins;

    /// <summary>
    /// 扫描插件根目录，发现所有有效插件类型并尝试加载，返回插件信息和扫描错误。
    /// </summary>
    /// <returns>插件发现快照，包括插件集合和错误集合。</returns>
    private PluginDiscoverySnapshot DiscoverPluginsCore()
    {
        EnsurePluginDirectoryExists();

        // 遍历插件目录，逐个扫描 dll 的类型元数据并尝试加载符合约定的插件类型。
        // 返回值包含已解析的插件信息以及扫描过程中收集到的错误描述，便于上层展示或日志记录。
        var plugins = new List<RuntimeBrowserPlugin>();
        var errors = new List<string>();
        var errorsDetailed = new List<BrowserPluginErrorDescriptor>();

        foreach (var pluginPath in Directory.EnumerateFiles(_pluginRootPath, "*.dll", SearchOption.AllDirectories))
        {
            string[] candidateTypeNames;

            try
            {
                candidateTypeNames = DiscoverPluginTypeNames(pluginPath);
            }
            catch (Exception ex)
            {
                var detail = DescribeException(ex);
                var message = $"插件程序集 {Path.GetFileName(pluginPath)} 元数据扫描失败：{detail}";
                logger.LogError(ex, "插件程序集 {PluginAssembly} 元数据扫描失败。", pluginPath);
                errors.Add(message);
                errorsDetailed.Add(new BrowserPluginErrorDescriptor(Path.GetFileName(pluginPath), SummarizeLoaderExceptions(ex as ReflectionTypeLoadException ?? new ReflectionTypeLoadException(Array.Empty<Type>(), new Exception[] { ex })), detail));
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

    /// <summary>
    /// 根据运行时插件信息创建插件描述符，初始化插件实例并处理异常。
    /// </summary>
    /// <param name="plugin">运行时插件信息。</param>
    /// <param name="errors">错误收集列表。</param>
    /// <returns>插件描述符或 null（初始化失败时）。</returns>
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
            logger.LogError(ex, "插件 {PluginType} 初始化失败。", plugin.Type.FullName);
            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    /// 获取或创建指定插件的运行时实例（线程安全）。
    /// </summary>
    /// <param name="plugin">插件元数据。</param>
    /// <returns>插件运行时实例。</returns>
    private RuntimeBrowserPluginInstance GetOrCreatePluginInstance(RuntimeBrowserPlugin plugin)
    {
        lock (_syncRoot)
        {
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current;

            if (Activator.CreateInstance(plugin.Type) is not IPlugin instance)
                throw new InvalidOperationException($"无法创建插件实例：{plugin.Type.FullName}");

            current = new RuntimeBrowserPluginInstance(plugin.Type, instance);
            _instances[plugin.Id] = current;
            return current;
        }
    }

    /// <summary>
    /// 在指定插件实例上以指定 HostContext 执行异步操作，自动处理上下文赋值与并发锁。
    /// </summary>
    /// <param name="instance">插件运行时实例。</param>
    /// <param name="hostContext">宿主上下文。</param>
    /// <param name="execute">执行委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>插件动作执行结果。</returns>
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

    /// <summary>
    /// 在指定程序集内根据候选类型名发现所有有效插件类型。
    /// </summary>
    /// <param name="assembly">插件程序集。</param>
    /// <param name="pluginPath">插件程序集路径。</param>
    /// <param name="candidateTypeNames">候选类型全名集合。</param>
    /// <param name="errors">错误收集列表。</param>
    /// <returns>发现的插件集合。</returns>
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
                var detail = DescribeException(ex);
                var message = $"插件类型 {candidateTypeName} 发现失败：{detail}";
                logger.LogError(ex, "插件类型 {PluginType} 发现失败。插件程序集：{PluginAssembly}", candidateTypeName, pluginPath);
                errors.Add(message);
            }
        }

        return plugins;
    }

    /// <summary>
    /// 扫描插件程序集，返回所有标记为 PluginAttribute 的类型全名。
    /// </summary>
    /// <param name="pluginPath">插件程序集路径。</param>
    /// <returns>插件类型全名集合。</returns>
    private static string[] DiscoverPluginTypeNames(string pluginPath)
    {
        using var stream = new FileStream(pluginPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var peReader = new PEReader(stream);

        if (!peReader.HasMetadata)
            return [];

        var metadataReader = peReader.GetMetadataReader();
        var typeNames = new List<string>();

        foreach (var typeHandle in metadataReader.TypeDefinitions)
        {
            var typeDefinition = metadataReader.GetTypeDefinition(typeHandle);
            var typeName = GetTypeFullName(metadataReader, typeDefinition);
            if (string.IsNullOrWhiteSpace(typeName) || string.Equals(typeName, "<Module>", StringComparison.Ordinal))
                continue;

            if (!HasCustomAttribute(metadataReader, typeDefinition.GetCustomAttributes(), BrowserPluginAttributeFullName))
                continue;

            typeNames.Add(typeName);
        }

        return [.. typeNames.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// 判断类型元数据是否包含指定特性。
    /// </summary>
    /// <param name="metadataReader">元数据读取器。</param>
    /// <param name="attributes">特性句柄集合。</param>
    /// <param name="attributeFullName">特性全名。</param>
    /// <returns>是否包含该特性。</returns>
    private static bool HasCustomAttribute(MetadataReader metadataReader, CustomAttributeHandleCollection attributes, string attributeFullName)
        => attributes.Any(attributeHandle => string.Equals(GetAttributeTypeFullName(metadataReader, attributeHandle), attributeFullName, StringComparison.Ordinal));

    /// <summary>
    /// 获取特性类型的完整名称。
    /// </summary>
    /// <param name="metadataReader">元数据读取器。</param>
    /// <param name="attributeHandle">特性句柄。</param>
    /// <returns>特性类型全名或 null。</returns>
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

    /// <summary>
    /// 获取类型定义的完整名称（含命名空间）。
    /// </summary>
    /// <param name="metadataReader">元数据读取器。</param>
    /// <param name="typeDefinition">类型定义。</param>
    /// <returns>类型全名。</returns>
    private static string GetTypeFullName(MetadataReader metadataReader, TypeDefinition typeDefinition)
    {
        var typeName = metadataReader.GetString(typeDefinition.Name);
        var typeNamespace = metadataReader.GetString(typeDefinition.Namespace);
        return string.IsNullOrWhiteSpace(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }

    /// <summary>
    /// 获取类型引用的完整名称（含命名空间）。
    /// </summary>
    /// <param name="metadataReader">元数据读取器。</param>
    /// <param name="typeReference">类型引用。</param>
    /// <returns>类型全名。</returns>
    private static string GetTypeFullName(MetadataReader metadataReader, TypeReference typeReference)
    {
        var typeName = metadataReader.GetString(typeReference.Name);
        var typeNamespace = metadataReader.GetString(typeReference.Namespace);
        return string.IsNullOrWhiteSpace(typeNamespace) ? typeName : $"{typeNamespace}.{typeName}";
    }

    /// <summary>
    /// 发现插件类型的所有控制命令。
    /// </summary>
    /// <param name="type">插件类型。</param>
    /// <returns>控制命令集合。</returns>
    private static List<RuntimeBrowserPluginControl> DiscoverControls(Type type)
    {
        var controls = new List<RuntimeBrowserPluginControl>();

        AddControl(type, controls, "start", "启动", "执行插件启动逻辑。", nameof(IPlugin.StartAsync));
        AddControl(type, controls, "stop", "停止", "执行插件停止逻辑。", nameof(IPlugin.StopAsync));
        AddControl(type, controls, "pause", "暂停", "执行插件暂停逻辑。", nameof(IPlugin.PauseAsync));
        AddControl(type, controls, "resume", "恢复", "执行插件恢复逻辑。", nameof(IPlugin.ResumeAsync));

        return controls;
    }

    /// <summary>
    /// 发现插件类型的所有动作方法。
    /// </summary>
    /// <param name="type">插件类型。</param>
    /// <returns>动作集合。</returns>
    private static List<RuntimeBrowserPluginAction> DiscoverActions(Type type)
    {
        var actions = new List<RuntimeBrowserPluginAction>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = method.GetCustomAttribute<PActionAttribute>();
            if (attribute is null || !HasSupportedSignature(method))
                continue;

            var legacyParameterMetadata = method
                .GetCustomAttributes<PInputAttribute>()
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Label))
                .GroupBy(item => item.Name ?? item.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            var parameters = method
                .GetParameters()
                .Where(parameter => !IsHostParameter(parameter))
                .Select(parameter => CreateActionParameter(
                    parameter,
                    parameter.GetCustomAttributes<PInputAttribute>().LastOrDefault(),
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

    /// <summary>
    /// 加载指定路径的插件程序集，并缓存其 AssemblyLoadContext。
    /// </summary>
    /// <param name="pluginPath">插件程序集路径。</param>
    /// <param name="errors">错误收集列表。</param>
    /// <returns>加载成功的程序集或 null。</returns>
    private Assembly? TryLoadPluginAssembly(string pluginPath, List<string> errors)
    {
        try
        {
            var fullPath = Path.GetFullPath(pluginPath);

            lock (_syncRoot)
            {
                if (_pluginAssemblies.TryGetValue(fullPath, out var loaded))
                    return loaded;
            }

            var loadContext = new PluginLoadContext(fullPath);
            var assembly = loadContext.LoadFromAssemblyPath(fullPath);

            lock (_syncRoot)
            {
                if (_pluginAssemblies.TryGetValue(fullPath, out var loaded))
                    return loaded;

                _pluginLoadContexts[fullPath] = loadContext;
                _pluginAssemblies[fullPath] = assembly;
                return assembly;
            }
        }
        catch (Exception ex)
        {
            var detail = DescribeException(ex);
            var message = $"插件程序集 {Path.GetFileName(pluginPath)} 加载失败：{detail}";
            logger.LogError(ex, "插件程序集 {PluginAssembly} 加载失败：{LoaderExceptionDetails}", pluginPath, detail);
            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    /// 汇总 ReflectionTypeLoadException 的所有 LoaderException 消息。
    /// </summary>
    /// <param name="exception">类型加载异常。</param>
    /// <returns>简要异常信息。</returns>
    private static string SummarizeLoaderExceptions(ReflectionTypeLoadException exception)
    {
        var messages = GetLoaderExceptionDescriptions(exception)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (messages is not { Length: > 0 })
            return exception.Message;

        return string.Join("； ", messages.Take(3));
    }

    /// <summary>
    /// 获取类型加载异常中的所有 LoaderException 描述。
    /// </summary>
    /// <param name="exception">类型加载异常。</param>
    /// <returns>异常消息集合。</returns>
    private static IEnumerable<string> GetLoaderExceptionDescriptions(ReflectionTypeLoadException exception)
        => exception.LoaderExceptions
            ?.Where(loaderException => loaderException is not null)
            .Select(loaderException => DescribeException(loaderException!))
            .Where(message => !string.IsNullOrWhiteSpace(message))
        ?? [];

    /// <summary>
    /// 生成异常的简要描述。
    /// </summary>
    /// <param name="exception">异常对象。</param>
    /// <returns>异常描述。</returns>
    private static string DescribeException(Exception exception)
        => exception switch
        {
            FileNotFoundException fileNotFoundException => DescribeFileNotFoundException(fileNotFoundException),
            FileLoadException fileLoadException => DescribeFileLoadException(fileLoadException),
            BadImageFormatException badImageFormatException => DescribeBadImageFormatException(badImageFormatException),
            TypeLoadException typeLoadException => DescribeTypeLoadException(typeLoadException),
            _ => exception.Message.Trim()
        };

    /// <summary>
    /// 生成缺失文件异常的描述。
    /// </summary>
    /// <param name="exception">文件未找到异常。</param>
    /// <returns>异常描述。</returns>
    private static string DescribeFileNotFoundException(FileNotFoundException exception)
    {
        var assemblyName = GetAssemblyDisplayName(exception.FileName);
        return string.IsNullOrWhiteSpace(assemblyName)
            ? $"缺少依赖 DLL：{exception.Message.Trim()}"
            : $"缺少依赖 DLL：{assemblyName}";
    }

    /// <summary>
    /// 生成文件加载异常的描述。
    /// </summary>
    /// <param name="exception">文件加载异常。</param>
    /// <returns>异常描述。</returns>
    private static string DescribeFileLoadException(FileLoadException exception)
    {
        var assemblyName = GetAssemblyDisplayName(exception.FileName);
        return string.IsNullOrWhiteSpace(assemblyName)
            ? $"程序集加载失败：{exception.Message.Trim()}"
            : $"程序集加载失败：{assemblyName}（可能是版本不匹配或文件被占用）";
    }

    /// <summary>
    /// 生成程序集格式异常的描述。
    /// </summary>
    /// <param name="exception">程序集格式异常。</param>
    /// <returns>异常描述。</returns>
    private static string DescribeBadImageFormatException(BadImageFormatException exception)
    {
        var assemblyName = GetAssemblyDisplayName(exception.FileName);
        return string.IsNullOrWhiteSpace(assemblyName)
            ? $"程序集格式无效：{exception.Message.Trim()}"
            : $"程序集格式无效：{assemblyName}（可能是目标框架或位数不兼容）";
    }

    /// <summary>
    /// 生成类型加载异常的描述。
    /// </summary>
    /// <param name="exception">类型加载异常。</param>
    /// <returns>异常描述。</returns>
    private static string DescribeTypeLoadException(TypeLoadException exception)
        => string.IsNullOrWhiteSpace(exception.TypeName)
            ? $"类型加载失败：{exception.Message.Trim()}"
            : $"类型加载失败：{exception.TypeName}";

    /// <summary>
    /// 获取程序集文件名或显示名。
    /// </summary>
    /// <param name="assemblyNameOrPath">程序集名或路径。</param>
    /// <returns>程序集显示名。</returns>
    private static string? GetAssemblyDisplayName(string? assemblyNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyNameOrPath))
            return null;

        var candidate = assemblyNameOrPath.Trim();
        if (candidate.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            candidate = Path.GetFileName(candidate);

        if (candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return candidate;

        var commaIndex = candidate.IndexOf(',');
        return commaIndex > 0 ? candidate[..commaIndex].Trim() : candidate;
    }

    /// <summary>
    /// 判断方法签名是否为受支持的插件动作签名。
    /// </summary>
    /// <param name="method">方法信息。</param>
    /// <returns>是否为受支持签名。</returns>
    private static bool HasSupportedSignature(MethodInfo method)
    {
        if (method.ReturnType != typeof(Task<PluginActionResult>))
            return false;

        return method.GetParameters().All(parameter => IsHostParameter(parameter) || IsBindableParameter(parameter));
    }

    /// <summary>
    /// 构建插件方法调用参数数组。
    /// </summary>
    /// <param name="method">方法信息。</param>
    /// <param name="hostContext">宿主上下文。</param>
    /// <returns>参数数组。</returns>
    private static object?[] BuildInvocationArguments(
        MethodInfo method,
        IHostContext hostContext)
    {
        var parameters = method.GetParameters();
        var invocationArguments = new object?[parameters.Length];

        for (var index = 0; index < parameters.Length; index++)
        {
            invocationArguments[index] = ResolveInvocationArgument(parameters[index], hostContext);
        }

        return invocationArguments;
    }

    /// <summary>
    /// 运行时插件元数据。
    /// </summary>
    /// <param name="Id">插件唯一标识。</param>
    /// <param name="Name">插件名称。</param>
    /// <param name="Description">插件描述。</param>
    /// <param name="Type">插件运行时类型。</param>
    /// <param name="Controls">插件控制命令集合。</param>
    /// <param name="Actions">插件动作集合。</param>
    private sealed record RuntimeBrowserPlugin(
        string Id,
        string Name,
        string? Description,
        Type Type,
        IReadOnlyList<RuntimeBrowserPluginControl> Controls,
        IReadOnlyList<RuntimeBrowserPluginAction> Actions);

    /// <summary>
    /// 插件发现快照，包含插件集合和错误集合。
    /// </summary>
    /// <param name="Plugins">发现到的插件集合。</param>
    /// <param name="Errors">扫描过程中的错误集合。</param>
    private sealed record PluginDiscoverySnapshot(IReadOnlyList<RuntimeBrowserPlugin> Plugins, IReadOnlyList<string> Errors, IReadOnlyList<BrowserPluginErrorDescriptor> ErrorsDetailed);

    /// <summary>
    /// 插件控制命令元数据。
    /// </summary>
    /// <param name="Command">命令标识。</param>
    /// <param name="Name">命令名称。</param>
    /// <param name="Description">命令描述。</param>
    /// <param name="Method">对应的方法信息。</param>
    /// <param name="Parameters">命令参数集合。</param>
    private sealed record RuntimeBrowserPluginControl(
        string Command,
        string Name,
        string? Description,
        MethodInfo Method,
        IReadOnlyList<RuntimeBrowserPluginActionParameter> Parameters);

    /// <summary>
    /// 插件动作元数据。
    /// </summary>
    /// <param name="Id">动作唯一标识。</param>
    /// <param name="Name">动作名称。</param>
    /// <param name="Description">动作描述。</param>
    /// <param name="Method">对应的方法信息。</param>
    /// <param name="Parameters">动作参数集合。</param>
    private sealed record RuntimeBrowserPluginAction(
        string Id,
        string Name,
        string? Description,
        MethodInfo Method,
        IReadOnlyList<RuntimeBrowserPluginActionParameter> Parameters);

    /// <summary>
    /// 插件动作参数元数据。
    /// </summary>
    /// <param name="Name">参数名称。</param>
    /// <param name="Label">参数显示名称。</param>
    /// <param name="Description">参数描述。</param>
    /// <param name="DefaultValue">参数默认值。</param>
    /// <param name="Required">是否必填。</param>
    /// <param name="InputType">输入类型。</param>
    /// <param name="Options">参数选项集合。</param>
    private sealed record RuntimeBrowserPluginActionParameter(
        string Name,
        string Label,
        string? Description,
        string? DefaultValue,
        bool Required,
        string InputType,
        IReadOnlyList<RuntimeBrowserPluginActionParameterOption> Options);

    /// <summary>
    /// 插件动作参数选项元数据。
    /// </summary>
    /// <param name="Value">选项值。</param>
    /// <param name="Label">选项显示名称。</param>
    private sealed record RuntimeBrowserPluginActionParameterOption(string Value, string Label);

    /// <summary>
    /// 插件运行时实例及其类型和并发锁。
    /// </summary>
    /// <param name="type">插件类型。</param>
    /// <param name="instance">插件实例。</param>
    private sealed class RuntimeBrowserPluginInstance(Type type, IPlugin instance)
    {
        private readonly object _lifecycleLock = new();
        private CancellationTokenSource _lifecycleCts = new();

        /// <summary>
        /// 插件类型。
        /// </summary>
        public Type Type { get; } = type;
        /// <summary>
        /// 插件实例。
        /// </summary>
        public IPlugin Instance { get; } = instance;
        /// <summary>
        /// 执行锁，确保同一时间只有一个操作在执行插件实例的方法，避免并发访问导致的状态不一致或竞态条件。
        /// </summary>
        public SemaphoreSlim ExecutionLock { get; } = new(1, 1);

        /// <summary>
        /// 获取当前用于插件生命周期的取消令牌。
        /// 该令牌在插件被 Stop 时会被触发，插件应通过 HostContext 提供的 CancellationToken 响应取消。
        /// </summary>
        public CancellationToken LifecycleCancellationToken => _lifecycleCts.Token;

        /// <summary>
        /// 确保如果当前生命周期令牌已被取消，则替换为新的未取消的令牌源（用于再次 Start）。
        /// </summary>
        public void EnsureFreshLifecycleToken()
        {
            lock (_lifecycleLock)
            {
                if (_lifecycleCts.IsCancellationRequested)
                {
                    try { _lifecycleCts.Dispose(); } catch { }
                    _lifecycleCts = new CancellationTokenSource();
                }
            }
        }

        /// <summary>
        /// 取消当前生命周期令牌，通知插件应停止其后台工作。
        /// </summary>
        public void CancelLifecycle()
        {
            lock (_lifecycleLock)
            {
                try { _lifecycleCts.Cancel(); } catch { }
            }
        }
    }


    /// <summary>
    /// 插件自定义 AssemblyLoadContext，用于隔离插件依赖。
    /// </summary>
    /// <param name="pluginAssemblyPath">插件程序集路径。</param>
    private sealed class PluginLoadContext(string pluginAssemblyPath) : AssemblyLoadContext($"BrowserPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: false)
    {
        /// <summary>
        /// .NET Core 提供的程序集依赖解析器，基于插件程序集路径自动解析依赖项位置。
        /// </summary>
        private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

        /// <summary>
        /// .NET 运行时加载程序集时会在插件目录及其子目录中查找依赖项，避免因插件依赖未找到而导致的加载失败。
        /// </summary>
        private readonly string _pluginDirectoryPath = Path.GetDirectoryName(pluginAssemblyPath) ?? AppContext.BaseDirectory;

        /// <summary>
        /// 加载程序集
        /// </summary>
        /// <param name="assemblyName">正在加载的程序集的名称。</param>
        /// <returns>要加载的程序集对象。</returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var sharedAssembly = Default.Assemblies.FirstOrDefault(current => AssemblyName.ReferenceMatchesDefinition(current.GetName(), assemblyName));
            if (sharedAssembly is not null)
                return sharedAssembly;

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                return LoadFromAssemblyPath(resolvedPath);

            var directPath = Path.Combine(_pluginDirectoryPath, $"{assemblyName.Name}.dll");
            if (File.Exists(directPath))
                return LoadFromAssemblyPath(directPath);

            // Fallback: try application base directory so plugins that depend on shared
            // libraries (copied to app output) can resolve them.
            var baseDirPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(baseDirPath))
                return LoadFromAssemblyPath(baseDirPath);

            return null;
        }

        /// <summary>
        /// 加载未管理的 DLL。
        /// </summary>
        /// <param name="unmanagedDllName">未管理的 DLL 名称。</param>
        /// <returns>未管理的 DLL 的句柄。</returns>
        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var resolvedPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                return LoadUnmanagedDllFromPath(resolvedPath);

            foreach (var candidatePath in EnumerateNativeLibraryCandidates(unmanagedDllName))
            {
                if (File.Exists(candidatePath))
                    return LoadUnmanagedDllFromPath(candidatePath);
            }

            // Fallback: try application base directory native candidates
            foreach (var candidate in new[] { Path.Combine(AppContext.BaseDirectory, GetNativeLibraryFileName(unmanagedDllName)), Path.Combine(AppContext.BaseDirectory, unmanagedDllName) })
            {
                if (File.Exists(candidate))
                    return LoadUnmanagedDllFromPath(candidate);
            }

            return nint.Zero;
        }

        /// <summary>
        /// 枚举可能的本地库候选路径。
        /// </summary>
        /// <param name="unmanagedDllName">未管理的 DLL 名称。</param>
        /// <returns>可能的本地库路径集合。</returns>
        private IEnumerable<string> EnumerateNativeLibraryCandidates(string unmanagedDllName)
        {
            var libraryFileName = GetNativeLibraryFileName(unmanagedDllName);
            yield return Path.Combine(_pluginDirectoryPath, libraryFileName);
            yield return Path.Combine(_pluginDirectoryPath, unmanagedDllName);

            var runtimesDirectoryPath = Path.Combine(_pluginDirectoryPath, "runtimes");
            if (!Directory.Exists(runtimesDirectoryPath))
                yield break;

            foreach (var runtimeDirectoryPath in Directory.EnumerateDirectories(runtimesDirectoryPath))
            {
                var nativeDirectoryPath = Path.Combine(runtimeDirectoryPath, "native");
                if (!Directory.Exists(nativeDirectoryPath))
                    continue;

                yield return Path.Combine(nativeDirectoryPath, libraryFileName);
                yield return Path.Combine(nativeDirectoryPath, unmanagedDllName);
            }
        }

        /// <summary>
        /// .NET 运行时对不同平台的本地库有不同的命名约定，该方法根据当前平台生成可能的库文件名，以增加加载成功的概率。
        /// </summary>
        /// <param name="unmanagedDllName">未管理的 DLL 名称。</param>
        /// <returns>可能的库文件名。</returns>
        private static string GetNativeLibraryFileName(string unmanagedDllName)
        {
            if (Path.HasExtension(unmanagedDllName))
                return unmanagedDllName;

            if (OperatingSystem.IsWindows())
                return $"{unmanagedDllName}.dll";

            if (OperatingSystem.IsMacOS())
                return $"lib{unmanagedDllName}.dylib";

            return $"lib{unmanagedDllName}.so";
        }
    }

    /// <summary>
    /// 创建插件动作参数元数据。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <param name="parameterMetadata">参数特性。</param>
    /// <param name="legacyMetadata">兼容旧特性的参数元数据。</param>
    /// <returns>动作参数元数据。</returns>
    private static RuntimeBrowserPluginActionParameter CreateActionParameter(ParameterInfo parameter, PInputAttribute? parameterMetadata, PInputAttribute? legacyMetadata)
    {
        var defaultValue = GetDefaultValue(parameter);
        var label = !string.IsNullOrWhiteSpace(parameterMetadata?.Label)
            ? parameterMetadata.Label
            : !string.IsNullOrWhiteSpace(legacyMetadata?.Label)
                ? legacyMetadata.Label
                : parameter.Name ?? string.Empty;
        var description = parameterMetadata?.Description ?? legacyMetadata?.Description;

        return new RuntimeBrowserPluginActionParameter(
            parameter.Name ?? string.Empty,
            label,
            description,
            defaultValue,
            IsRequiredParameter(parameter),
            GetInputType(parameter),
            GetOptions(parameter));
    }

    /// <summary>
    /// 向插件控制命令集合添加指定控制命令。
    /// </summary>
    /// <param name="type">插件类型。</param>
    /// <param name="controls">控制命令集合。</param>
    /// <param name="command">命令标识。</param>
    /// <param name="name">命令名称。</param>
    /// <param name="description">命令描述。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="parameterTypes">参数类型数组。</param>
    private static void AddControl(Type type, ICollection<RuntimeBrowserPluginControl> controls, string command, string name, string description, string methodName, params Type[] parameterTypes)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
        if (method is null)
            return;

        controls.Add(new RuntimeBrowserPluginControl(
            command,
            name,
            description,
            method,
            CreateMethodInputParameters(method)));
    }

    /// <summary>
    /// 创建方法的所有输入参数元数据集合。
    /// </summary>
    /// <param name="method">方法信息。</param>
    /// <returns>参数元数据集合。</returns>
    private static IReadOnlyList<RuntimeBrowserPluginActionParameter> CreateMethodInputParameters(MethodInfo method)
        => [.. method
            .GetCustomAttributes<PInputAttribute>()
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Name) || !string.IsNullOrWhiteSpace(attribute.Label))
            .Select(attribute => new RuntimeBrowserPluginActionParameter(
                attribute.Name?.Trim() ?? attribute.Label.Trim(),
                attribute.Label.Trim(),
                attribute.Description,
                attribute.DefaultValue,
                attribute.Required,
                NormalizeInputType(attribute.InputType),
                []))];

    /// <summary>
    /// 规范化参数输入类型。
    /// </summary>
    /// <param name="inputType">输入类型字符串。</param>
    /// <returns>规范化后的输入类型。</returns>
    private static string NormalizeInputType(string? inputType)
    {
        if (string.IsNullOrWhiteSpace(inputType))
            return "text";

        return inputType.Trim().ToLowerInvariant() switch
        {
            "checkbox" => "checkbox",
            "number" => "number",
            "datetime-local" => "datetime-local",
            "guid" => "guid",
            _ => "text"
        };
    }

    /// <summary>
    /// 确保插件动作 ID 在集合中唯一。
    /// </summary>
    /// <param name="baseId">基础 ID。</param>
    /// <param name="usedIds">已用 ID 集合。</param>
    /// <returns>唯一 ID。</returns>
    private static string EnsureUniqueActionId(string baseId, HashSet<string> usedIds)
    {
        var candidate = baseId;
        var suffix = 1;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }

        return candidate;
    }

    /// <summary>
    /// 判断参数是否为宿主上下文相关类型。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>如果是宿主参数返回 true，否则返回 false。</returns>
    private static bool IsHostParameter(ParameterInfo parameter)
        => parameter.ParameterType == typeof(IBrowserContext)
            || parameter.ParameterType == typeof(IPage)
            || parameter.ParameterType == typeof(CancellationToken)
            || parameter.ParameterType == typeof(IHostContext)
            || parameter.ParameterType == typeof(IReadOnlyDictionary<string, string?>);

    /// <summary>
    /// 判断参数类型是否为可绑定类型。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>如果是可绑定类型返回 true，否则返回 false。</returns>
    private static bool IsBindableParameter(ParameterInfo parameter)
    {
        var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        return parameterType == typeof(string)
            || parameterType == typeof(bool)
            || parameterType == typeof(byte)
            || parameterType == typeof(short)
            || parameterType == typeof(int)
            || parameterType == typeof(long)
            || parameterType == typeof(float)
            || parameterType == typeof(double)
            || parameterType == typeof(decimal)
            || parameterType == typeof(Guid)
            || parameterType == typeof(DateTime)
            || parameterType == typeof(DateTimeOffset)
            || parameterType == typeof(TimeSpan)
            || parameterType.IsEnum;
    }

    /// <summary>
    /// 解析方法参数的实际调用值。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <param name="hostContext">宿主上下文。</param>
    /// <returns>参数值。</returns>
    private static object? ResolveInvocationArgument(ParameterInfo parameter, IHostContext hostContext)
    {
        if (parameter.ParameterType == typeof(IBrowserContext))
            return hostContext.BrowserContext;

        if (parameter.ParameterType == typeof(IPage))
            return hostContext.ActivePage;

        if (parameter.ParameterType == typeof(CancellationToken))
            return hostContext.CancellationToken;

        if (parameter.ParameterType == typeof(IHostContext))
            return hostContext;

        if (parameter.ParameterType == typeof(IReadOnlyDictionary<string, string?>))
            return hostContext.Arguments;

        return BindUserArgument(parameter, hostContext.Arguments);
    }

    /// <summary>
    /// 从参数字典绑定用户输入参数。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <param name="arguments">参数字典。</param>
    /// <returns>参数值。</returns>
    private static object? BindUserArgument(ParameterInfo parameter, IReadOnlyDictionary<string, string?> arguments)
    {
        var parameterName = parameter.Name ?? throw new InvalidOperationException("插件动作参数缺少名称。");

        if (!arguments.TryGetValue(parameterName, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            if (parameter.HasDefaultValue)
                return parameter.DefaultValue;

            if (IsNullableParameter(parameter))
                return null;

            throw new InvalidOperationException($"缺少参数：{parameterName}");
        }

        try
        {
            return ConvertArgumentValue(parameter.ParameterType, rawValue.Trim());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"参数 {parameterName} 的值无效：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 将字符串参数值转换为指定类型。
    /// </summary>
    /// <param name="parameterType">目标类型。</param>
    /// <param name="rawValue">原始字符串值。</param>
    /// <returns>转换后的值。</returns>
    private static object? ConvertArgumentValue(Type parameterType, string rawValue)
    {
        var targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

        if (targetType == typeof(string))
            return rawValue;

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(rawValue, out var booleanValue))
                return booleanValue;

            if (string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(rawValue, "on", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(rawValue, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(rawValue, "off", StringComparison.OrdinalIgnoreCase))
                return false;

            throw new FormatException("布尔值必须是 true/false。");
        }

        if (targetType.IsEnum)
            return Enum.Parse(targetType, rawValue, ignoreCase: true);

        if (targetType == typeof(Guid))
            return Guid.Parse(rawValue);

        if (targetType == typeof(DateTime))
            return DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

        if (targetType == typeof(DateTimeOffset))
            return DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);

        if (targetType == typeof(TimeSpan))
            return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);

        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
            return converter.ConvertFromInvariantString(rawValue);

        throw new InvalidOperationException($"不支持的参数类型：{targetType.Name}");
    }

    /// <summary>
    /// 判断参数是否为必填参数。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>是否必填。</returns>
    private static bool IsRequiredParameter(ParameterInfo parameter)
        => !parameter.HasDefaultValue && !IsNullableParameter(parameter);

    /// <summary>
    /// 判断参数是否为可空类型。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>是否可空。</returns>
    private static bool IsNullableParameter(ParameterInfo parameter)
    {
        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            return true;

        if (parameter.ParameterType.IsValueType)
            return false;

        return NullabilityContext.Create(parameter).ReadState != NullabilityState.NotNull;
    }

    /// <summary>
    /// 获取参数的默认值字符串。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>默认值字符串。</returns>
    private static string? GetDefaultValue(ParameterInfo parameter)
    {
        if (!parameter.HasDefaultValue)
            return null;

        return parameter.DefaultValue switch
        {
            null => null,
            bool value => value ? "true" : "false",
            DateTime value => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset value => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            Guid value => value.ToString("D", CultureInfo.InvariantCulture),
            IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
            _ => parameter.DefaultValue.ToString()
        };
    }

    /// <summary>
    /// 获取参数的输入类型（用于前端渲染）。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>输入类型字符串。</returns>
    private static string GetInputType(ParameterInfo parameter)
    {
        var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (parameterType == typeof(bool))
            return "checkbox";

        if (parameterType == typeof(DateTime) || parameterType == typeof(DateTimeOffset))
            return "datetime-local";

        if (parameterType == typeof(Guid))
            return "guid";

        if (parameterType.IsEnum)
            return "select";

        if (parameterType == typeof(byte)
            || parameterType == typeof(short)
            || parameterType == typeof(int)
            || parameterType == typeof(long)
            || parameterType == typeof(float)
            || parameterType == typeof(double)
            || parameterType == typeof(decimal))
            return "number";

        return "text";
    }

    /// <summary>
    /// 获取枚举类型参数的所有选项。
    /// </summary>
    /// <param name="parameter">参数信息。</param>
    /// <returns>参数选项集合。</returns>
    private static IReadOnlyList<RuntimeBrowserPluginActionParameterOption> GetOptions(ParameterInfo parameter)
    {
        var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (!parameterType.IsEnum)
            return [];

        return [.. Enum
            .GetNames(parameterType)
            .Select(name => new RuntimeBrowserPluginActionParameterOption(name, GetEnumOptionLabel(parameterType, name)))];
    }

    /// <summary>
    /// 获取枚举成员的显示标签。
    /// </summary>
    /// <param name="enumType">枚举类型。</param>
    /// <param name="memberName">成员名。</param>
    /// <returns>显示标签。</returns>
    private static string GetEnumOptionLabel(Type enumType, string memberName)
    {
        var field = enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
        if (field is null)
            return memberName;

        var displayName = field.GetCustomAttribute<DisplayAttribute>()?.GetName();
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName!;

        var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return string.IsNullOrWhiteSpace(description) ? memberName : description;
    }
}
