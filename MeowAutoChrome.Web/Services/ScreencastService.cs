using MeowAutoChrome.Web.Hubs;
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Warpper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Playwright;
using System.Text.Json;

namespace MeowAutoChrome.Web.Services;
/// <summary>
/// 单例服务，管理 CDP Screencast 会话生命周期：
/// - 第一个客户端连接时自动开始推帧
/// - 最后一个客户端断开时自动停止推帧
/// </summary>
public class ScreencastService
{
    /// <summary>
    /// 用于获取当前活动页面和浏览器上下文，以便创建 CDP 会话和订阅帧事件
    /// </summary>
    private readonly BrowserInstanceManager browserInstances;
    /// <summary>
    /// 用于向所有连接的客户端广播帧数据和状态变化（如禁用通知）
    /// </summary>
    private readonly IHubContext<BrowserHub> hub;
    /// <summary>
    /// 当前的 CDP 会话实例；null 表示未启动推流
    /// </summary>
    private ICDPSession? _session;
    /// <summary>
    /// 当前连接的客户端数量；用于决定何时启动或停止推流
    /// </summary>
    private int _clientCount;
    /// <summary>
    /// 用于保护对 _clientCount 和推流状态的访问，确保线程安全
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    /// <summary>
    /// 用户请求的启用状态（不考虑浏览器模式）；只有在浏览器为 headless 时才实际启用推流
    /// </summary>
    private bool _enabled = true;
    /// <summary>
    /// 当前推流的最大宽度（像素）；可以通过 UpdateSettingsAsync 更新，必要时重启推流以应用新设置
    /// </summary>
    private int _maxWidth = 1280;
    /// <summary>
    /// 当前推流的最大高度（像素）；可以通过 UpdateSettingsAsync 更新，必要时重启推流以应用新设置
    /// </summary>
    private int _maxHeight = 800;
    /// <summary>
    /// 两帧之间的最小间隔，单位毫秒；通过 FpsToInterval 方法从用户设置的 FPS 转换得到，默认值根据 ProgramSettings 初始化；可以通过 UpdateSettingsAsync 更新，必要时重启推流以应用新设置
    /// </summary>
    private int _frameIntervalMs;
    /// <summary>
    /// 上一帧成功发送给客户端的时间戳（Environment.TickCount64），用于实现基于时间的帧率限制；通过 CanSendFrameNow 方法检查是否可以发送新的一帧；在 StartAsync 和 StopAsync 中重置为 0
    /// </summary>
    private long _lastFrameSentAtMs;
    /// <summary>
    /// 当前推流的目标页面 ID；在 StartAsync 时设置为当前选定页面的 ID，在 StopAsync 时重置为 null；在 EnsureTargetAsync 中检查是否需要切换目标页面
    /// </summary>
    private string? _targetPageId;

    /// <summary>
    /// 用户请求的启用状态（不考虑浏览器是否为 headless）。
    /// </summary>
    public bool RequestedEnabled => _enabled;

    /// <summary>
    /// 实际的启用状态，考虑了浏览器模式（只有 headless 时才启用）。
    /// </summary>
    public bool Enabled => _enabled && browserInstances.IsHeadless;

    /// <summary>
    /// 当前推流的最大宽度（像素）。
    /// </summary>
    public int MaxWidth => _maxWidth;

    /// <summary>
    /// 当前推流的最大高度（像素）。
    /// </summary>
    public int MaxHeight => _maxHeight;

    /// <summary>
    /// 两帧之间的最小间隔，单位毫秒。
    /// </summary>
    public int FrameIntervalMs => _frameIntervalMs;

    /// <summary>
    /// 构造函数，初始化 ScreencastService 并从设置中读取帧率配置。
    /// </summary>
    /// <param name="browserInstances">浏览器实例管理器，用于获取活动页面与上下文。</param>
    /// <param name="hub">SignalR Hub 上下文，用于向客户端广播帧数据与状态变化。</param>
    /// <param name="programSettingsService">程序设置服务，用于读取初始 Screencast FPS 配置。</param>
    public ScreencastService(BrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, ProgramSettingsService programSettingsService)
    {
        this.browserInstances = browserInstances;
        this.hub = hub;

        var settings = programSettingsService.GetAsync().GetAwaiter().GetResult();
        _frameIntervalMs = FpsToInterval(settings.ScreencastFps);
    }

