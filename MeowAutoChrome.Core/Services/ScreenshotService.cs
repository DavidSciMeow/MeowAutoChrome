using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services;

public sealed class ScreenshotService
{
    private readonly BrowserInstanceManagerCore _manager;
    private readonly ILogger<ScreenshotService> _logger;

    public ScreenshotService(BrowserInstanceManagerCore manager, ILogger<ScreenshotService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    /// <summary>
    /// Capture a PNG screenshot from the first available page of any running instance.
    /// If no page/instance available returns null.
    /// </summary>
    public async Task<byte[]?> CaptureScreenshotAsync()
    {
        // choose first available instance
        var instance = _manager.Instances.FirstOrDefault();
        if (instance is null)
            return null;

        var context = instance.BrowserContext;
        if (context is null)
            return null;

        var page = context.Pages.FirstOrDefault();
        if (page is null)
            return null;

        try
        {
            var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png });
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture screenshot");
            return null;
        }
    }
}
