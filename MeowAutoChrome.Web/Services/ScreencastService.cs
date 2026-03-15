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
    private readonly BrowserInstanceManager browserInstances;
    private readonly IHubContext<BrowserHub> hub;
    private ICDPSession? _session;
    private int _clientCount;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _enabled = true;
    private int _maxWidth = 1280;
    private int _maxHeight = 800;
    private int _frameIntervalMs;
    private long _lastFrameSentAtMs;
    private string? _targetPageId;

    public bool RequestedEnabled => _enabled;
    public bool Enabled => _enabled && browserInstances.IsHeadless;
    public int MaxWidth => _maxWidth;
    public int MaxHeight => _maxHeight;
    public int FrameIntervalMs => _frameIntervalMs;

    public ScreencastService(BrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, ProgramSettingsService programSettingsService)
    {
        this.browserInstances = browserInstances;
        this.hub = hub;

        var settings = programSettingsService.GetAsync().GetAwaiter().GetResult();
        _frameIntervalMs = FpsToInterval(settings.ScreencastFps);
    }

    private static int FpsToInterval(int fps)
        => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

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

    private void OnFrame(object? sender, JsonElement? e)
    {
        if (e is not { } frame) return;
        if (_session is { } session)
            _ = PushFrameAsync(frame, session);
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

    public async Task DispatchKeyEventAsync(KeyEventData d)
    {
        if (_session is not { } s) return;

        var args = new Dictionary<string, object>
        {
            ["type"] = d.Type,
            ["key"] = d.Key,
            ["code"] = d.Code,
            ["text"] = d.Text ?? "",
            ["modifiers"] = d.Modifiers,
        };

        try { await s.SendAsync("Input.dispatchKeyEvent", args); }
        catch { /* session 已失效时忽略 */ }
    }

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

            await browserInstances.SetViewportSizeAsync(_maxWidth, _maxHeight);

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
