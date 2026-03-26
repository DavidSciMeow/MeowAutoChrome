using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace MeowAutoChrome.Contracts.BrowserPlugin;

/// <summary>
/// 插件宿主上下文，提供插件运行时所需的环境信息和操作接口
/// </summary>
/// <param name="browserContext">浏览器上下文</param>
/// <param name="activePage">当前活动页面</param>
/// <param name="browserInstanceId">浏览器实例ID</param>
/// <param name="browserInstanceManager">浏览器实例管理器</param>
/// <param name="arguments">插件启动参数</param>
/// <param name="pluginId">插件ID</param>
/// <param name="targetId">目标ID</param>
/// <param name="cancellationToken">取消令牌</param>
/// <param name="publishUpdate">插件向宿主发送更新的委托</param>
public sealed class PluginHostContext(PluginHostContextOptions options) : IHostContext
{
    private readonly Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? _publishUpdate = options.PublishUpdate;
    /// <summary>
    /// 浏览器上下文，提供对浏览器环境的访问和操作能力
    /// </summary>
    public IBrowserContext BrowserContext { get; } = options.BrowserContext;
    public IPage? ActivePage { get; } = options.ActivePage;
    public string BrowserInstanceId { get; } = options.BrowserInstanceId;
    public IBrowserInstanceManager BrowserInstanceManager { get; } = options.BrowserInstanceManager;
    public IReadOnlyDictionary<string, string?> Arguments { get; } = options.Arguments;
    public string PluginId { get; } = options.PluginId;
    public string TargetId { get; } = options.TargetId;
    public CancellationToken CancellationToken { get; } = options.CancellationToken;
    /// <summary>
    /// 推送更新到宿主，插件可以通过调用这个方法向宿主发送状态更新或操作结果，参数包括消息、数据和是否打开模态框，宿主可以根据这些信息更新UI或执行相应的操作，如果没有提供委托则调用此方法不会有任何效果
    /// </summary>
    /// <param name="message">信息</param>
    /// <param name="data">数据</param>
    /// <param name="openModal">是否打开模态框</param>
    /// <returns></returns>
    public Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true) => _publishUpdate?.Invoke(message, data, openModal) ?? Task.CompletedTask;
}
