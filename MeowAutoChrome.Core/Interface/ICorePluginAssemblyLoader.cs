using System.Reflection;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// 插件程序集加载的 Core 层抽象，避免 PluginHost 与 PluginDiscovery 之间的命名空间相互依赖。<br/>
/// Core-facing abstraction for plugin assembly loading to avoid mutual namespace dependency between PluginHost and PluginDiscovery.
/// </summary>
public interface ICorePluginAssemblyLoader
{
    /// <summary>
    /// 从路径加载程序集并在错误列表中追加错误信息（失败则返回 null）。<br/>
    /// Load an assembly from the given path and append errors to the provided list (returns null on failure).
    /// </summary>
    /// <param name="pluginPath">程序集文件路径 / assembly file path.</param>
    /// <param name="errors">用于追加错误信息的可变列表 / list to append error messages to.</param>
    Assembly? Load(string pluginPath, List<string> errors);
    /// <summary>
    /// 卸载指定路径的插件程序集。<br/>
    /// Unload the plugin assembly at the specified path.
    /// </summary>
    /// <param name="pluginPath">要卸载的程序集路径 / assembly path to unload.</param>
    void Unload(string pluginPath);
    /// <summary>
    /// 在内部注册从程序集发现的插件 ID 列表。<br/>
    /// Register plugin ids discovered in the assembly path.
    /// </summary>
    /// <param name="pluginPath">程序集路径 / assembly path.</param>
    /// <param name="pluginIds">要注册的插件 ID 列表 / plugin ids to register.</param>
    void RegisterPlugins(string pluginPath, IEnumerable<string> pluginIds);
    /// <summary>
    /// 注销指定路径下注册的插件。<br/>
    /// Unregister plugins associated with the given assembly path.
    /// </summary>
    /// <param name="pluginPath">程序集路径 / assembly path.</param>
    void UnregisterPlugins(string pluginPath);
    /// <summary>
    /// 根据插件 ID 获取对应的程序集路径（若有）。<br/>
    /// Get the assembly path for a given plugin id, if any.
    /// </summary>
    /// <param name="pluginId">插件 ID / plugin id.</param>
    string? GetAssemblyPathForPluginId(string pluginId);
}
