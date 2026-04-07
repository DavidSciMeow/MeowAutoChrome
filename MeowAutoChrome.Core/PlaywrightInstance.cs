using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
// using MeowAutoChrome.Core.Services; // not required in this file
using System.Collections.Concurrent;
using MeowAutoChrome.Core.Interface;

namespace MeowAutoChrome.Core;
/// <summary>
/// Playwright 实例封装，负责管理浏览器上下文、标签页并提供 CDP、事件分发等功能。<br/>
/// Wrapper for a Playwright instance that manages browser context, pages, and provides CDP and event dispatching.
/// </summary>
/// <remarks>
/// 创建一个新的 <see cref="PlaywrightInstance"/> 实例。<br/>
/// Create a new <see cref="PlaywrightInstance"/>.
/// </remarks>
/// <param name="logger">日志记录器 / logger.</param>
/// <param name="instanceId">实例 Id / instance identifier.</param>
/// <param name="displayName">显示名称 / display name.</param>
/// <param name="ownerId">宿主/拥有者 Id / owner identifier.</param>
public class PlaywrightInstance(ILogger<PlaywrightInstance> logger, string instanceId, string displayName, string ownerId) : ICoreScreencastable, ICoreBrowserInstance
{
    private IBrowserContext? _context;
    private readonly ConcurrentDictionary<string, IPage> _pagesById = new();
    private static readonly Lazy<Task<IPlaywright>> _sharedPlaywrightLazy = new(() => Playwright.CreateAsync());
    /// <summary>
    /// 此实例所属的宿主标识。<br/>
    /// Identifier of the owner that created or manages this instance.
    /// </summary>
    public string OwnerId { get; } = ownerId;

    /// <summary>
    /// 最近一次发生的错误消息（如果有）。<br/>
    /// The most recent error message for this instance, if any.
    /// </summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// 实例唯一标识符。<br/>
    /// Unique identifier for this Playwright instance.
    /// </summary>
    public string InstanceId { get; } = instanceId;

    /// <summary>
    /// 人类可读的实例显示名称。<br/>
    /// Human-readable display name for the instance.
    /// </summary>
    public string DisplayName { get; } = displayName;

    /// <summary>
    /// 指示实例是否为无头（Headless）模式。<br/>
    /// Indicates whether the instance is running in headless mode.
    /// </summary>
    public bool IsHeadless { get; private set; }

    /// <summary>
    /// 与此实例关联的用户数据目录路径。<br/>
    /// Filesystem path to the user data directory used by this instance.
    /// </summary>
    public string UserDataDirectoryPath { get; set; } = string.Empty;

    /// <summary>
    /// 当前选中的标签页 Id（若有）。<br/>
    /// Currently selected page id (if any).
    /// </summary>
    public string? SelectedPageId { get; set; }

    /// <summary>
    /// 当前实例中所有页面的只读视图。<br/>
    /// Read-only view of all pages managed by this instance.
    /// </summary>
    public IReadOnlyList<IPage> Pages => _pagesById.Values.ToList().AsReadOnly();

    /// <summary>
    /// 当前实例中所有标签页 Id 的只读列表。会过滤已经关闭的 Page。
    /// Read-only list of all page/tab ids for this instance. Closed pages are filtered out.
    /// </summary>
    public IReadOnlyList<string> TabIds => _pagesById.Where(kv =>
    {
        try { return !(kv.Value?.IsClosed ?? true); }
        catch { return true; }
    }).Select(kv => kv.Key).ToList().AsReadOnly();

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    public event Action<string>? TabClosed;
    public event Action<string>? TabOpened;

    /// <summary>
    /// 根据标签页 Id 获取对应的页面对象（若存在）。<br/>
    /// Get the page object for the specified tab id, if it exists.
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id.</param>
    /// <returns>匹配的页面或 null。<br/>The matching page or null if not found.</returns>
    public IPage? GetPageById(string tabId)
    {
        if (!_pagesById.TryGetValue(tabId, out var p)) return null;
        try
        {
            if (p is null) { _pagesById.TryRemove(tabId, out _); return null; }
            if (p.IsClosed)
            {
                _pagesById.TryRemove(tabId, out _);
                if (SelectedPageId == tabId) SelectedPageId = _pagesById.Keys.FirstOrDefault();
                return null;
            }
        }
        catch { }

        return p;
    }