    /// <summary>
    /// 转换用户设置的 FPS（帧率）为两帧之间的最小间隔（毫秒）。确保间隔不小于 16ms（约 60 FPS），并且对输入的 FPS 进行合理限制（1-60）。这个方法用于根据用户期望的帧率计算实际的发送频率限制。
    /// </summary>
    /// <param name="fps">帧率</param>
    /// <returns>两帧之间的最小间隔</returns>
    private static int FpsToInterval(int fps) => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

    /// <summary>
    /// 处理客户端连接事件。每当有客户端连接时调用；当第一个客户端连接时会启动 Screencast 推流。
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
    /// 处理客户端断开事件。每当有客户端断开时调用；当最后一个客户端断开时会停止 Screencast 推流。
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
    /// 启动 CDP Screencast：创建 CDP 会话并订阅帧事件，然后通过 Chromium 开始推流。
    /// </summary>
    private async Task StartAsync()
    {
        if (!Enabled) return;

        var page = browserInstances.ActivePage;
        if (page == null) return;

        _targetPageId = browserInstances.SelectedPageId;
        Interlocked.Exchange(ref _lastFrameSentAtMs, 0);

        _session = await browserInstances.BrowserContext.NewCDPSessionAsync(page);
        _session.Event("Page.screencastFrame").OnEvent += OnFrame;

        await _session.SendAsync("Page.startScreencast", new Dictionary<string, object>
        {
            ["format"] = "jpeg",
            ["quality"] = 80,
            ["maxWidth"] = _maxWidth,
            ["maxHeight"] = _maxHeight,
        });
    }

    /// <summary>
    /// 停止当前的 CDP Screencast 会话并清理相关状态（取消订阅事件、释放会话）。
    /// </summary>
    private async Task StopAsync()
    {
        if (_session == null) return;
        _session.Event("Page.screencastFrame").OnEvent -= OnFrame;
        try { await _session.SendAsync("Page.stopScreencast"); }
        catch { /* page 已关闭时忽略 */ }
        _session = null;
        _targetPageId = null;
        Interlocked.Exchange(ref _lastFrameSentAtMs, 0);
    }

    /// <summary>
    /// CDP 帧事件回调：在事件到达时异步推送帧处理任务。
    /// </summary>
    private void OnFrame(object? sender, JsonElement? e)
    {
        if (e is not { } frame) return;
        if (_session is { } session)
            _ = PushFrameAsync(frame, session);
    }

    /// <summary>
    /// 处理单个推流帧：解析数据、应答 ACK，并将编码后的帧广播给所有连接的客户端（符合发送频率限制时）。
    /// </summary>
    /// <param name="e">来自 CDP 的帧事件数据（JSON）。</param>
    /// <param name="session">CDP 会话实例（用于发送 ACK）。</param>
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

        // 必须 Ack，否则 Chromium 停止推帧
        try
        {
            await session.SendAsync("Page.screencastFrameAck", new Dictionary<string, object>
            {
                ["sessionId"] = sessionId
            });
        }
        catch { /* session 已失效时忽略 */ }

