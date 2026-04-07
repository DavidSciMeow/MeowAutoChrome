using Microsoft.Playwright;
using MeowAutoChrome.Contracts;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services.PluginHost;

/// <summary>
/// Core 管理的插件宿主上下文实现：用于在执行插件时为插件提供浏览器上下文、页面、参数与回调。<br/>
/// Core-owned implementation of the host context used when executing plugins; provides browser context, active page, arguments and callbacks.
/// 它实现 Contracts 的 IPluginContext 以兼容依赖 Contracts 的插件。<br/>
/// It implements the Contracts IPluginContext facade so plugins depending on Contracts continue to work.
/// </summary>
/// <remarks>
/// 构造一个新的宿主上下文实例。<br/>
/// Construct a new host context instance.
/// </remarks>
/// <param name="browserContext">Playwright 浏览器上下文 / Playwright browser context.</param>
/// <param name="activePage">当前激活页面（可空）/ currently active page (optional).</param>
/// <param name="browserInstanceId">浏览器实例 Id / browser instance identifier.</param>
/// <param name="arguments">传递给插件的参数字典 / arguments dictionary provided to the plugin.</param>
/// <param name="pluginId">插件 Id / plugin identifier.</param>
/// <param name="targetId">目标 Id（例如触发源）/ target id (e.g. trigger source).</param>
/// <param name="cancellationToken">插件生命周期的取消令牌 / cancellation token for plugin lifecycle.</param>
/// <param name="publishUpdate">用于发布插件更新的回调 / callback to publish plugin updates.</param>
/// <param name="requestNewInstance">用于请求新浏览器实例的回调（可空）/ optional callback to request a new browser instance.</param>
/// <param name="getInstanceInfo">用于查询实例信息的回调（可空）/ optional callback to get instance info.</param>
public sealed class PluginHostContextCore(IBrowserContext browserContext, IPage? activePage, string browserInstanceId, IReadOnlyDictionary<string, string?> arguments, string pluginId, string targetId, CancellationToken cancellationToken, Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? publishUpdate, Func<BrowserCreationOptions, CancellationToken, Task<string?>>? requestNewInstance = null, Func<string, CancellationToken, Task<PluginBrowserInstanceInfo?>>? getInstanceInfo = null, Func<LogLevel, string, string?, Task>? logCallback = null) : IPluginContext
{

    /// <summary>
    /// 当前的 Playwright 浏览器上下文。<br/>
    /// The active Playwright browser context.
    /// </summary>
    public IBrowserContext BrowserContext { get; } = browserContext;

    /// <summary>
    /// 当前激活的页面（如果有）。<br/>
    /// Currently active page, if any.
    /// </summary>
    public IPage? ActivePage { get; } = activePage;

    /// <summary>
    /// 浏览器实例 Id。<br/>
    /// Browser instance id.
    /// </summary>
    public string BrowserInstanceId { get; } = browserInstanceId;

    /// <summary>
    /// 传递给插件的只读参数字典。<br/>
    /// Read-only dictionary of arguments passed to the plugin.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Arguments { get; } = arguments ?? new Dictionary<string, string?>();

    /// <summary>
    /// 插件 Id。<br/>
    /// Plugin id.
    /// </summary>
    public string PluginId { get; } = pluginId;

    /// <summary>
    /// 目标 Id（例如触发源）。<br/>
    /// Target id (for example, trigger source).
    /// </summary>
    public string TargetId { get; } = targetId;

    /// <summary>
    /// 插件执行期间使用的取消令牌。<br/>
    /// Cancellation token used during plugin execution.
    /// </summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>
    /// 发布插件状态/进度更新。<br/>
    /// Publish a plugin update/status message.
    /// </summary>
    /// <param name="message">可选消息文本 / optional message text.</param>
    /// <param name="data">可选的键值数据 / optional key/value data.</param>
    /// <param name="openModal">是否以模态方式展示 / whether to open as modal.</param>
    /// <returns>任务 / task.</returns>
    public Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true)
        => publishUpdate?.Invoke(message, data, openModal) ?? Task.CompletedTask;

    /// <summary>
    /// 请求创建新的浏览器实例（如果宿主支持）。<br/>
    /// Request creation of a new browser instance if the host supports it.
    /// </summary>
    /// <param name="options">浏览器创建选项 / browser creation options.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>新实例 Id（如果创建成功）或 null / new instance id or null.</returns>
    public Task<string?> RequestNewBrowserInstanceAsync(BrowserCreationOptions options, CancellationToken cancellationToken = default)
        => requestNewInstance is null ? Task.FromResult<string?>(null) : requestNewInstance(options, cancellationToken);

    /// <summary>
    /// 获取指定实例的元信息（如果宿主提供该能力）。<br/>
    /// Get metadata for the specified instance if the host provides that capability.
    /// </summary>
    /// <param name="instanceId">实例 Id / instance id.</param>
    /// <param name="cancellationToken">取消令牌 / cancellation token.</param>
    /// <returns>实例信息或 null / instance info or null.</returns>
    public Task<PluginBrowserInstanceInfo?> GetBrowserInstanceInfoAsync(string instanceId, CancellationToken cancellationToken = default)
        => getInstanceInfo is null ? Task.FromResult<PluginBrowserInstanceInfo?>(null) : getInstanceInfo(instanceId, cancellationToken);

    /// <summary>
    /// 将日志写入宿主（接受字符串形式的级别并转换为 LogLevel）。
    /// </summary>
    public Task WriteLogAsync(string level, string message, string? category = null)
    {
        if (logCallback is null)
            return Task.CompletedTask;

        try
        {
            // 尝试按名称解析（区分大小写不敏感），回退到 Information
            if (Enum.TryParse<LogLevel>(level, true, out var parsed))
            {
                return logCallback(parsed, message, category ?? PluginId);
            }

            // 尝试按数字解析
            if (int.TryParse(level, out var num) && Enum.IsDefined(typeof(LogLevel), num))
            {
                return logCallback((LogLevel)num, message, category ?? PluginId);
            }
        }
        catch { }

        return logCallback(LogLevel.Information, message, category ?? PluginId);
    }
}