    /// <summary>
    /// 为指定页面创建一个 CDP 会话（如果浏览器上下文可用）。<br/>
    /// Create a CDP session for the specified page if the browser context is available.
    /// </summary>
    /// <param name="page">要为其创建 CDP 会话的页面 / page to create the CDP session for.</param>
    /// <returns>可能为 null 的异步 CDP 会话。<br/>An asynchronous CDP session or null if context is not available.</returns>
    public Task<ICDPSession?> CreateCdpSessionAsync(IPage page) => BrowserContext is null ? Task.FromResult<ICDPSession?>(null) : BrowserContext.NewCDPSessionAsync(page);

    /// <summary>
    /// 通过给定 CDP 会话分发鼠标事件。<br/>
    /// Dispatch a mouse event through the provided CDP session.
    /// </summary>
    /// <param name="session">CDP 会话 / CDP session.</param>
    /// <param name="args">事件参数，通常为字典表示 / event arguments, typically a dictionary object.</param>
    /// <returns>表示异步发送操作的任务 / A Task representing the send operation.</returns>
    public Task DispatchMouseEventAsync(ICDPSession session, object args) => session.SendAsync("Input.dispatchMouseEvent", (Dictionary<string, object>?)args);

    /// <summary>
    /// 通过给定 CDP 会话分发按键事件。<br/>
    /// Dispatch a keyboard event through the provided CDP session.
    /// </summary>
    /// <param name="session">CDP 会话 / CDP session.</param>
    /// <param name="args">事件参数，通常为字典表示 / event arguments, typically a dictionary object.</param>
    /// <returns>表示异步发送操作的任务 / A Task representing the send operation.</returns>
    public Task DispatchKeyEventAsync(ICDPSession session, object args) => session.SendAsync("Input.dispatchKeyEvent", (Dictionary<string, object>?)args);

    /// <summary>
    /// 底层的 Playwright 浏览器上下文（可能为 null）。<br/>
    /// Underlying Playwright browser context (may be null).
    /// </summary>
    public IBrowserContext? BrowserContext => _context;

