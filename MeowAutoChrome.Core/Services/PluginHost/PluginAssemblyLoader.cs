using System.Reflection;
using MeowAutoChrome.Core.Interface;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 负责将插件程序集加载到隔离的 <see cref="PluginLoadContext"/> 中并缓存已加载的程序集与上下文。<br/>
/// Responsible for loading plugin assemblies into isolated <see cref="PluginLoadContext"/> instances and caching loaded assemblies and contexts.
/// 从 BrowserPluginHostCore 中提取以降低该类型复杂度。<br/>
/// Extracted from BrowserPluginHostCore to reduce complexity.
/// </summary>
/// <remarks>
/// 构造函数：创建插件程序集加载器并注入日志记录器。<br/>
/// Constructor: creates the plugin assembly loader and injects a logger.
/// </remarks>
/// <param name="logger">日志记录器 / logger.</param>
public sealed class PluginAssemblyLoader(ILogger<PluginAssemblyLoader> logger) : IPluginAssemblyLoader
{
    private readonly Dictionary<string, Assembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginLoadContext> _loadContexts = new(StringComparer.OrdinalIgnoreCase);
    // map plugin id -> assembly path for quick lookup when unloading
    private readonly Dictionary<string, string> _pluginToPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 从给定路径加载程序集并缓存其装载上下文；发生错误时将错误信息追加到 <paramref name="errors"/> 并返回 null。<br/>
    /// Load an assembly from the specified path and cache its load context; on error append messages to <paramref name="errors"/> and return null.
    /// </summary>
    /// <param name="pluginPath">程序集文件路径 / assembly file path.</param>
    /// <param name="errors">用于收集加载错误的可变列表 / list to append error messages to.</param>
    /// <returns>已加载的 <see cref="Assembly"/> 或 null（加载失败时）。</returns>
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
            logger.LogError(ex, "加载插件程序集失败：{PluginAssembly}", pluginPath);
            errors.Add(message);
            return null;
        }
    }

    /// <summary>
    /// 卸载给定路径对应的插件上下文并清理缓存映射。<br/>
    /// Unload the plugin context associated with the given path and clean cached mappings.
    /// </summary>
    /// <param name="pluginPath">要卸载的程序集路径 / assembly path to unload.</param>
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
                logger.LogWarning(ex, "卸载插件上下文失败：{Plugin}", pluginPath);
            }
        }
        _loadContexts.Remove(fullPath);
        _assemblies.Remove(fullPath);

        // remove any plugin id mappings for this path
        var keys = _pluginToPath.Where(kv => string.Equals(kv.Value, fullPath, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToArray();
        foreach (var k in keys)
            _pluginToPath.Remove(k);
    }

    /// <summary>
    /// 注册在指定程序集路径中发现的插件 ID（用于反向查找）。<br/>
    /// Register plugin ids discovered in the specified assembly path for reverse lookup.
    /// </summary>
    /// <param name="pluginPath">程序集路径 / assembly path.</param>
    /// <param name="pluginIds">要注册的插件 ID 列表 / plugin ids to register.</param>
    public void RegisterPlugins(string pluginPath, IEnumerable<string> pluginIds)
    {
        var fullPath = Path.GetFullPath(pluginPath);
        foreach (var id in pluginIds) _pluginToPath[id] = fullPath;
    }

    /// <summary>
    /// 注销与指定程序集路径关联的插件 ID。<br/>
    /// Unregister plugin ids associated with the specified assembly path.
    /// </summary>
    /// <param name="pluginPath">程序集路径 / assembly path.</param>
    public void UnregisterPlugins(string pluginPath)
    {
        var fullPath = Path.GetFullPath(pluginPath);
        var keys = _pluginToPath.Where(kv => string.Equals(kv.Value, fullPath, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToArray();
        foreach (var k in keys) _pluginToPath.Remove(k);
    }

    /// <summary>
    /// 根据插件 ID 查找其对应的程序集路径（如果存在）。<br/>
    /// Get the assembly path for a plugin id, if present.
    /// </summary>
    /// <param name="pluginId">插件 ID / plugin id.</param>
    /// <returns>程序集路径或 null / assembly path or null.</returns>
    public string? GetAssemblyPathForPluginId(string pluginId) => _pluginToPath.TryGetValue(pluginId, out var path) ? path : null;
}
