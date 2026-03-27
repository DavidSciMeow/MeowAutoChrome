using System.Collections.Generic;

namespace MeowAutoChrome.Core.Models;

public sealed record BrowserInstanceInfo(string Id, string Name, string? OwnerPluginId, string Color, bool IsSelected, int PageCount);
public sealed record BrowserTabInfo(string Id, string Title, string Url, bool IsActive, string? OwnerPluginId = null);
public sealed record BrowserInstanceViewportSettingsResponse(int Width, int Height, string ViewportType = "Auto");
public sealed record BrowserInstanceSettingsResponse(string InstanceId, string DisplayName, string? UserDataDirectory = null);
public sealed record BrowserInstanceSettingsUpdateRequest(string InstanceId, bool IsHeadless, string? UserDataDirectory, int ViewportWidth, int ViewportHeight);
