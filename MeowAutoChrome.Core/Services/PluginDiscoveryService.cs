using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts.BrowserPlugin;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

public sealed class PluginDiscoveryService : IPluginDiscoveryService
{
    private readonly string _pluginRootPath;

    public PluginDiscoveryService(string? pluginRootPath = null)
    {
        _pluginRootPath = pluginRootPath ?? Path.Combine(AppContext.BaseDirectory, "Plugins");
    }

    public string PluginRootPath => _pluginRootPath;

    public void EnsurePluginDirectoryExists() => Directory.CreateDirectory(_pluginRootPath);

    public IEnumerable<string> EnumeratePluginAssemblies() => Directory.EnumerateFiles(_pluginRootPath, "*.dll", SearchOption.AllDirectories);

    public PluginDiscoverySnapshot DiscoverAll(MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader assemblyLoader)
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

    public (List<RuntimeBrowserPlugin> Plugins, List<string> Errors, List<BrowserPluginErrorDescriptor> ErrorsDetailed) DiscoverFromAssembly(string pluginPath, MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader assemblyLoader)
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

    private List<RuntimeBrowserPlugin> DiscoverPluginsInAssembly(Assembly assembly, string pluginPath, IReadOnlyList<string> candidateTypeNames, List<string> errors)
    {
        var plugins = new List<RuntimeBrowserPlugin>();

        foreach (var candidateTypeName in candidateTypeNames)
        {
            try
            {
                var type = assembly.GetType(candidateTypeName, throwOnError: false, ignoreCase: false);
                if (type is not { IsAbstract: false, IsInterface: false } || !typeof(MeowAutoChrome.Contracts.IPlugin).IsAssignableFrom(type))
                    continue;

                var pluginAttribute = type.GetCustomAttribute<MeowAutoChrome.Contracts.Attributes.PluginAttribute>();
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
