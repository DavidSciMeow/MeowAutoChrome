namespace MeowAutoChrome.Web.Models;

public sealed record BrowserInstanceSettingsResponseDto(string InstanceId, string DisplayName, string? UserDataDirectory = null);
