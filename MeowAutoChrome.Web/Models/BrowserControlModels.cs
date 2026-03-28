namespace MeowAutoChrome.Web.Models;

public sealed record BrowserCreateInstanceRequest(string? OwnerPluginId, string? DisplayName, string? UserDataDirectory, string? PreviewInstanceId);
public sealed record BrowserCreateTabRequest(string? InstanceId, string? Url);
public sealed record BrowserCreateInstanceResponse(string InstanceId, string? UserDataDirectory);
public sealed record BrowserNavigateRequest(string Url);
public sealed record BrowserViewportSyncRequest(int Width, int Height);
public sealed record BrowserCloseTabRequest(string TabId);
public sealed record BrowserCloseInstanceRequest(string InstanceId);
public sealed record ValidateInstanceFolderRequest(string RootPath, string FolderName);
public sealed record ValidateInstanceFolderResponse(bool Ok, string? Error, string? Folder);
public sealed record BrowserSelectTabRequest(string TabId);
public sealed record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);
public sealed record BrowserLayoutSettingsRequest(int PluginPanelWidth);

public sealed record BrowserStatusResponse(
    string? CurrentUrl,
    string? Title,
    string? ErrorMessage,
    bool SupportsScreencast,
    bool ScreencastEnabled,
    int ScreencastMaxWidth,
    int ScreencastMaxHeight,
    int ScreencastFrameIntervalMs,
    double CpuUsagePercent,
    double MemoryUsageMb,
    int TotalPageCount,
    int PluginPanelWidth,
    IReadOnlyList<BrowserTabInfoDto> Tabs,
    string? CurrentInstanceId,
    BrowserInstanceViewportSettingsResponseDto CurrentViewport,
    bool IsHeadless);


public sealed record BrowserTabInfoDto(string Id, string? Title, string? Url, bool IsActive, string? OwnerPluginId = null);

public sealed record BrowserInstanceViewportSettingsResponseDto(int Width, int Height, string ViewportType = "Auto");

