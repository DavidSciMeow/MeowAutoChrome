using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using MeowAutoChrome.Core.Services.PluginHost;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Responsible for loading plugin assemblies into isolated PluginLoadContext instances
/// and caching the loaded assemblies and their contexts. Extracted from BrowserPluginHostCore.
/// </summary>
public sealed class PluginAssemblyLoader : MeowAutoChrome.Core.Interface.ICorePluginAssemblyLoader, IPluginAssemblyLoader
{
    private readonly Dictionary<string, Assembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginLoadContext> _loadContexts = new(StringComparer.OrdinalIgnoreCase);
    // map plugin id -> assembly path for quick lookup when unloading
    private readonly Dictionary<string, string> _pluginToPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<PluginAssemblyLoader> _logger;

    public PluginAssemblyLoader(ILogger<PluginAssemblyLoader> logger)
    {
        _logger = logger;
    }

    public Assembly? Load(string pluginPath, List<string> errors)
    {
        try
        {
            var fullPath = Path.GetFullPath(pluginPath);

            lock (_assemblies)
            {
                if (_assemblies.TryGetValue(fullPath, out var loaded))
                    return loaded;
            }

            var loadContext = new PluginLoadContext(fullPath);
            var assembly = loadContext.LoadFromAssemblyPath(fullPath);

            lock (_assemblies)
            {
                _loadContexts[fullPath] = loadContext;
                _assemblies[fullPath] = assembly;
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

    public void Unload(string pluginPath)
    {
        var fullPath = Path.GetFullPath(pluginPath);
        if (_loadContexts.TryGetValue(fullPath, out var ctx))
        {
            try
            {
                ctx.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "卸载插件上下文失败：{Plugin}", pluginPath);
            }
        }
        _loadContexts.Remove(fullPath);
        _assemblies.Remove(fullPath);

        // remove any plugin id mappings for this path
        var keys = _pluginToPath.Where(kv => string.Equals(kv.Value, fullPath, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToArray();
        foreach (var k in keys)
            _pluginToPath.Remove(k);
    }

    public void RegisterPlugins(string pluginPath, IEnumerable<string> pluginIds)
    {
        var fullPath = Path.GetFullPath(pluginPath);
        foreach (var id in pluginIds)
        {
            _pluginToPath[id] = fullPath;
        }
    }

    public void UnregisterPlugins(string pluginPath)
    {
        var fullPath = Path.GetFullPath(pluginPath);
        var keys = _pluginToPath.Where(kv => string.Equals(kv.Value, fullPath, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToArray();
        foreach (var k in keys)
            _pluginToPath.Remove(k);
    }

    public string? GetAssemblyPathForPluginId(string pluginId)
        => _pluginToPath.TryGetValue(pluginId, out var path) ? path : null;
}
