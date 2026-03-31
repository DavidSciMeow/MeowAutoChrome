using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace MeowAutoChrome.Core.Services;

/// <summary>
/// 屏幕截图服务：从第一个可用页面捕获 PNG 格式的截图。<br/>
/// Screenshot service that captures a PNG screenshot from the first available page.
/// </summary>
public sealed class ScreenshotService
{
    private readonly BrowserInstanceManagerCore _manager;
    private readonly ILogger<ScreenshotService> _logger;

    /// <summary>
    /// 创建 ScreenshotService 实例。<br/>
    /// Create a ScreenshotService instance.
    /// </summary>
    /// <param name="manager">浏览器实例管理器 / browser instance manager.</param>
    /// <param name="logger">日志记录器 / logger.</param>
    public ScreenshotService(BrowserInstanceManagerCore manager, ILogger<ScreenshotService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    /// <summary>
    /// 从任一运行实例的第一个可用页面捕获 PNG 截图并以字节数组返回；若无可用页面或发生错误则返回 null。<br/>
    /// Capture a PNG screenshot from the first available page of any running instance; returns null when no page is available or on error.
    /// </summary>
    /// <returns>截图字节数组或 null / screenshot bytes or null.</returns>
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
