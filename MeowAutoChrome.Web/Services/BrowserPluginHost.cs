using MeowAutoChrome.Contracts;
using MeowAutoChrome.Contracts.Attributes;
using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Warpper;
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

namespace MeowAutoChrome.Web.Services;

public sealed class BrowserPluginHost(BrowserInstanceManager browserInstances, IWebHostEnvironment environment, IHubContext<BrowserHub> browserHub, ILogger<BrowserPluginHost> logger)
{
    private static readonly string BrowserPluginAttributeFullName = typeof(BrowserPluginAttribute).FullName ?? nameof(BrowserPluginAttribute);
    private readonly string _pluginRootPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _pluginAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginLoadContext> _pluginLoadContexts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public string PluginRootPath => _pluginRootPath;

    public void EnsurePluginDirectoryExists()
        => Directory.CreateDirectory(_pluginRootPath);

    public BrowserPluginCatalogResponse GetPluginCatalog()
    {
        var discovery = DiscoverPluginsCore();
        var errors = new List<string>(discovery.Errors);
        var plugins = discovery.Plugins
            .Select(plugin => CreatePluginDescriptor(plugin, errors))
            .Where(plugin => plugin is not null)
            .ToArray()!;

        return new BrowserPluginCatalogResponse(plugins, errors);
    }

    public IReadOnlyList<BrowserPluginDescriptor> GetPlugins()
        => GetPluginCatalog().Plugins;

