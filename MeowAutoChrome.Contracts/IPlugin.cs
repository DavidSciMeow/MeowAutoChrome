// Note: BrowserPlugin types have been migrated into MeowAutoChrome.Core.Models.
// The minimal plugin-facing surface (`IPlugin`, `PAResult`, `PluginState`) remains
// in Contracts. Avoid referencing removed Contracts sub-namespaces.
using System.Threading.Tasks;

namespace MeowAutoChrome.Contracts;

/// <summary>
/// 插件接口，定义插件的基本属性与生命周期操作。<br/>
/// Plugin interface defining plugin capabilities and lifecycle operations.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 启动插件并执行初始化逻辑。<br/>
    /// Start the plugin and perform initialization logic.
    /// </summary>
    /// <returns>异步操作，返回规范化的操作结果。<br/>Asynchronous operation returning the normalized operation result.</returns>
    Task<IResult> StartAsync();

    /// <summary>
    /// 停止插件并释放资源。<br/>
    /// Stop the plugin and release resources.
    /// </summary>
    /// <returns>异步操作，返回规范化的操作结果。<br/>Asynchronous operation returning the normalized operation result.</returns>
    Task<IResult> StopAsync();

    /// <summary>
    /// 将插件置于暂停状态（若支持）。<br/>
    /// Pause the plugin (if supported).
    /// </summary>
    /// <returns>异步操作，返回规范化的操作结果。<br/>Asynchronous operation returning the normalized operation result.</returns>
    Task<IResult> PauseAsync();

    /// <summary>
    /// 从暂停状态恢复插件运行（若支持）。<br/>
    /// Resume the plugin from paused state (if supported).
    /// </summary>
    /// <returns>异步操作，返回规范化的操作结果。<br/>Asynchronous operation returning the normalized operation result.</returns>
    Task<IResult> ResumeAsync();

    /// <summary>
    /// 是否支持暂停/恢复操作。宿主会根据该能力决定是否暴露暂停/恢复控制。<br/>
    /// Whether the plugin supports pause/resume operations. The host uses this capability to decide whether pause/resume controls should be exposed.
    /// </summary>
    bool SupportsPause { get; }

    /// <summary>
    /// 宿主提供的上下文。宿主在调用插件生命周期方法和动作前会设置该属性，调用后会清除。<br/>
    /// Host-provided context. The host sets this before invoking plugin lifecycle methods and actions, and clears it afterwards.
    /// </summary>
    IPluginContext? HostContext { get; set; }
}