    /// <summary>
    /// 初始化 Playwright 实例，创建持久化浏览器上下文并映射现有页面。<br/>
    /// Initialize the Playwright instance, launch a persistent browser context and map existing pages.
    /// </summary>
    /// <param name="userDataDir">用于浏览器持久化的用户数据目录路径 / user data directory path for the browser.</param>
    /// <param name="headless">是否以无头模式运行 / whether to run in headless mode.</param>
    /// <param name="userAgent">可选的用户代理字符串 / optional user agent string.</param>
    /// <returns>异步操作。<br/>Asynchronous operation.</returns>
    /// <exception cref="Exception">当初始化或 Playwright 启动失败时抛出 / Thrown when initialization or Playwright startup fails.</exception>
    public async Task InitializeAsync(string userDataDir, bool headless, string? userAgent = null)
    {
        // remember the user data path for this instance so callers can read it later
        UserDataDirectoryPath = userDataDir;
        Directory.CreateDirectory(userDataDir);
        try
        {
            var playwright = await GetSharedPlaywrightAsync();
            _context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = headless,
                UserAgent = userAgent,
                ViewportSize = null,
                Args = ["--no-first-run", "--disable-blink-features=AutomationControlled"]
            });
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            logger.LogError(ex, "Failed to initialize Playwright instance {InstanceId}", InstanceId);
            throw;
        }

        // Map existing pages from context to generated ids
        foreach (var page in _context.Pages)
        {
            var id = Guid.NewGuid().ToString("N");
            _pagesById[id] = page;
        }

        // Listen for pages created after initialization (e.g. popups/window.open)
        try
        {
            _context.Page += (_, page) =>
            {
                try
                {
                    var id = Guid.NewGuid().ToString("N");
                    _pagesById[id] = page;
                    SelectedPageId = id;
                    try { TabOpened?.Invoke(id); } catch { }
                }
                catch { }
            };
        }
        catch { }

        IsHeadless = headless;
        logger.LogInformation("Playwright instance initialized {InstanceId} headless={Headless}", InstanceId, IsHeadless);

        // Start background monitor to detect externally closed pages and raise TabClosed
        try
        {
            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;
            _monitorTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var keys = _pagesById.Keys.ToArray();
                        foreach (var key in keys)
                        {
                            if (!_pagesById.TryGetValue(key, out var pg)) continue;
                            bool closed = true;
                            try { closed = pg == null || pg.IsClosed; } catch { closed = true; }
                            if (closed)
                            {
                                if (_pagesById.TryRemove(key, out _))
                                {
                                    if (SelectedPageId == key) SelectedPageId = _pagesById.Keys.FirstOrDefault();
                                    try { TabClosed?.Invoke(key); } catch { }
                                }
                            }
                        }
                    }
                    catch { }
                    try { await Task.Delay(500, token); } catch { break; }
                }
            }, token);
        }
        catch { }
    }

    private static Task<IPlaywright> GetSharedPlaywrightAsync() => _sharedPlaywrightLazy.Value;

    /// <summary>
    /// 关闭浏览器上下文并清理页面映射。<br/>
    /// Close the browser context and clear page mappings.
    /// </summary>
    /// <returns>表示关闭操作完成的任务 / A Task representing the close operation.</returns>
    public async Task CloseAsync()
    {
        try
        {
            try { _monitorCts?.Cancel(); } catch { }
            try { if (_monitorTask != null) await _monitorTask; } catch { }
            if (_context != null)
                await _context.CloseAsync();
            _pagesById.Clear();
        }
        catch { }
    }

    /// <summary>
    /// 为指定页面生成并注册一个新的标签页 Id。<br/>
    /// Generate and register a new tab id for the specified page.
    /// </summary>
    /// <param name="page">页面对象 / page object.</param>
    /// <returns>新生成的标签页 Id / the newly generated tab id.</returns>
    public string CreateTabId(IPage page)
    {
        var id = Guid.NewGuid().ToString("N");
        _pagesById[id] = page;
        return id;
    }

    /// <summary>
    /// 在浏览器上下文中创建一个新页面并返回其标签页 Id，可选地导航到指定 URL。<br/>
    /// Create a new page in the browser context and return its tab id; optionally navigate to the provided URL.
    /// </summary>
    /// <param name="url">可选的要导航到的 URL / optional URL to navigate the new page to.</param>
    /// <returns>新页面的标签页 Id / the tab id of the created page.</returns>
    /// <exception cref="InvalidOperationException">当浏览器上下文尚未初始化时抛出 / Thrown when the browser context has not been initialized.</exception>
    public async Task<string> CreateTabAsync(string? url = null)
    {
        if (BrowserContext is null) throw new InvalidOperationException("Browser context not initialized");
        var page = await BrowserContext.NewPageAsync();
        if (!string.IsNullOrWhiteSpace(url))
        {
            try { await page.GotoAsync(url); } catch { }
        }
        var id = CreateTabId(page);
        SelectedPageId = id;
        return id;
    }

    /// <summary>
    /// 尝试根据标签页 Id 获取页面对象。<br/>
    /// Try to get the page object for the given tab id.
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id.</param>
    /// <param name="page">当返回 true 时该输出参数包含页面 / the out parameter that will contain the page when true.</param>
    /// <returns>如果找到页面则返回 true，否则返回 false / true if the page was found; otherwise false.</returns>
    public bool TryGetPage(string tabId, out IPage? page) => _pagesById.TryGetValue(tabId, out page);

    /// <summary>
    /// 关闭指定的标签页并从映射中移除它。<br/>
    /// Close the specified tab and remove it from internal mappings.
    /// </summary>
    /// <param name="tabId">要关闭的标签页 Id / tab id to close.</param>
    /// <returns>是否成功关闭标签页 / whether the tab was successfully closed.</returns>
    public async Task<bool> CloseTabAsync(string tabId)
    {
        if (!_pagesById.TryRemove(tabId, out var page)) return false;
        try { await page.CloseAsync(); } catch { }
        if (SelectedPageId == tabId) SelectedPageId = _pagesById.Keys.FirstOrDefault();
        try { TabClosed?.Invoke(tabId); } catch { }
        return true;
    }

    /// <summary>
    /// 将指定标签页设置为当前选中页。<br/>
    /// Select the specified tab as the currently selected page.
    /// </summary>
    /// <param name="tabId">标签页 Id / tab id to select.</param>
    /// <returns>如果选择成功返回 true，否则返回 false / true when selection succeeded; otherwise false.</returns>
    public bool SelectPage(string tabId)
    {
        if (!_pagesById.ContainsKey(tabId)) return false;
        SelectedPageId = tabId;
        return true;
    }

    /// <summary>
    /// 获取当前选中的页面（若有）。<br/>
    /// Get the currently selected page, if any.
    /// </summary>
    /// <returns>当前选中的页面或 null。<br/>The currently selected page or null if none is selected.</returns>
    public IPage? GetSelectedPage() => SelectedPageId is null ? null : (_pagesById.TryGetValue(SelectedPageId, out var p) ? p : null);
}
