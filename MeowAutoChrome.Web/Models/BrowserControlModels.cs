using MeowAutoChrome.Web.Warpper;

namespace MeowAutoChrome.Web.Models;
public record BrowserNavigateRequest(string Url);

public record BrowserSelectTabRequest(string TabId);

public record BrowserCloseTabRequest(string TabId);

public record BrowserCloseInstanceRequest(string InstanceId);

public record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);

public record BrowserLayoutSettingsRequest(int PluginPanelWidth);

public record BrowserViewportSyncRequest(int Width, int Height);

public record BrowserInstanceViewportSettingsResponse(
    int Width,
    int Height,
    bool AutoResizeViewport,
    bool PreserveAspectRatio);

public record BrowserInstanceUserAgentSettingsResponse(
    string? ProgramUserAgent,
    bool AllowInstanceOverride,
    bool UseProgramUserAgent,
    string? UserAgent,
    string? EffectiveUserAgent,
    bool IsLocked);

public record BrowserInstanceSettingsResponse(
    string InstanceId,
    string InstanceName,
    string UserDataDirectory,
    bool IsSelected,
    BrowserInstanceViewportSettingsResponse Viewport,
    BrowserInstanceUserAgentSettingsResponse UserAgent);

public record BrowserInstanceSettingsUpdateRequest(
    string InstanceId,
    string UserDataDirectory,
    int ViewportWidth,
    int ViewportHeight,
    bool AutoResizeViewport,
    bool PreserveAspectRatio,
    bool UseProgramUserAgent,
    string? UserAgent,
    bool MigrateExistingUserData,
    int? DisplayWidth,
    int? DisplayHeight);

public record BrowserStatusResponse(
    string? Url,
    string? Title,
    string? ErrorMessage,
    bool SupportsScreencast,
    bool ScreencastEnabled,
    int ScreencastMaxWidth,
    int ScreencastMaxHeight,
    int ScreencastFrameIntervalMs,
    double CpuUsagePercent,
    double MemoryUsageMb,
    int PageCount,
    int PluginPanelWidth,
    IReadOnlyList<BrowserTabInfo> Tabs,
    string CurrentInstanceId,
    BrowserInstanceViewportSettingsResponse CurrentInstanceViewport
);
