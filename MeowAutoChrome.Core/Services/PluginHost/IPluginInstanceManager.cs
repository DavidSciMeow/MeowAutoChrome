using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 管理插件运行时实例的接口，提供获取、移除与生命周期控制方法。<br/>
/// Interface for managing runtime plugin instances: get/create, remove and control lifecycle.
/// </summary>
public interface IPluginInstanceManager
{
    /// <summary>
    /// 获取或创建指定插件的运行时实例。<br/>
    /// Get or create a runtime instance for the specified plugin.
    /// </summary>
    /// <param name="plugin">要获取或创建实例的运行时插件元数据。<br/>Runtime plugin metadata to get or create an instance for.</param>
    /// <returns>返回对应的 `RuntimeBrowserPluginInstance`。<br/>Returns the corresponding `RuntimeBrowserPluginInstance`.</returns>
    RuntimeBrowserPluginInstance GetOrCreateInstance(RuntimeBrowserPlugin plugin);
    /// <summary>
    /// 根据插件 ID 移除对应实例。<br/>
    /// Remove the instance associated with the given plugin id.
    /// </summary>
    /// <param name="plugin">要移除实例的插件 ID。<br/>Plugin id of the instance to remove.</param>
    void RemoveInstanceByPluginId(string plugin);
    /// <summary>
    /// 为插件确保新的生命周期取消令牌（丢弃旧的）。<br/>
    /// Ensure a fresh lifecycle cancellation token for the plugin.
    /// </summary>
    /// <param name="plugin">目标运行时插件。<br/>Target runtime plugin.</param>
    void EnsureFreshLifecycleToken(RuntimeBrowserPlugin plugin);
    /// <summary>
    /// 取消插件的生命周期以触发停止/清理。<br/>
    /// Cancel the plugin lifecycle to trigger stop/cleanup.
    /// </summary>
    /// <param name="plugin">目标运行时插件。<br/>Target runtime plugin.</param>
    void CancelLifecycle(RuntimeBrowserPlugin plugin);
}