        if (data != null && CanSendFrameNow())
            await hub.Clients.All.SendAsync("ReceiveFrame", new
            {
                data,
                width,
                height,
            });
    }
    /// <summary>
    /// 将来自客户端的鼠标事件通过 CDP 会话转发到目标页面。
    /// </summary>
    /// <param name="d">鼠标事件数据。</param>
    public async Task DispatchMouseEventAsync(MouseEventData d)
    {
        if (_session is not { } s) return;

        var args = new Dictionary<string, object>
        {
            ["type"] = d.Type,
            ["x"] = d.X,
            ["y"] = d.Y,
            ["button"] = d.Button,
            ["buttons"] = d.Buttons,
            ["clickCount"] = d.ClickCount,
            ["modifiers"] = d.Modifiers,
        };
        if (d.DeltaX is { } dx) args["deltaX"] = dx;
        if (d.DeltaY is { } dy) args["deltaY"] = dy;

        try { await s.SendAsync("Input.dispatchMouseEvent", args); }
        catch { /* session 已失效时忽略 */ }
    }
    /// <summary>
    /// 将来自客户端的键盘事件通过 CDP 会话转发到目标页面。
    /// </summary>
    /// <param name="d">键盘事件数据。</param>
    public async Task DispatchKeyEventAsync(KeyEventData d)
    {
        if (_session is not { } s) return;

        var args = new Dictionary<string, object>
        {
            ["type"] = d.Type,
            ["key"] = d.Key,
            ["code"] = d.Code,
            ["text"] = d.Text ?? "",
            ["unmodifiedText"] = d.Text ?? "",
            ["modifiers"] = d.Modifiers,
            ["windowsVirtualKeyCode"] = d.WindowsVirtualKeyCode,
            ["nativeVirtualKeyCode"] = d.NativeVirtualKeyCode,
            ["autoRepeat"] = d.AutoRepeat,
            ["isKeypad"] = d.IsKeypad,
            ["isSystemKey"] = d.IsSystemKey,
        };

        try { await s.SendAsync("Input.dispatchKeyEvent", args); }
        catch { /* session 已失效时忽略 */ }
    }
    /// <summary>
    /// 更新 Screencast 的设置（启用状态、最大分辨率和帧间隔），必要时在客户端存在时重启推流以应用新设置。
    /// </summary>
    /// <param name="enabled">是否启用 Screencast（由用户设置，不考虑浏览器模式）。</param>
    /// <param name="maxWidth">推流允许的最大宽度（像素）。</param>
    /// <param name="maxHeight">推流允许的最大高度（像素）。</param>
    /// <param name="frameIntervalMs">两帧之间的最小间隔，单位毫秒。</param>
    public async Task UpdateSettingsAsync(bool enabled, int maxWidth, int maxHeight, int frameIntervalMs)
    {
        await _semaphore.WaitAsync();
        try
        {
            maxWidth = Math.Max(320, maxWidth);
            maxHeight = Math.Max(240, maxHeight);
            frameIntervalMs = Math.Clamp(frameIntervalMs, 16, 2000);

            var wasEnabled = Enabled;
            var widthChanged = _maxWidth != maxWidth;
            var heightChanged = _maxHeight != maxHeight;

            _enabled = enabled;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
            _frameIntervalMs = frameIntervalMs;

            var restartNeeded = wasEnabled != Enabled || widthChanged || heightChanged;

            if (!restartNeeded || _clientCount <= 0)
                return;

            await StopAsync();

            if (Enabled)
                await StartAsync();
            else
                await hub.Clients.All.SendAsync("ScreencastDisabled");
        }
        finally { _semaphore.Release(); }
    }

    /// <summary>
    /// 判断当前是否允许发送新的一帧，基于配置的帧间隔（线程安全）。
    /// </summary>
    private bool CanSendFrameNow()
    {
        var interval = Volatile.Read(ref _frameIntervalMs);
        if (interval <= 16)
            return true;

        while (true)
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastFrameSentAtMs);

            if (last != 0 && now - last < interval)
                return false;

            if (Interlocked.CompareExchange(ref _lastFrameSentAtMs, now, last) == last)
                return true;
        }
    }
    /// <summary>
    /// 强制刷新当前推流目标：停止并重新开始 Screencast（仅在有客户端连接时执行）。
    /// </summary>
    public async Task RefreshTargetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_clientCount <= 0)
                return;

            await StopAsync();

            if (Enabled)
                await StartAsync();
            else
                await hub.Clients.All.SendAsync("ScreencastDisabled");
        }
        finally { _semaphore.Release(); }
    }
    /// <summary>
    /// 确保当前的 CDP 会话指向正确的目标页面；如果会话不存在或目标变更，则（重新）启动 Screencast。
    /// </summary>
    public async Task EnsureTargetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_clientCount <= 0 || !Enabled)
                return;

            _ = browserInstances.ActivePage;
            var selectedPageId = browserInstances.SelectedPageId;

            if (_session == null)
            {
                await StartAsync();
                return;
            }

            if (_targetPageId != selectedPageId)
            {
                await StopAsync();
                await StartAsync();
            }
        }
        finally { _semaphore.Release(); }
    }
    /// <summary>
    /// 在浏览器模式（例如 headless 与非 headless）变化时调用：会停止当前推流，并根据新模式在有客户端时重启或通知禁用。
    /// </summary>
    public async Task OnBrowserModeChangedAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            await StopAsync();

            if (_clientCount <= 0)
                return;

            if (Enabled)
                await StartAsync();
            else
                await hub.Clients.All.SendAsync("ScreencastDisabled");
        }
        finally { _semaphore.Release(); }
    }
}
