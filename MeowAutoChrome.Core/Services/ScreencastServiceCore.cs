using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 核心屏幕投射服务：管理 CDP 会话、接收帧并将其转发到注册的帧接收端，同时处理客户端连接计数与设置变更。<br/>
/// Core screencast service that manages CDP sessions, receives frames and forwards them to a registered frame sink, while handling client connections and setting changes.
/// </summary>
/// <remarks>
/// 构造函数：创建 ScreencastServiceCore 并注入所需的依赖项（浏览器实例管理器、帧接收端与日志）。<br/>
/// Constructor: creates ScreencastServiceCore and injects required dependencies (browser instance manager, frame sink, and logger).
/// </remarks>
/// <param name="browserInstances">浏览器实例管理器 / browser instance manager.</param>
/// <param name="sink">用于发送帧的接收端 / frame sink used to send frames.</param>
/// <param name="logger">日志记录器 / logger.</param>
public sealed class ScreencastServiceCore(BrowserInstanceManagerCore browserInstances, IScreencastFrameSink sink, ILogger<ScreencastServiceCore> logger)
{

    // CDP session for the current target page
    private ICDPSession? _session;
    private string? _targetPageId;

    // client connection tracking and synchronization
    private int _clientCount;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // settings
    private bool _enabled = true;
    private int _maxWidth = 1280;
    private int _maxHeight = 800;
    private int _frameIntervalMs = 100;
    private long _lastFrameSentAtMs;

    /// <summary>
    /// 指示屏幕投射当前是否启用。<br/>
    /// Indicates whether screencast is currently enabled.
    /// </summary>
    public bool Enabled => _enabled;

    /// <summary>
    /// 最大帧宽度（像素）。<br/>
    /// Maximum frame width in pixels.
    /// </summary>
    public int MaxWidth => _maxWidth;

    /// <summary>
    /// 最大帧高度（像素）。<br/>
    /// Maximum frame height in pixels.
    /// </summary>
    public int MaxHeight => _maxHeight;

    /// <summary>
    /// 帧间隔，毫秒。<br/>
    /// Frame interval in milliseconds.
    /// </summary>
    public int FrameIntervalMs => _frameIntervalMs;

    private static int FpsToInterval(int fps) => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

    /// <summary>
    /// 当有客户端连接时调用：增加连接计数并在必要时启动投射。<br/>
    /// Called when a client connects: increments client count and starts screencast if needed.
    /// </summary>
    public async Task OnClientConnectedAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _clientCount++;
            if (_clientCount == 1)
                await StartAsync();
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// 当客户端断开连接时调用：减少连接计数并在没有客户端时停止投射。<br/>
    /// Called when a client disconnects: decrements client count and stops screencast when no clients remain.
    /// </summary>
    public async Task OnClientDisconnectedAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _clientCount = Math.Max(0, _clientCount - 1);
            if (_clientCount == 0)
                await StopAsync();
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// 确保当前有目标页面的 CDP 会话可用；当选中页面变化时重建会话。<br/>
    /// Ensure that a CDP session for the current target page is available; recreate session when selected page changes.
    /// </summary>
    public async Task EnsureTargetAsync()
    {
        var page = browserInstances.ActivePage;
        if (page is null) return;

        if (_session is null || _targetPageId != browserInstances.SelectedPageId)
        {
            // recreate session for new target
            try
            {
                _session = await browserInstances.BrowserContext.NewCDPSessionAsync(page);
                _targetPageId = browserInstances.SelectedPageId;
                _session.Event("Page.screencastFrame").OnEvent += OnFrame;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create CDP session");
                _session = null;
            }
        }
    }

    private async Task StartAsync()
    {
        if (!_enabled) return;
        if (browserInstances.ActivePage is null) return;

        await EnsureTargetAsync();
        if (_session is null) return;

        Interlocked.Exchange(ref _lastFrameSentAtMs, 0);

        try
        {
            await _session.SendAsync("Page.startScreencast", new Dictionary<string, object>
            {
                ["format"] = "jpeg",
                ["quality"] = 80,
                ["maxWidth"] = _maxWidth,
                ["maxHeight"] = _maxHeight,
            });
            logger.LogDebug("Started screencast");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start screencast");
        }
    }

    private async Task StopAsync()
    {
        if (_session is null) return;
        _session.Event("Page.screencastFrame").OnEvent -= OnFrame;
        try { await _session.SendAsync("Page.stopScreencast"); } catch { }
        _session = null;
        _targetPageId = null;
        Interlocked.Exchange(ref _lastFrameSentAtMs, 0);
        logger.LogDebug("Stopped screencast");
    }

    private void OnFrame(object? sender, JsonElement? e)
    {
        if (e is not { } frame) return;
        if (_session is { } session)
        {
            _ = PushFrameAsync(frame, session);
        }
    }

