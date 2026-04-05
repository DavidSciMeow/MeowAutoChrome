using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Services.PluginDiscovery;

/// <summary>
/// 插件发现服务：枚举插件程序集、执行元数据扫描并返回发现的插件清单与错误信息。<br/>
/// Plugin discovery service: enumerate plugin assemblies, scan metadata and return discovered plugins and errors.
/// </summary>
public interface IPluginDiscoveryService
{
    /// <summary>
    /// 插件根目录路径。<br/>
    /// Root path where plugin assemblies are located.
    /// </summary>
    string PluginRootPath { get; }

    /// <summary>
    /// 确保插件目录已存在（若不存在则创建）。<br/>
    /// Ensure the plugin directory exists (create if missing).
    /// </summary>
    void EnsurePluginDirectoryExists();

    /// <summary>
    /// 枚举所有插件程序集的路径。<br/>
    /// Enumerate filesystem paths to all plugin assemblies.
    /// </summary>
    IEnumerable<string> EnumeratePluginAssemblies();

    /// <summary>
    /// 扫描插件目录并尝试加载与解析所有插件，返回聚合结果快照。<br/>
    /// Discover and load plugins from the plugin directory and return an aggregated snapshot.
    /// </summary>
    /// <param name="assemblyLoader">用于加载程序集的装载器 / assembly loader used to load plugin assemblies.</param>
    PluginDiscoverySnapshot DiscoverAll(Interface.ICorePluginAssemblyLoader assemblyLoader);

    /// <summary>
    /// 从单个程序集路径中发现插件，返回插件列表与错误信息。<br/>
    /// Discover plugins from a single assembly path and return plugins along with error details.
    /// </summary>
    /// <param name="pluginPath">插件程序集的文件路径 / plugin assembly file path.</param>
    /// <param name="assemblyLoader">用于加载程序集的装载器 / assembly loader used to load the assembly.</param>
    /// <returns>包含插件、错误摘要与详细错误信息的三元组 / tuple containing plugins, error messages and detailed descriptors.</returns>
    (List<RuntimeBrowserPlugin> Plugins, List<string> Errors, List<BrowserPluginErrorDescriptor> ErrorsDetailed) DiscoverFromAssembly(string pluginPath, Interface.ICorePluginAssemblyLoader assemblyLoader);

    /// <summary>
    /// 在运行时更新插件根目录路径并确保存储的目录存在。<br/>
    /// Update the plugin root path at runtime and ensure the directory exists.
    /// </summary>
    /// <param name="path">新的插件根路径（支持多个路径以分号或竖线分隔）。</param>
    void SetPluginRootPath(string path);
}
