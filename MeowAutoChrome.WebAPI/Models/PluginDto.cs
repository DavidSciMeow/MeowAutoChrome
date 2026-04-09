namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 插件动作参数选项 DTO。<br/>
/// DTO representing one selectable option for a plugin action parameter.
/// </summary>
/// <param name="Value">选项值。<br/>Option value.</param>
/// <param name="Label">选项显示名。<br/>Option label.</param>
public sealed record BrowserPluginActionParameterOptionDto(string Value, string Label);

/// <summary>
/// 插件动作参数 DTO。<br/>
/// DTO describing a plugin action parameter.
/// </summary>
public sealed record BrowserPluginActionParameterDto(
    string Name,
    string Label,
    string? Description,
    string? DefaultValue,
    bool Required,
    string InputType,
    IReadOnlyList<BrowserPluginActionParameterOptionDto> Options);

/// <summary>
/// 插件控制命令 DTO。<br/>
/// DTO describing a plugin control command.
/// </summary>
/// <param name="Command">控制命令标识。<br/>Control command identifier.</param>
/// <param name="Name">显示名称。<br/>Display name.</param>
/// <param name="Description">描述。<br/>Description.</param>
/// <param name="Parameters">参数列表。<br/>Parameter list.</param>
public sealed record BrowserPluginControlDto(string Command, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDto> Parameters);

/// <summary>
/// 插件函数 DTO。<br/>
/// DTO describing a plugin function.
/// </summary>
/// <param name="Id">函数标识。<br/>Function identifier.</param>
/// <param name="Name">显示名称。<br/>Display name.</param>
/// <param name="Description">描述。<br/>Description.</param>
/// <param name="Parameters">参数列表。<br/>Parameter list.</param>
public sealed record BrowserPluginFunctionDto(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDto> Parameters);

/// <summary>
/// 插件描述 DTO。<br/>
/// DTO describing one discovered plugin.
/// </summary>
public sealed record BrowserPluginDescriptorDto(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginControlDto> Controls,
    IReadOnlyList<BrowserPluginFunctionDto> Functions);

/// <summary>
/// 插件扫描错误 DTO。<br/>
/// DTO describing one plugin scan error.
/// </summary>
/// <param name="Assembly">相关程序集路径。<br/>Related assembly path.</param>
/// <param name="Summary">错误摘要。<br/>Error summary.</param>
/// <param name="Detail">错误详情。<br/>Error detail.</param>
public sealed record BrowserPluginErrorDescriptorDto(string? Assembly, string Summary, string Detail);

/// <summary>
/// 插件目录响应 DTO。<br/>
/// DTO representing the plugin catalog response.
/// </summary>
/// <param name="Plugins">插件列表。<br/>Plugin list.</param>
/// <param name="Errors">简要错误列表。<br/>Simple error list.</param>
/// <param name="ErrorsDetailed">详细错误列表。<br/>Detailed error list.</param>
public sealed record BrowserPluginCatalogResponseDto(IReadOnlyList<BrowserPluginDescriptorDto?> Plugins, IReadOnlyList<string> Errors, IReadOnlyList<BrowserPluginErrorDescriptorDto> ErrorsDetailed);
