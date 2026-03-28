using Microsoft.Playwright;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services;

public sealed class ScreencastServiceCore
{
    private readonly BrowserInstanceManagerCore _browserInstances;
    private readonly IScreencastFrameSink _sink;
    private readonly ILogger<ScreencastServiceCore> _logger;

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

    public ScreencastServiceCore(BrowserInstanceManagerCore browserInstances, IScreencastFrameSink sink, ILogger<ScreencastServiceCore> logger)
    {
        _browserInstances = browserInstances;
        _sink = sink;
        _logger = logger;
    }

    public bool Enabled => _enabled;
    public int MaxWidth => _maxWidth;
    public int MaxHeight => _maxHeight;
    public int FrameIntervalMs => _frameIntervalMs;

    private static int FpsToInterval(int fps) => Math.Max(16, (int)Math.Round(1000d / Math.Clamp(fps, 1, 60)));

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

    public async Task EnsureTargetAsync()
    {
        var page = _browserInstances.ActivePage;
        if (page is null) return;

        if (_session is null || _targetPageId != _browserInstances.SelectedPageId)
        {
            // recreate session for new target
            try
            {
                _session = await _browserInstances.BrowserContext.NewCDPSessionAsync(page);
                _targetPageId = _browserInstances.SelectedPageId;
                _session.Event("Page.screencastFrame").OnEvent += OnFrame;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create CDP session");
                _session = null;
            }
        }
    }

    private async Task StartAsync()
    {
        if (!_enabled) return;
        if (_browserInstances.ActivePage is null) return;

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
            _logger.LogDebug("Started screencast");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start screencast");
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
        _logger.LogDebug("Stopped screencast");
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
            await _sink.SendFrameAsync(data, width, height);
    }

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

    public bool RequestedEnabled => _enabled;

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
                await _sink.NotifyScreencastDisabledAsync();
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

    public async Task RefreshTargetAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_clientCount <= 0 || !_enabled) return;

            await StopAsync();
            if (_enabled) await StartAsync(); else await _sink.NotifyScreencastDisabledAsync();
        }
        finally { _semaphore.Release(); }
    }

    public Task OnBrowserModeChangedAsync()
    {
        // Re-evaluate target/session when browser mode changes
        return RefreshTargetAsync();
    }
}
