using Microsoft.Playwright;
using System.Text.Json;
using MeowAutoChrome.Core.Services;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services;

public sealed class ScreencastServiceCore
{
    private readonly BrowserInstanceManagerCore _browserInstances;
    private readonly IScreencastFrameSink _sink;
    private readonly ILogger<ScreencastServiceCore> _logger;
    private ICDPSession? _session;
    private string? _targetPageId;

    public ScreencastServiceCore(BrowserInstanceManagerCore browserInstances, IScreencastFrameSink sink, ILogger<ScreencastServiceCore> logger)
    {
        _browserInstances = browserInstances;
        _sink = sink;
        _logger = logger;
    }

    public async Task EnsureTargetAsync()
    {
        var page = _browserInstances.ActivePage;
        if (page is null)
            return;

        if (_session is null || _targetPageId != _browserInstances.SelectedPageId)
        {
            try
            {
                _session = await _browserInstances.BrowserContext.NewCDPSessionAsync(page);
                _targetPageId = _browserInstances.SelectedPageId;
                _session.Event("Page.screencastFrame").OnEvent += OnFrame;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create CDP session");
            }
        }
    }

    private async void OnFrame(object? sender, JsonElement? e)
    {
        if (e is not { } frame) return;
        var sessionId = frame.GetProperty("sessionId").GetInt32();
        var data = frame.GetProperty("data").GetString();
        int? width = null;
        int? height = null;

        if (frame.TryGetProperty("metadata", out var metadata))
        {
            if (metadata.TryGetProperty("deviceWidth", out var deviceWidth) && deviceWidth.TryGetInt32(out var w))
                width = w;
            if (metadata.TryGetProperty("deviceHeight", out var deviceHeight) && deviceHeight.TryGetInt32(out var h))
                height = h;
        }

        try
        {
            await _session.SendAsync("Page.screencastFrameAck", new Dictionary<string, object> { ["sessionId"] = sessionId });
        }
        catch { }

        if (data != null)
            await _sink.SendFrameAsync(data, width, height);
    }
}
