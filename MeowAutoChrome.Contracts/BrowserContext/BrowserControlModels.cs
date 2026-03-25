using System.Collections.Generic;

namespace MeowAutoChrome.Contracts.BrowserContext;

public record BrowserNavigateRequest(string Url);
public record BrowserSelectTabRequest(string TabId);
public record BrowserCloseTabRequest(string TabId);
public record BrowserCloseInstanceRequest(string InstanceId);
public record ScreencastSettingsRequest(bool Enabled, int MaxWidth, int MaxHeight, int FrameIntervalMs);
public record BrowserLayoutSettingsRequest(int PluginPanelWidth);
public record BrowserViewportSyncRequest(int Width, int Height);
public record BrowserCreateInstanceRequest(string OwnerPluginId, string? DisplayName, string? UserDataDirectory, string? PreviewInstanceId);
public record BrowserCreateTabRequest(string? InstanceId, string? Url);

public record ValidateInstanceFolderRequest(string? RootPath, string FolderName);
public record ValidateInstanceFolderResponse(bool Valid, string? Message, string FullPath);

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

public record BrowserTabInfo(
    string Id,
    string? Title,
    string? Url,
    bool IsSelected,
    string InstanceId,
    string InstanceName,
    string InstanceColor,
    string? InstanceOwnerId,
    bool IsInSelectedInstance);

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
