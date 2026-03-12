using MeowAutoChrome.Web.Warpper;

namespace MeowAutoChrome.Web.Models;
public record BrowserNavigateRequest(string Url);

public record BrowserSelectTabRequest(string TabId);

public record BrowserCloseTabRequest(string TabId);

public record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);

public record BrowserLayoutSettingsRequest(int PluginPanelWidth);

public record BrowserStatusResponse(
    string? Url,
    string? Title,
    string? ErrorMessage,
    bool ScreencastEnabled,
    int ScreencastMaxWidth,
    int ScreencastMaxHeight,
    int ScreencastFrameIntervalMs,
    double CpuUsagePercent,
    double MemoryUsageMb,
    int PageCount,
    int PluginPanelWidth,
    IReadOnlyList<BrowserTabInfo> Tabs
);