    public async Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default)
    {
        var plugins = DiscoverPlugins();

        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;

        var instance = GetOrCreatePluginInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        var hostContext = new BrowserPluginHostContext(
            browserInstances.BrowserContext,
            browserInstances.ActivePage,
            browserInstances.CurrentInstanceId,
            browserInstances,
            normalizedArguments,
            plugin.Id,
            command,
            cancellationToken,
            (message, data, openModal) => PublishPluginOutputAsync(plugin.Id, command, message, data, openModal, connectionId, cancellationToken));

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
            cancellationToken);

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
        var hostContext = new BrowserPluginHostContext(
            browserInstances.BrowserContext,
            browserInstances.ActivePage,
            browserInstances.CurrentInstanceId,
            browserInstances,
            normalizedArguments,
            plugin.Id,
            action.Id,
            cancellationToken,
            (message, data, openModal) => PublishPluginOutputAsync(plugin.Id, action.Id, message, data, openModal, connectionId, cancellationToken));

        var result = await ExecuteWithHostContextAsync(
            instance,
            hostContext,
            pluginInstance =>
            {
                var invocation = action.Method.Invoke(pluginInstance, BuildInvocationArguments(action.Method, hostContext));
                if (invocation is not Task<BrowserPluginActionResult> task)
                    throw new InvalidOperationException($"插件动作返回类型无效：{plugin.Type.FullName}.{action.Method.Name}");

                return task;
            },
            cancellationToken);

        return new BrowserPluginExecutionResponse(plugin.Id, action.Id, result.Message, instance.Instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
    }

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

    private IReadOnlyList<RuntimeBrowserPlugin> DiscoverPlugins()
        => DiscoverPluginsCore().Plugins;

    private PluginDiscoverySnapshot DiscoverPluginsCore()
    {
        EnsurePluginDirectoryExists();

        var plugins = new List<RuntimeBrowserPlugin>();
        var errors = new List<string>();

        foreach (var pluginPath in Directory.EnumerateFiles(_pluginRootPath, "*.dll", SearchOption.AllDirectories))
        {
            IReadOnlyList<string> candidateTypeNames;

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
                continue;
            }

            if (candidateTypeNames.Count == 0)
                continue;

            var assembly = TryLoadPluginAssembly(pluginPath, errors);
            if (assembly is null)
                continue;

            plugins.AddRange(DiscoverPlugins(assembly, pluginPath, candidateTypeNames, errors));
        }

        return new PluginDiscoverySnapshot(
            plugins
            .Where(plugin => plugin.Actions.Count > 0 || plugin.Controls.Count > 0)
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray(),
            errors.ToArray());
    }

    private BrowserPluginDescriptor? CreatePluginDescriptor(RuntimeBrowserPlugin plugin, ICollection<string> errors)
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
                plugin.Controls
                    .Where(control => instance.Instance.SupportsPause || (control.Command != "pause" && control.Command != "resume"))
                    .Select(control => new BrowserPluginControlDescriptor(
                        control.Command,
                        control.Name,
                        control.Description,
                        control.Parameters
                            .Select(parameter => new BrowserPluginActionParameterDescriptor(
                                parameter.Name,
                                parameter.Label,
                                parameter.Description,
                                parameter.DefaultValue,
                                parameter.Required,
                                parameter.InputType,
                                parameter.Options
                                    .Select(option => new BrowserPluginActionParameterOptionDescriptor(option.Value, option.Label))
                                    .ToArray()))
                            .ToArray()))
                    .ToArray(),
                plugin.Actions
                    .Select(action => new BrowserPluginFunctionDescriptor(
                        action.Id,
                        action.Name,
                        action.Description,
                        action.Parameters
                            .Select(parameter => new BrowserPluginActionParameterDescriptor(
                                parameter.Name,
                                parameter.Label,
                                parameter.Description,
                                parameter.DefaultValue,
                                parameter.Required,
                                parameter.InputType,
                                parameter.Options
                                    .Select(option => new BrowserPluginActionParameterOptionDescriptor(option.Value, option.Label))
                                    .ToArray()))
                            .ToArray()))
                    .ToArray());
        }
        catch (Exception ex)
        {
            var message = $"插件 {plugin.Type.FullName} 初始化失败：{ex.Message}";
            logger.LogError(ex, "插件 {PluginType} 初始化失败。", plugin.Type.FullName);
            errors.Add(message);
            return null;
        }
    }

    private RuntimeBrowserPluginInstance GetOrCreatePluginInstance(RuntimeBrowserPlugin plugin)
    {
        lock (_syncRoot)
        {
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current;

            var instance = Activator.CreateInstance(plugin.Type) as IBrowserPlugin;
            if (instance is null)
                throw new InvalidOperationException($"无法创建插件实例：{plugin.Type.FullName}");

            current = new RuntimeBrowserPluginInstance(plugin.Type, instance);
            _instances[plugin.Id] = current;
            return current;
        }
    }

    private static async Task<BrowserPluginActionResult> ExecuteWithHostContextAsync(RuntimeBrowserPluginInstance instance, IHostContext hostContext, Func<IBrowserPlugin, Task<BrowserPluginActionResult>> execute, CancellationToken cancellationToken)
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

    private IEnumerable<RuntimeBrowserPlugin> DiscoverPlugins(Assembly assembly, string pluginPath, IReadOnlyList<string> candidateTypeNames, ICollection<string> errors)
    {
        var plugins = new List<RuntimeBrowserPlugin>();

        foreach (var candidateTypeName in candidateTypeNames)
        {
            try
            {
                var type = assembly.GetType(candidateTypeName, throwOnError: false, ignoreCase: false);
                if (type is not { IsAbstract: false, IsInterface: false } || !typeof(IBrowserPlugin).IsAssignableFrom(type))
                    continue;

                var pluginAttribute = type.GetCustomAttribute<BrowserPluginAttribute>();
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

    private static IReadOnlyList<string> DiscoverPluginTypeNames(string pluginPath)
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

        return typeNames
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasCustomAttribute(MetadataReader metadataReader, CustomAttributeHandleCollection attributes, string attributeFullName)
        => attributes.Any(attributeHandle => string.Equals(GetAttributeTypeFullName(metadataReader, attributeHandle), attributeFullName, StringComparison.Ordinal));

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

    private static IReadOnlyList<RuntimeBrowserPluginControl> DiscoverControls(Type type)
    {
        var controls = new List<RuntimeBrowserPluginControl>();

        AddControl(type, controls, "start", "启动", "执行插件启动逻辑。", nameof(IBrowserPlugin.StartAsync));
        AddControl(type, controls, "stop", "停止", "执行插件停止逻辑。", nameof(IBrowserPlugin.StopAsync));
        AddControl(type, controls, "pause", "暂停", "执行插件暂停逻辑。", nameof(IBrowserPlugin.PauseAsync));
        AddControl(type, controls, "resume", "恢复", "执行插件恢复逻辑。", nameof(IBrowserPlugin.ResumeAsync));

        return controls;
    }

    private static IReadOnlyList<RuntimeBrowserPluginAction> DiscoverActions(Type type)
    {
        var actions = new List<RuntimeBrowserPluginAction>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = method.GetCustomAttribute<BrowserPluginActionAttribute>();
            if (attribute is null || !HasSupportedSignature(method))
                continue;

            var legacyParameterMetadata = method
                .GetCustomAttributes<BrowserPluginInputAttribute>()
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) || !string.IsNullOrWhiteSpace(item.Label))
                .GroupBy(item => item.Name ?? item.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            var parameters = method
                .GetParameters()
                .Where(parameter => !IsHostParameter(parameter))
                .Select(parameter => CreateActionParameter(
                    parameter,
                    parameter.GetCustomAttributes<BrowserPluginInputAttribute>().LastOrDefault(),
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

    private Assembly? TryLoadPluginAssembly(string pluginPath, ICollection<string> errors)
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

    private static string SummarizeLoaderExceptions(ReflectionTypeLoadException exception)
    {
        var messages = GetLoaderExceptionDescriptions(exception)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (messages is not { Length: > 0 })
            return exception.Message;

        return string.Join("； ", messages.Take(3));
    }

    private static IEnumerable<string> GetLoaderExceptionDescriptions(ReflectionTypeLoadException exception)
        => exception.LoaderExceptions
            ?.Where(loaderException => loaderException is not null)
            .Select(loaderException => DescribeException(loaderException!))
            .Where(message => !string.IsNullOrWhiteSpace(message))
        ?? [];

    private static string DescribeException(Exception exception)
        => exception switch
        {
            FileNotFoundException fileNotFoundException => DescribeFileNotFoundException(fileNotFoundException),
            FileLoadException fileLoadException => DescribeFileLoadException(fileLoadException),
            BadImageFormatException badImageFormatException => DescribeBadImageFormatException(badImageFormatException),
            TypeLoadException typeLoadException => DescribeTypeLoadException(typeLoadException),
            _ => exception.Message.Trim()
        };

    private static string DescribeFileNotFoundException(FileNotFoundException exception)
    {
        var assemblyName = GetAssemblyDisplayName(exception.FileName);
        return string.IsNullOrWhiteSpace(assemblyName)
            ? $"缺少依赖 DLL：{exception.Message.Trim()}"
            : $"缺少依赖 DLL：{assemblyName}";
    }

    private static string DescribeFileLoadException(FileLoadException exception)
    {
        var assemblyName = GetAssemblyDisplayName(exception.FileName);
        return string.IsNullOrWhiteSpace(assemblyName)
            ? $"程序集加载失败：{exception.Message.Trim()}"
            : $"程序集加载失败：{assemblyName}（可能是版本不匹配或文件被占用）";
    }

    private static string DescribeBadImageFormatException(BadImageFormatException exception)
    {
        var assemblyName = GetAssemblyDisplayName(exception.FileName);
        return string.IsNullOrWhiteSpace(assemblyName)
            ? $"程序集格式无效：{exception.Message.Trim()}"
            : $"程序集格式无效：{assemblyName}（可能是目标框架或位数不兼容）";
    }

    private static string DescribeTypeLoadException(TypeLoadException exception)
        => string.IsNullOrWhiteSpace(exception.TypeName)
            ? $"类型加载失败：{exception.Message.Trim()}"
            : $"类型加载失败：{exception.TypeName}";

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

    private static bool HasSupportedSignature(MethodInfo method)
    {
        if (method.ReturnType != typeof(Task<BrowserPluginActionResult>))
            return false;

        return method.GetParameters().All(parameter => IsHostParameter(parameter) || IsBindableParameter(parameter));
    }

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

    private sealed record RuntimeBrowserPlugin(
        string Id,
        string Name,
        string? Description,
        Type Type,
        IReadOnlyList<RuntimeBrowserPluginControl> Controls,
        IReadOnlyList<RuntimeBrowserPluginAction> Actions);

    private sealed record PluginDiscoverySnapshot(IReadOnlyList<RuntimeBrowserPlugin> Plugins, IReadOnlyList<string> Errors);

    private sealed record RuntimeBrowserPluginControl(
        string Command,
        string Name,
        string? Description,
        MethodInfo Method,
        IReadOnlyList<RuntimeBrowserPluginActionParameter> Parameters);

    private sealed record RuntimeBrowserPluginAction(
        string Id,
        string Name,
        string? Description,
        MethodInfo Method,
        IReadOnlyList<RuntimeBrowserPluginActionParameter> Parameters);

    private sealed record RuntimeBrowserPluginActionParameter(
        string Name,
        string Label,
        string? Description,
        string? DefaultValue,
        bool Required,
        string InputType,
        IReadOnlyList<RuntimeBrowserPluginActionParameterOption> Options);

    private sealed record RuntimeBrowserPluginActionParameterOption(string Value, string Label);

    private sealed class RuntimeBrowserPluginInstance(Type type, IBrowserPlugin instance)
    {
        public Type Type { get; } = type;
        public IBrowserPlugin Instance { get; } = instance;
        public SemaphoreSlim ExecutionLock { get; } = new(1, 1);
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _pluginDirectoryPath;

        public PluginLoadContext(string pluginAssemblyPath)
            : base($"BrowserPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
            _pluginDirectoryPath = Path.GetDirectoryName(pluginAssemblyPath) ?? AppContext.BaseDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var sharedAssembly = Default.Assemblies.FirstOrDefault(current => AssemblyName.ReferenceMatchesDefinition(current.GetName(), assemblyName));
            if (sharedAssembly is not null)
                return sharedAssembly;

            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                return LoadFromAssemblyPath(resolvedPath);

            var directPath = Path.Combine(_pluginDirectoryPath, $"{assemblyName.Name}.dll");
            return File.Exists(directPath)
                ? LoadFromAssemblyPath(directPath)
                : null;
        }

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

            return nint.Zero;
        }

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

    private static RuntimeBrowserPluginActionParameter CreateActionParameter(ParameterInfo parameter, BrowserPluginInputAttribute? parameterMetadata, BrowserPluginInputAttribute? legacyMetadata)
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

    private static IReadOnlyList<RuntimeBrowserPluginActionParameter> CreateMethodInputParameters(MethodInfo method)
        => method
            .GetCustomAttributes<BrowserPluginInputAttribute>()
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Name) || !string.IsNullOrWhiteSpace(attribute.Label))
            .Select(attribute => new RuntimeBrowserPluginActionParameter(
                attribute.Name?.Trim() ?? attribute.Label.Trim(),
                attribute.Label.Trim(),
                attribute.Description,
                attribute.DefaultValue,
                attribute.Required,
                NormalizeInputType(attribute.InputType),
                Array.Empty<RuntimeBrowserPluginActionParameterOption>()))
            .ToArray();

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

    private static bool IsHostParameter(ParameterInfo parameter)
        => parameter.ParameterType == typeof(IBrowserContext)
            || parameter.ParameterType == typeof(IPage)
            || parameter.ParameterType == typeof(CancellationToken)
            || parameter.ParameterType == typeof(IHostContext)
            || parameter.ParameterType == typeof(IReadOnlyDictionary<string, string?>);

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

    private static bool IsRequiredParameter(ParameterInfo parameter)
        => !parameter.HasDefaultValue && !IsNullableParameter(parameter);

    private static bool IsNullableParameter(ParameterInfo parameter)
    {
        if (Nullable.GetUnderlyingType(parameter.ParameterType) is not null)
            return true;

        if (parameter.ParameterType.IsValueType)
            return false;

        return NullabilityContext.Create(parameter).ReadState != NullabilityState.NotNull;
    }

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

    private static IReadOnlyList<RuntimeBrowserPluginActionParameterOption> GetOptions(ParameterInfo parameter)
    {
        var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        if (!parameterType.IsEnum)
            return Array.Empty<RuntimeBrowserPluginActionParameterOption>();

        return Enum
            .GetNames(parameterType)
            .Select(name => new RuntimeBrowserPluginActionParameterOption(name, GetEnumOptionLabel(parameterType, name)))
            .ToArray();
    }

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
