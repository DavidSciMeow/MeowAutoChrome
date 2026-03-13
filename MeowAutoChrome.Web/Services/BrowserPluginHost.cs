using MeowAutoChrome.Contracts;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Warpper;
using Microsoft.Playwright;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;

namespace MeowAutoChrome.Web.Services;

public sealed class BrowserPluginHost(PlayWrightWarpper browser, IWebHostEnvironment environment)
{
    private readonly string _pluginRootPath = Path.Combine(environment.ContentRootPath, "Plugins");
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public string PluginRootPath => _pluginRootPath;

    public void EnsurePluginDirectoryExists()
        => Directory.CreateDirectory(_pluginRootPath);

    public IReadOnlyList<BrowserPluginDescriptor> GetPlugins()
        => DiscoverPlugins()
            .Select(plugin =>
            {
                var instance = GetOrCreatePluginInstance(plugin);

                return new BrowserPluginDescriptor(
                    plugin.Id,
                    plugin.Name,
                    plugin.Description,
                    instance.Instance.State.ToString(),
                    instance.Instance.SupportsPause,
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
            })
            .ToArray();

    public async Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, CancellationToken cancellationToken = default)
    {
        var plugins = DiscoverPlugins();

        var plugin = plugins.FirstOrDefault(item => string.Equals(item.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (plugin is null)
            return null;

        var instance = GetOrCreatePluginInstance(plugin);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        var hostContext = new BrowserPluginHostContext(browser.BrowserContext, browser.ActivePage, normalizedArguments, cancellationToken);

        var result = await ExecuteWithHostContextAsync(
            instance,
            hostContext,
            pluginInstance => command.ToLowerInvariant() switch
            {
                "start" => pluginInstance.StartAsync(normalizedArguments, hostContext.BrowserContext, hostContext.ActivePage, cancellationToken),
                "stop" => pluginInstance.StopAsync(hostContext.BrowserContext, hostContext.ActivePage, cancellationToken),
                "pause" => pluginInstance.PauseAsync(hostContext.BrowserContext, hostContext.ActivePage, cancellationToken),
                "resume" => pluginInstance.ResumeAsync(hostContext.BrowserContext, hostContext.ActivePage, cancellationToken),
                _ => throw new InvalidOperationException($"不支持的插件控制命令：{command}")
            },
            cancellationToken);

        return new BrowserPluginExecutionResponse(plugin.Id, command, result.Message, instance.Instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
    }

    public async Task<BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, CancellationToken cancellationToken = default)
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
        var hostContext = new BrowserPluginHostContext(browser.BrowserContext, browser.ActivePage, normalizedArguments, cancellationToken);

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

    private IReadOnlyList<RuntimeBrowserPlugin> DiscoverPlugins()
    {
        EnsurePluginDirectoryExists();

        return Directory
            .EnumerateFiles(_pluginRootPath, "*.dll", SearchOption.AllDirectories)
            .Select(TryLoadPluginAssembly)
            .Where(assembly => assembly is not null)
            .SelectMany(assembly => DiscoverPlugins(assembly!))
            .Where(plugin => plugin.Actions.Count > 0)
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
            if (instance.Instance is IHostContextAware aware)
                aware.HostContext = hostContext;

            try
            {
                return await execute(instance.Instance);
            }
            finally
            {
                if (instance.Instance is IHostContextAware resettable)
                    resettable.HostContext = null;
            }
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }

    private static IEnumerable<RuntimeBrowserPlugin> DiscoverPlugins(Assembly assembly)
        => assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                && typeof(IBrowserPlugin).IsAssignableFrom(type)
                && type.GetCustomAttribute<BrowserPluginAttribute>() is not null)
            .Select(type =>
            {
                var pluginAttribute = type.GetCustomAttribute<BrowserPluginAttribute>()!;
                var actions = DiscoverActions(type);

                return new RuntimeBrowserPlugin(pluginAttribute.Id, pluginAttribute.Name, pluginAttribute.Description, type, actions);
            });

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
                .Where(item => !string.IsNullOrWhiteSpace(item.Label))
                .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
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

    private static Assembly? TryLoadPluginAssembly(string pluginPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(pluginPath);
            var loaded = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly =>
                !string.IsNullOrWhiteSpace(assembly.Location)
                && string.Equals(Path.GetFullPath(assembly.Location), fullPath, StringComparison.OrdinalIgnoreCase));

            return loaded ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
        catch
        {
            return null;
        }
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
        IReadOnlyList<RuntimeBrowserPluginAction> Actions);

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
