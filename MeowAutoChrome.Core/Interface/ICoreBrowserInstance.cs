using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Interface;

/// <summary>
/// Core 层内部的浏览器实例抽象，避免依赖 Web 专用实现。
/// Core-internal abstraction for a browser instance to avoid depending on Web-specific implementations.
/// </summary>
public interface ICoreBrowserInstance
{
    /// <summary>
    /// 实例所属者标识。<br/>
    /// Identifier of the owner of the instance.
    /// </summary>
    string OwnerId { get; }
    /// <summary>
    /// 实例 ID。<br/>
    /// Instance id.
    /// </summary>
    string InstanceId { get; }
    /// <summary>
    /// 实例的显示名称。<br/>
    /// Display name of the instance.
    /// </summary>
    string DisplayName { get; }
    /// <summary>
    /// 是否以无头模式运行。<br/>
    /// Whether the instance is running headless.
    /// </summary>
    bool IsHeadless { get; }
    /// <summary>
    /// 用户数据目录路径。<br/>
    /// Path to the user data directory.
    /// </summary>
    string UserDataDirectoryPath { get; set; }
    /// <summary>
    /// 当前选中的页面 ID（可空）。<br/>
    /// Currently selected page id (nullable).
    /// </summary>
    string? SelectedPageId { get; set; }
    /// <summary>
    /// 此实例的页面集合。<br/>
    /// Read-only list of pages in this instance.
    /// </summary>
    IReadOnlyList<IPage> Pages { get; }
    /// <summary>
    /// 标签（选项卡）ID 列表。<br/>
    /// List of tab ids.
    /// </summary>
    IReadOnlyList<string> TabIds { get; }

    /// <summary>
    /// 由宿主持有的 Playwright 浏览器上下文（可能为 null）。<br/>
    /// The Playwright browser context held by the host (may be null).
    /// </summary>
    IBrowserContext? BrowserContext { get; }
    /// <summary>
    /// 获取当前选中的页面，若不存在则返回 null。<br/>
    /// Get the currently selected page, or null if none.
    /// </summary>
    IPage? GetSelectedPage();
    /// <summary>
    /// 根据页签 ID 获取页面，找不到则返回 null。<br/>
    /// Get a page by tab id, or null if not found.
    /// </summary>
    /// <param name="tabId">页签 ID（Tab identifier）。<br/>Tab identifier.</param>
    IPage? GetPageById(string tabId);

    // CDP / input helpers (define here to avoid depending on Core.Services namespace and
    // therefore break namespace mutual dependency between Core.Interface and Core.Services)
    /// <summary>
    /// 为指定页面创建 CDP 会话（可能失败返回 null）。<br/>
    /// Create a CDP session for the given page; may return null on failure.
    /// </summary>
    Task<ICDPSession?> CreateCdpSessionAsync(IPage page);
    /// <summary>
    /// 分发鼠标事件到 CDP 会话。<br/>
    /// Dispatch a mouse event to the CDP session.
    /// </summary>
    /// <param name="session">目标 CDP 会话 / target CDP session.</param>
    /// <param name="payload">事件载荷（通常为字典或对象）/ event payload (usually an object or dictionary).</param>
    Task DispatchMouseEventAsync(ICDPSession session, object payload);
    /// <summary>
    /// 分发键盘事件到 CDP 会话。<br/>
    /// Dispatch a key event to the CDP session.
    /// </summary>
    /// <param name="session">目标 CDP 会话 / target CDP session.</param>
    /// <param name="payload">事件载荷（通常为字典或对象）/ event payload (usually an object or dictionary).</param>
    Task DispatchKeyEventAsync(ICDPSession session, object payload);

    /// <summary>
    /// 初始化实例（提供用户数据路径和是否无头等选项）。<br/>
    /// Initialize the instance with user data directory and headless option.
    /// </summary>
    /// <param name="userDataDir">用户数据目录路径 / user data directory path.</param>
    /// <param name="headless">是否以无头模式运行 / whether to run headless.</param>
    /// <param name="userAgent">可选的 User-Agent 字符串 / optional user agent string.</param>
    /// <param name="browserExecutablePath">可选的浏览器可执行文件路径 / optional browser executable path.</param>
    Task InitializeAsync(string userDataDir, bool headless, string? userAgent = null, string? browserExecutablePath = null);
    /// <summary>
    /// 关闭实例并释放资源。<br/>
    /// Close the instance and release resources.
    /// </summary>
    Task CloseAsync();
    /// <summary>
    /// 创建新标签页并返回其 ID。<br/>
    /// Create a new tab and return its id.
    /// </summary>
    /// <param name="url">可选初始 URL / optional initial URL.</param>
    Task<string> CreateTabAsync(string? url = null);
    /// <summary>
    /// 关闭指定标签页，返回是否成功。<br/>
    /// Close the specified tab and return whether it succeeded.
    /// </summary>
    /// <param name="tabId">要关闭的标签页 ID / tab id to close.</param>
    Task<bool> CloseTabAsync(string tabId);
    /// <summary>
    /// 选择指定标签页作为当前页面。<br/>
    /// Select the specified tab as the current page.
    /// </summary>
    /// <param name="tabId">要选择的标签页 ID / tab id to select.</param>
    bool SelectPage(string tabId);
}