    private async Task PushFrameAsync(JsonElement e, ICDPSession session)
    {
        var sessionId = e.GetProperty("sessionId").GetInt32();
        var data = e.GetProperty("data").GetString();
        int? width = null;
        int? height = null;

        if (e.TryGetProperty("metadata", out var metadata))
        {
            if (metadata.TryGetProperty("deviceWidth", out var deviceWidth) && deviceWidth.TryGetInt32(out var w))
                width = w;
            if (metadata.TryGetProperty("deviceHeight", out var deviceHeight) && deviceHeight.TryGetInt32(out var h))
                height = h;
        }

        try
        {
            await session.SendAsync("Page.screencastFrameAck", new Dictionary<string, object> { ["sessionId"] = sessionId });
        }
        catch { }

        if (data != null && CanSendFrameNow())
            await sink.SendFrameAsync(data, width, height);
    }

    /// <summary>
    /// 通过底层 CDP 会话分发鼠标事件（如果会话存在）。<br/>
    /// Dispatch a mouse event via the underlying CDP session if available.
    /// </summary>
    /// <param name="d">来自 Web 层的事件数据（字典或类似结构）/ event data from the Web layer (dictionary-like).</param>
    public async Task DispatchMouseEventAsync(object d)
    {
        if (_session is null) return;
        // d is a browser-agnostic object from Web layer; attempt to map
        try
        {
            // Expect dictionary-like shape
            if (d is IDictionary<string, object> dict)
            {
                var dd = new Dictionary<string, object>(dict);
                await _session.SendAsync("Input.dispatchMouseEvent", dd);
            }
        }
        catch { }
    }

    /// <summary>
    /// 通过底层 CDP 会话分发按键事件（如果会话存在）。<br/>
    /// Dispatch a keyboard event via the underlying CDP session if available.
    /// </summary>
    /// <param name="d">来自 Web 层的事件数据（字典或类似结构）/ event data from the Web layer (dictionary-like).</param>
    public async Task DispatchKeyEventAsync(object d)
    {
        if (_session is null) return;
        try
        {
            if (d is IDictionary<string, object> dict)
            {
                var dd = new Dictionary<string, object>(dict);
                await _session.SendAsync("Input.dispatchKeyEvent", dd);
            }
        }
        catch { }
    }

    /// <summary>
    /// 请求的启用状态（当前设置的启用标志）。<br/>
    /// The requested enabled flag (current configured enabled state).
    /// </summary>
    public bool RequestedEnabled => _enabled;

    /// <summary>
    /// 更新投射设置（启用、最大宽高与帧间隔），并在需要时重启或停止投射。<br/>
    /// Update screencast settings (enabled, max dimensions and frame interval) and restart/stop screencast if necessary.
    /// </summary>
    /// <param name="enabled">是否启用投射 / whether to enable screencast.</param>
    /// <param name="maxWidth">最大帧宽度 / max frame width.</param>
    /// <param name="maxHeight">最大帧高度 / max frame height.</param>
    /// <param name="frameIntervalMs">帧间隔（毫秒）/ frame interval in milliseconds.</param>
    public async Task UpdateSettingsAsync(bool enabled, int maxWidth, int maxHeight, int frameIntervalMs)
    {
        await _semaphore.WaitAsync();
        try
        {
            maxWidth = Math.Max(320, maxWidth);
            maxHeight = Math.Max(240, maxHeight);
            frameIntervalMs = Math.Clamp(frameIntervalMs, 16, 2000);

            var wasEnabled = _enabled;
            var widthChanged = _maxWidth != maxWidth;
            var heightChanged = _maxHeight != maxHeight;

            _enabled = enabled;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
            _frameIntervalMs = frameIntervalMs;

            var restartNeeded = wasEnabled != _enabled || widthChanged || heightChanged;

            if (!restartNeeded || _clientCount <= 0)
                return;

            await StopAsync();

            if (_enabled)
                await StartAsync();
            else
                await sink.NotifyScreencastDisabledAsync();
        }
        finally { _semaphore.Release(); }
    }

    private bool CanSendFrameNow()
    {
        var interval = Volatile.Read(ref _frameIntervalMs);
        if (interval <= 16) return true;

        while (true)
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastFrameSentAtMs);

            if (last != 0 && now - last < interval) return false;

            if (Interlocked.CompareExchange(ref _lastFrameSentAtMs, now, last) == last)
                return true;
        }
    }

    /// <summary>
    /// 刷新当前目标：在需要时重启或停止投射以应用最新上下文或设置。<br/>
    /// Refresh the current target: restart or stop screencast as needed to apply context or setting changes.
    /// </summary>
    public async Task RefreshTargetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_clientCount <= 0 || !_enabled) return;

            await StopAsync();
            if (_enabled) await StartAsync(); else await sink.NotifyScreencastDisabledAsync();
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// 当浏览器模式发生变化时调用以重新评估目标与会话。<br/>
    /// Called when the browser mode changes to re-evaluate the target/session.
    /// </summary>
    public Task OnBrowserModeChangedAsync() =>
        // Re-evaluate target/session when browser mode changes
        RefreshTargetAsync();
}
