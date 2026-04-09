using MeowAutoChrome.Core.Models;
using MeowAutoChrome.Contracts;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 管理运行时插件实例及其生命周期令牌的实现。<br/>
/// Manages runtime plugin instances and their lifecycle tokens.
/// 从 BrowserPluginHostCore 中提取以减少类型大小。<br/>
/// Extracted from BrowserPluginHostCore to reduce type size.
/// </summary>
public sealed class PluginInstanceManager : IPluginInstanceManager
{
    private readonly Dictionary<string, RuntimeBrowserPluginInstance> _instances = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取或创建指定插件的运行时实例。<br/>
    /// Get or create a runtime instance for the specified plugin.
    /// </summary>
    /// <param name="plugin">运行时插件描述 / runtime plugin descriptor.</param>
    /// <returns>插件运行时实例 / runtime plugin instance.</returns>
    public RuntimeBrowserPluginInstance GetOrCreateInstance(RuntimeBrowserPlugin plugin)
    {
        lock (_instances)
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
    /// 如果存在则返回指定插件的运行时实例，否则返回 null。<br/>
    /// Return the runtime instance for the specified plugin if it exists; otherwise null.
    /// </summary>
    /// <param name="plugin">运行时插件描述 / runtime plugin descriptor.</param>
    public RuntimeBrowserPluginInstance? GetInstanceIfExists(RuntimeBrowserPlugin plugin)
    {
        lock (_instances)
        {
            if (_instances.TryGetValue(plugin.Id, out var current) && current.Type == plugin.Type)
                return current;
            return null;
        }
    }

    /// <summary>
    /// 为指定插件确保一个新的生命周期取消令牌（如果实例存在）。<br/>
    /// Ensure a fresh lifecycle cancellation token for the specified plugin instance if it exists.
    /// </summary>
    /// <param name="plugin">运行时插件描述 / runtime plugin descriptor.</param>
    public void EnsureFreshLifecycleToken(RuntimeBrowserPlugin plugin)
    {
        var inst = GetInstanceIfExists(plugin);
        if (inst is null) return;
        inst.EnsureFreshLifecycleToken();
    }

    /// <summary>
    /// 取消指定插件的生命周期以触发停止和清理（如果实例存在）。<br/>
    /// Cancel the specified plugin's lifecycle to trigger stop/cleanup if the instance exists.
    /// </summary>
    /// <param name="plugin">运行时插件描述 / runtime plugin descriptor.</param>
    public void CancelLifecycle(RuntimeBrowserPlugin plugin)
    {
        var inst = GetInstanceIfExists(plugin);
        if (inst is null) return;
        inst.CancelLifecycle();
    }

    /// <summary>
    /// 从管理器中移除指定插件 Id 的实例（如果存在）。<br/>
    /// Remove the instance associated with the specified plugin id from the manager.
    /// </summary>
    /// <param name="pluginId">插件 Id / plugin id.</param>
    public void RemoveInstanceByPluginId(string pluginId)
    {
        lock (_instances)
        {
            _instances.Remove(pluginId);
        }
    }
}
