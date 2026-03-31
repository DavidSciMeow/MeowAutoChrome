using MeowAutoChrome.Core.Models;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Plugin host 的 Core 层抽象，供核心组件调用以发现、加载和控制插件。<br/>
/// Core-level abstraction of the plugin host used by core components to discover, load and control plugins.
/// </summary>
public interface IPluginHostCore
{
    /// <summary>
    /// 插件根路径。<br/>
    /// Root path where plugins are located.
    /// </summary>
    string PluginRootPath { get; }
    /// <summary>
    /// 确保插件目录存在（如果不存在则创建）。<br/>
    /// Ensure the plugin directory exists (create if missing).
    /// </summary>
    void EnsurePluginDirectoryExists();
    /// <summary>
    /// 获取当前插件目录下的目录树/目录响应。<br/>
    /// Get the plugin catalog response.
    /// </summary>
    BrowserPluginCatalogResponse GetPluginCatalog();
    /// <summary>
    /// 列出已发现的插件描述符列表。<br/>
    /// Get the list of discovered plugin descriptors.
    /// </summary>
    IReadOnlyList<BrowserPluginDescriptor?> GetPlugins();
    /// <summary>
    /// 发送控制命令到插件（例如 start/stop/pause/resume）。<br/>
    /// Send a control command to a plugin (e.g., start/stop/pause/resume).
    /// </summary>
    /// <param name="pluginId">目标插件 ID / target plugin id.</param>
    /// <param name="command">控制命令（start/stop/pause/resume）/ control command (start/stop/pause/resume).</param>
    /// <param name="arguments">可选的键值参数 / optional key/value arguments.</param>
    /// <param name="connectionId">可选的连接 Id（用于发布回调）/ optional connection id for publishing callbacks.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task<BrowserPluginExecutionResponse?> ControlAsync(string pluginId, string command, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// 执行插件暴露的函数/动作并返回执行结果。<br/>
    /// Execute a plugin-exposed function/action and return the execution response.
    /// </summary>
    /// <param name="pluginId">目标插件 ID / target plugin id.</param>
    /// <param name="functionId">要执行的函数/动作 ID / function/action id to execute.</param>
    /// <param name="arguments">可选的键值参数 / optional key/value arguments.</param>
    /// <param name="connectionId">可选的连接 Id / optional connection id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task<BrowserPluginExecutionResponse?> ExecuteAsync(string pluginId, string functionId, IReadOnlyDictionary<string, string?>? arguments, string? connectionId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// 立即触发插件扫描并返回目录结果。<br/>
    /// Trigger an immediate plugin scan and return the catalog result.
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task<BrowserPluginCatalogResponse> ScanPluginsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// 从给定路径加载插件程序集并返回加载结果。<br/>
    /// Load a plugin assembly from the specified path and return the catalog response.
    /// </summary>
    /// <param name="pluginPath">插件程序集路径 / plugin assembly path.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task<BrowserPluginCatalogResponse> LoadPluginAssemblyAsync(string pluginPath, CancellationToken cancellationToken = default);
    /// <summary>
    /// 卸载指定插件并返回操作结果与错误列表。<br/>
    /// Unload the specified plugin and return success flag and error list.
    /// </summary>
    /// <param name="pluginId">要卸载的插件 ID / plugin id to unload.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task<(bool Success, IReadOnlyList<string> Errors)> UnloadPluginAsync(string pluginId, CancellationToken cancellationToken = default);
    /// <summary>
    /// 预览创建新浏览器实例（返回实例 ID 与用户数据目录）。<br/>
    /// Preview creating a new browser instance (returns instance id and user data directory).
    /// </summary>
    /// <param name="ownerId">实例所属者标识 / owner identifier.</param>
    /// <param name="root">可选的用户数据目录根路径 / optional user data directory root.</param>
    Task<(string InstanceId, string? UserDataDirectory)?> PreviewNewInstanceAsync(string ownerId, string? root = null);
    /// <summary>
    /// 关闭指定浏览器实例。<br/>
    /// Close the specified browser instance.
    /// </summary>
    /// <param name="instanceId">要关闭的实例 ID / instance id to close.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    Task<bool> CloseBrowserInstanceAsync(string instanceId, CancellationToken cancellationToken = default);
}
