using System.Reflection;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Core.Struct;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// 插件发现实现：在插件根目录中扫描程序集、加载集合并返回发现的插件及错误信息。<br/>
/// Implementation of plugin discovery which scans the plugin root directory, loads assemblies and returns discovered plugins and errors.
/// </summary>
/// <remarks>
/// 创建一个 PluginDiscoveryService 实例，可通过可选参数覆盖插件根路径。<br/>
/// Create a PluginDiscoveryService instance; plugin root path can be overridden via the optional constructor parameter.
/// </remarks>
/// <param name="pluginRootPath">可选的插件根目录路径 / optional plugin root path.</param>
public sealed class PluginDiscoveryService(string? pluginRootPath = null) : IPluginDiscoveryService
{
    private string _pluginRootPath = pluginRootPath ?? ProgramSettings.GetDefaultPluginDirectoryPath();

    /// <summary>
    /// 插件根目录路径。<br/>
    /// Plugin root path.
    /// </summary>
    public string PluginRootPath => _pluginRootPath;

    /// <summary>
    /// 确保插件目录已存在（若不存在则创建）。<br/>
    /// Ensure the plugin directory exists (create if missing).
    /// </summary>
    public void EnsurePluginDirectoryExists()
    {
        var separators = new[] { ';', '|' };
        var roots = _pluginRootPath.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());
        foreach (var root in roots)
        {
            try { if (!string.IsNullOrWhiteSpace(root)) Directory.CreateDirectory(root); } catch { }
        }
    }

    /// <summary>
    /// 在运行时设置插件根目录。<br/>
    /// Set the plugin root path at runtime. Supports multiple roots separated by ';' or '|'.
    /// </summary>
    /// <param name="path">新的插件根路径 / new plugin root path.</param>
    public void SetPluginRootPath(string path)
    {
        _pluginRootPath = string.IsNullOrWhiteSpace(path) ? ProgramSettings.GetDefaultPluginDirectoryPath() : path.Trim();
        try { EnsurePluginDirectoryExists(); } catch { }
    }

    /// <summary>
    /// 枚举插件根目录下所有的程序集文件路径（递归）。<br/>
    /// Enumerate all assembly file paths under the plugin root directory (recursive).
    /// </summary>
    public IEnumerable<string> EnumeratePluginAssemblies()
    {
        // Support multiple plugin root paths separated by ';' or '|' in the configured _pluginRootPath.
        var separators = new[] { ';', '|' };
        var roots = _pluginRootPath.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim());

        var files = new List<string>();
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            try
            {
                if (!Directory.Exists(root)) continue;
                files.AddRange(Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories));
            }
            catch
            {
                // ignore directories we cannot access and continue with others
                continue;
            }
        }

        return files;
    }

    /// <summary>
    /// 发现插件根目录下的所有插件并返回聚合快照。<br/>
    /// Discover all plugins under the plugin root and return an aggregated snapshot.
    /// </summary>
    /// <param name="assemblyLoader">用于加载程序集的装载器 / assembly loader.</param>
    /// <returns>表示发现结果的快照 / snapshot representing discovery results.</returns>
    public PluginDiscoverySnapshot DiscoverAll(Interface.IPluginAssemblyLoader assemblyLoader)
    {
        EnsurePluginDirectoryExists();

        var plugins = new List<RuntimeBrowserPlugin>();
        var errors = new List<string>();
        var errorsDetailed = new List<BrowserPluginErrorDescriptor>();

        foreach (var pluginPath in EnumeratePluginAssemblies())
        {
            try
            {
                var candidateTypeNames = PluginMetadataScanner.DiscoverPluginTypeNames(pluginPath);
                if (candidateTypeNames.Length == 0)
                    continue;

                var assembly = assemblyLoader.Load(pluginPath, errors);
                if (assembly is null)
                    continue;

                var discovered = DiscoverPluginsInAssembly(assembly, pluginPath, candidateTypeNames, errors);
                plugins.AddRange(discovered);

                assemblyLoader.RegisterPlugins(pluginPath, discovered.Select(p => p.Id));
            }
            catch (Exception ex)
            {
                var detail = ex.ToString();
                var message = $"插件程序集 {Path.GetFileName(pluginPath)} 元数据扫描失败：{detail}";
                errors.Add(message);
                errorsDetailed.Add(new BrowserPluginErrorDescriptor(Path.GetFileName(pluginPath), ex.Message, detail));
            }
        }

        return new PluginDiscoverySnapshot(
            [.. plugins
            .Where(plugin => plugin.Actions.Count > 0 || plugin.Controls.Count > 0)
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)],
            [.. errors],
            [.. errorsDetailed]);
    }

    /// <summary>
    /// 从单个程序集路径发现插件并返回插件与错误信息。<br/>
    /// Discover plugins from a single assembly path and return plugins and errors.
    /// </summary>
    /// <param name="pluginPath">程序集文件路径 / assembly file path.</param>
    /// <param name="assemblyLoader">用于加载程序集的装载器 / assembly loader.</param>
    /// <returns>包含插件、错误摘要与详细错误信息的三元组 / tuple containing plugins, error messages and detailed descriptors.</returns>
    public (List<RuntimeBrowserPlugin> Plugins, List<string> Errors, List<BrowserPluginErrorDescriptor> ErrorsDetailed) DiscoverFromAssembly(string pluginPath, Interface.IPluginAssemblyLoader assemblyLoader)
    {
        var plugins = new List<RuntimeBrowserPlugin>();
        var errors = new List<string>();
        var errorsDetailed = new List<BrowserPluginErrorDescriptor>();

        try
        {
            var candidateTypeNames = PluginMetadataScanner.DiscoverPluginTypeNames(pluginPath);
            if (candidateTypeNames.Length == 0)
                return (plugins, errors, errorsDetailed);

            var assembly = assemblyLoader.Load(pluginPath, errors);
            if (assembly is null)
                return (plugins, errors, errorsDetailed);

            var discovered = DiscoverPluginsInAssembly(assembly, pluginPath, candidateTypeNames, errors);
            plugins.AddRange(discovered);
            assemblyLoader.RegisterPlugins(pluginPath, discovered.Select(p => p.Id));
        }
        catch (Exception ex)
        {
            var detail = ex.ToString();
            var message = $"插件程序集 {Path.GetFileName(pluginPath)} 元数据扫描失败：{detail}";
            errors.Add(message);
            errorsDetailed.Add(new BrowserPluginErrorDescriptor(Path.GetFileName(pluginPath), ex.Message, detail));
        }

        return (plugins, errors, errorsDetailed);
    }

    private static List<RuntimeBrowserPlugin> DiscoverPluginsInAssembly(Assembly assembly, string pluginPath, IReadOnlyList<string> candidateTypeNames, List<string> errors)
    {
        var plugins = new List<RuntimeBrowserPlugin>();

        foreach (var candidateTypeName in candidateTypeNames)
        {
            try
            {
                var type = assembly.GetType(candidateTypeName, throwOnError: false, ignoreCase: false);
                if (type is not { IsAbstract: false, IsInterface: false } || !typeof(Contracts.IPlugin).IsAssignableFrom(type))
                    continue;

                var pluginAttribute = type.GetCustomAttribute<Contracts.Attributes.PluginAttribute>();
                if (pluginAttribute is null)
                    continue;

                // Use the helper in PluginDiscovery namespace for controls/actions
                plugins.Add(new RuntimeBrowserPlugin(
                    pluginAttribute.Id,
                    pluginAttribute.Name,
                    pluginAttribute.Description,
                    type,
                    PluginTypeIntrospector.DiscoverControls(type),
                    PluginTypeIntrospector.DiscoverActions(type)));
            }
            catch (Exception ex)
            {
                var detail = ex.ToString();
                var message = $"插件类型 {candidateTypeName} 发现失败：{detail}";
                errors.Add(message);
            }
        }

        return plugins;
    }
}
