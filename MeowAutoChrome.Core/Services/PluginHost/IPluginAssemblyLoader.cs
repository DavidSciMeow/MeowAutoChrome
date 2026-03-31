using System.Reflection;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// 插件程序集加载器接口（宿主实现）。继承自 Core 层的 `ICorePluginAssemblyLoader`。
/// <br/>Plugin assembly loader interface (host implementation). Inherits from Core's `ICorePluginAssemblyLoader`.
/// </summary>
public interface IPluginAssemblyLoader : Interface.ICorePluginAssemblyLoader
{
    /// <summary>
    /// 从路径加载程序集并在错误列表中追加错误信息（失败则返回 null）。<br/>
    /// Load an assembly from the given path and append errors to the provided list (returns null on failure).
    /// </summary>
    /// <param name="pluginPath">插件程序集文件路径。<br/>Plugin assembly file path.</param>
    /// <param name="errors">用于追加加载错误信息的可变列表（如果发生错误则将错误消息添加到此列表）。<br/>A list to which loading error messages will be appended if failures occur.</param>
    /// <returns>加载成功时返回程序集，否则返回 null。<br/>Returns the loaded Assembly on success, or null on failure.</returns>
    Assembly? Load(string pluginPath, List<string> errors);
    /// <summary>
    /// 卸载指定路径的插件程序集。<br/>
    /// Unload the plugin assembly at the specified path.
    /// </summary>
    /// <param name="pluginPath">要卸载的插件程序集路径。<br/>Assembly path of the plugin to unload.</param>
    void Unload(string pluginPath);
    /// <summary>
    /// 在内部注册从程序集发现的插件 ID 列表。<br/>
    /// Register plugin ids discovered in the assembly path.
    /// </summary>
    /// <param name="pluginPath">程序集路径。<br/>Assembly path where plugins were discovered.</param>
    /// <param name="pluginIds">从程序集发现的插件 ID 列表。<br/>Enumerable of plugin ids discovered in the assembly.</param>
    void RegisterPlugins(string pluginPath, IEnumerable<string> pluginIds);
    /// <summary>
    /// 注销指定路径下注册的插件。<br/>
    /// Unregister plugins associated with the given assembly path.
    /// </summary>
    /// <param name="pluginPath">要注销的程序集路径。<br/>Assembly path whose registered plugins should be unregistered.</param>
    void UnregisterPlugins(string pluginPath);
    /// <summary>
    /// 根据插件 ID 获取对应的程序集路径（若有）。<br/>
    /// Get the assembly path for a given plugin id, if any.
    /// </summary>
    /// <param name="pluginId">要查找的插件 ID。<br/>Plugin id to find the assembly path for.</param>
    /// <returns>如果找到对应程序集则返回其路径，否则返回 null。<br/>Returns the assembly path if found, otherwise null.</returns>
    string? GetAssemblyPathForPluginId(string pluginId);
}
