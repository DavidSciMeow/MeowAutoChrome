namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 实例级 User-Agent 设置 DTO。<br/>
/// DTO describing instance-level User-Agent settings.
/// </summary>
/// <param name="UseProgramUserAgent">是否跟随程序级 User-Agent。<br/>Whether the instance follows the program-level User-Agent.</param>
/// <param name="IsLocked">是否被全局设置锁定。<br/>Whether the value is locked by global settings.</param>
/// <param name="UserAgent">实例级 User-Agent。<br/>Instance-level User-Agent.</param>
/// <param name="ProgramUserAgent">程序级 User-Agent。<br/>Program-level User-Agent.</param>
/// <param name="EffectiveUserAgent">当前实际生效的 User-Agent。<br/>Currently effective User-Agent.</param>
public sealed record BrowserInstanceSettingsUserAgentDto(
    bool UseProgramUserAgent,
    bool IsLocked,
    string? UserAgent,
    string? ProgramUserAgent,
    string? EffectiveUserAgent);

/// <summary>
/// 实例级视口设置 DTO。<br/>
/// DTO describing instance-level viewport settings.
/// </summary>
/// <param name="Width">默认宽度。<br/>Default width.</param>
/// <param name="Height">默认高度。<br/>Default height.</param>
/// <param name="PreserveAspectRatio">是否保持宽高比。<br/>Whether to preserve aspect ratio.</param>
/// <param name="AutoResizeViewport">是否自动跟随 UI 尺寸调整。<br/>Whether the viewport should automatically track UI size.</param>
public sealed record BrowserInstanceSettingsViewportDto(
    int Width,
    int Height,
    bool PreserveAspectRatio,
    bool AutoResizeViewport);

/// <summary>
/// 实例设置响应 DTO。<br/>
/// DTO returned when reading instance settings.
/// </summary>
/// <param name="InstanceId">实例 ID。<br/>Instance id.</param>
/// <param name="InstanceName">实例名称。<br/>Instance name.</param>
/// <param name="UserDataDirectory">用户数据目录。<br/>User data directory.</param>
/// <param name="IsSelected">是否当前选中。<br/>Whether this instance is currently selected.</param>
/// <param name="Viewport">视口设置。<br/>Viewport settings.</param>
/// <param name="UserAgent">User-Agent 设置。<br/>User-Agent settings.</param>
public sealed record BrowserInstanceSettingsResponseDto(
    string InstanceId,
    string InstanceName,
    string? UserDataDirectory,
    bool IsSelected,
    BrowserInstanceSettingsViewportDto Viewport,
    BrowserInstanceSettingsUserAgentDto UserAgent);
