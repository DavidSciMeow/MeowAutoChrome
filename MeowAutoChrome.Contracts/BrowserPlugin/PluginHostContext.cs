using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MeowAutoChrome.Contracts.Interface;
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
public sealed class PluginHostContext(
    IBrowserContext browserContext,
    IPage? activePage,
    string browserInstanceId,
    IBrowserInstanceManager browserInstanceManager,
    IReadOnlyDictionary<string, string?> arguments,
    string pluginId,
    string targetId,
    CancellationToken cancellationToken,
    Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? publishUpdate = null) : IHostContext
{
    /// <summary>
    /// 插件向宿主发送更新的委托，参数包括消息、数据和是否打开模态框
    /// </summary>
    private readonly Func<string?, IReadOnlyDictionary<string, string?>?, bool, Task>? _publishUpdate = publishUpdate;
    /// <summary>
    /// 浏览器上下文，提供对浏览器环境的访问和操作能力
    /// </summary>
    public IBrowserContext BrowserContext { get; } = browserContext;
    /// <summary>
    /// 当前活动页面，可能为null，如果插件需要操作页面但未提供活动页面，则应抛出异常
    /// </summary>
    public IPage? ActivePage { get; } = activePage;
    /// <summary>
    /// 浏览器实例ID，唯一标识当前插件所在的浏览器实例，插件可以通过这个ID与宿主进行交互或区分不同的浏览器实例
    /// </summary>
    public string BrowserInstanceId { get; } = browserInstanceId;
    /// <summary>
    /// 浏览器实例管理器，提供对浏览器实例的管理和操作能力，插件可以通过这个接口获取其他浏览器实例的信息或执行跨实例的操作
    /// </summary>
    public IBrowserInstanceManager BrowserInstanceManager { get; } = browserInstanceManager;
    /// <summary>
    /// 浏览器的插件启动参数，包含插件启动时传递的键值对参数，插件可以通过这些参数获取运行时所需的信息或配置，如果没有参数则为一个空字典
    /// </summary>
    public IReadOnlyDictionary<string, string?> Arguments { get; } = arguments;
    /// <summary>
    /// 插件ID，唯一标识当前插件，插件可以通过这个ID与宿主进行交互或区分不同的插件实例
    /// </summary>
    public string PluginId { get; } = pluginId;
    /// <summary>
    /// 目标ID，唯一标识当前插件的目标，插件可以通过这个ID与宿主进行交互或区分不同的目标实例
    /// </summary>
    public string TargetId { get; } = targetId;
    /// <summary>
    /// 取消令牌，插件可以通过这个令牌感知宿主的取消请求，并在适当的时候停止自己的操作或清理资源，以实现更好的用户体验和资源管理
    /// </summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;
    /// <summary>
    /// 推送更新到宿主，插件可以通过调用这个方法向宿主发送状态更新或操作结果，参数包括消息、数据和是否打开模态框，宿主可以根据这些信息更新UI或执行相应的操作，如果没有提供委托则调用此方法不会有任何效果
    /// </summary>
    /// <param name="message">信息</param>
    /// <param name="data">数据</param>
    /// <param name="openModal">是否打开模态框</param>
    /// <returns></returns>
    public Task PublishUpdateAsync(string? message, IReadOnlyDictionary<string, string?>? data = null, bool openModal = true) => _publishUpdate?.Invoke(message, data, openModal) ?? Task.CompletedTask;
}
