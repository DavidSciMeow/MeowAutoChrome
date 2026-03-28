namespace MeowAutoChrome.Web.Models;

public sealed record BrowserPluginActionParameterOptionDto(string Value, string Label);

public sealed record BrowserPluginActionParameterDto(
    string Name,
    string Label,
    string? Description,
    string? DefaultValue,
    bool Required,
    string InputType,
    IReadOnlyList<BrowserPluginActionParameterOptionDto> Options);

public sealed record BrowserPluginControlDto(string Command, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDto> Parameters);

public sealed record BrowserPluginFunctionDto(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDto> Parameters);

public sealed record BrowserPluginDescriptorDto(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginControlDto> Controls,
    IReadOnlyList<BrowserPluginFunctionDto> Functions);

public sealed record BrowserPluginErrorDescriptorDto(string? Assembly, string Summary, string Detail);

public sealed record BrowserPluginCatalogResponseDto(IReadOnlyList<BrowserPluginDescriptorDto?> Plugins, IReadOnlyList<string> Errors, IReadOnlyList<BrowserPluginErrorDescriptorDto> ErrorsDetailed);
