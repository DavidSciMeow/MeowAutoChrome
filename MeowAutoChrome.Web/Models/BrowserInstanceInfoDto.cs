namespace MeowAutoChrome.Web.Models;

/// <summary>
/// DTO used by Web layer (SignalR Hubs / Views) to represent instance info without referencing Core model types.
/// </summary>
public record BrowserInstanceInfoDto(
    string Id,
    string DisplayName,
    string? UserDataDirectory,
    string Color,
    bool IsCurrent,
    int PageCount
);
