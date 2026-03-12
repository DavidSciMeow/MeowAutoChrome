using MeowAutoChrome.Contracts;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Warpper;
using System.Reflection;
using System.Runtime.Loader;

namespace MeowAutoChrome.Web.Services;

public sealed class BrowserPluginHost(PlayWrightWarpper browser, IWebHostEnvironment environment)
{
    private readonly string _pluginRootPath = Path.Combine(environment.ContentRootPath, "Plugins");
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

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
                    instance.State.ToString(),
                    instance.SupportsPause,
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
                                    parameter.Required))
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
        var context = new PlaywrightPluginContext(browser);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();

        var result = command.ToLowerInvariant() switch
        {
            "start" => await instance.StartAsync(normalizedArguments, context, cancellationToken),
            "stop" => await instance.StopAsync(context, cancellationToken),
            "pause" => await instance.PauseAsync(context, cancellationToken),
            "resume" => await instance.ResumeAsync(context, cancellationToken),
            _ => throw new InvalidOperationException($"不支持的插件控制命令：{command}")
        };

        return new BrowserPluginExecutionResponse(plugin.Id, command, result.Message, instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
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
        var context = new PlaywrightPluginContext(browser);
        var normalizedArguments = arguments ?? new Dictionary<string, string?>();
        var invocation = action.Method.Invoke(instance, BuildInvocationArguments(action.Method, context, normalizedArguments, cancellationToken));
        if (invocation is not Task<BrowserPluginActionResult> task)
            throw new InvalidOperationException($"插件动作返回类型无效：{plugin.Type.FullName}.{action.Method.Name}");

        var result = await task;
        return new BrowserPluginExecutionResponse(plugin.Id, action.Id, result.Message, instance.State.ToString(), result.Data ?? new Dictionary<string, string?>());
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

    private IBrowserPlugin GetOrCreatePluginInstance(RuntimeBrowserPlugin plugin)
    {
        lock (_syncRoot)
        {
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current.Instance;

            var instance = Activator.CreateInstance(plugin.Type) as IBrowserPlugin;
            if (instance is null)
                throw new InvalidOperationException($"无法创建插件实例：{plugin.Type.FullName}");

            _instances[plugin.Id] = new RuntimeBrowserPluginInstance(plugin.Type, instance);
            return instance;
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
                var actions = type
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Select(method => new
                    {
                        Method = method,
                        Attribute = method.GetCustomAttribute<BrowserPluginActionAttribute>(),
                        Parameters = method.GetCustomAttributes<BrowserPluginInputAttribute>().ToArray()
                    })
                    .Where(item => item.Attribute is not null && HasSupportedSignature(item.Method))
                    .Select(item => new RuntimeBrowserPluginAction(
                        item.Attribute!.Id,
                        item.Attribute.Name,
                        item.Attribute.Description,
                        item.Method,
                        item.Parameters
                            .Select(parameter => new RuntimeBrowserPluginActionParameter(
                                parameter.Name,
                                parameter.Label,
                                parameter.Description,
                                parameter.DefaultValue,
                                parameter.Required))
                            .ToArray()))
                    .ToArray();

                return new RuntimeBrowserPlugin(pluginAttribute.Id, pluginAttribute.Name, pluginAttribute.Description, type, actions);
            });

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

        var parameters = method.GetParameters();
        return parameters.Length == 2
            && parameters[0].ParameterType == typeof(IBrowserPluginContext)
            && parameters[1].ParameterType == typeof(CancellationToken)
            || parameters.Length == 3
            && parameters[0].ParameterType == typeof(IBrowserPluginContext)
            && parameters[1].ParameterType == typeof(IReadOnlyDictionary<string, string?>)
            && parameters[2].ParameterType == typeof(CancellationToken);
    }

    private static object?[] BuildInvocationArguments(
        MethodInfo method,
        IBrowserPluginContext context,
        IReadOnlyDictionary<string, string?> arguments,
        CancellationToken cancellationToken)
    {
        var parameters = method.GetParameters();

        return parameters.Length switch
        {
            2 => [context, cancellationToken],
            3 => [context, arguments, cancellationToken],
            _ => throw new InvalidOperationException($"不支持的插件动作签名：{method.DeclaringType?.FullName}.{method.Name}")
        };
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
        bool Required);

    private sealed record RuntimeBrowserPluginInstance(Type Type, IBrowserPlugin Instance);
}
