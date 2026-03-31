namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 浏览器插件动作参数的单个选项 DTO（值与显示标签）。<br/>
/// DTO for a single option of a browser plugin action parameter (value and display label).
/// </summary>
/// <param name="Value">选项值。<br/>Option value.</param>
/// <param name="Label">显示标签。<br/>Display label.</param>
public sealed record BrowserPluginActionParameterOptionDto(string Value, string Label);

/// <summary>
/// 描述插件动作参数的 DTO，包括名称、标签、默认值和可选项。<br/>
/// DTO describing a plugin action parameter including name, label, default value and options.
/// </summary>
/// <param name="Name">参数名（键）。<br/>Parameter name (key).</param>
/// <param name="Label">显示标签。<br/>Display label.</param>
/// <param name="Description">参数描述（可空）。<br/>Parameter description (nullable).</param>
/// <param name="DefaultValue">默认值（可空）。<br/>Default value (nullable).</param>
/// <param name="Required">是否必填。<br/>Whether the parameter is required.</param>
/// <param name="InputType">输入类型提示。<br/>Input type hint.</param>
/// <param name="Options">可选项列表。<br/>List of options.</param>
public sealed record BrowserPluginActionParameterDto(
    string Name,
    string Label,
    string? Description,
    string? DefaultValue,
    bool Required,
    string InputType,
    IReadOnlyList<BrowserPluginActionParameterOptionDto> Options);

/// <summary>
/// 表示插件控制项的 DTO（例如一个按钮命令），包含名称与参数列表。<br/>
/// DTO representing a plugin control (e.g. a command button), including name and parameters.
/// </summary>
/// <param name="Command">控制命令标识。<br/>Control command identifier.</param>
/// <param name="Name">显示名称。<br/>Display name.</param>
/// <param name="Description">描述（可空）。<br/>Description (nullable).</param>
/// <param name="Parameters">参数描述列表。<br/>List of parameter descriptors.</param>
public sealed record BrowserPluginControlDto(string Command, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDto> Parameters);

/// <summary>
/// 表示一个插件函数/动作的 DTO（Id、名称与参数）。<br/>
/// DTO representing a plugin function/action (id, name and parameters).
/// </summary>
/// <param name="Id">动作 Id。<br/>Action id.</param>
/// <param name="Name">动作名称。<br/>Action name.</param>
/// <param name="Description">动作描述（可空）。<br/>Action description (nullable).</param>
/// <param name="Parameters">参数描述列表。<br/>List of parameter descriptors.</param>
public sealed record BrowserPluginFunctionDto(string Id, string Name, string? Description, IReadOnlyList<BrowserPluginActionParameterDto> Parameters);

/// <summary>
/// 插件描述信息 DTO，用于在清单中展示插件的元数据与运行状态。<br/>
/// Plugin descriptor DTO used in catalogs to show plugin metadata and runtime state.
/// </summary>
/// <param name="Id">插件 Id。<br/>Plugin id.</param>
/// <param name="Name">插件名称。<br/>Plugin name.</param>
/// <param name="Description">插件描述（可空）。<br/>Plugin description (nullable).</param>
/// <param name="State">运行状态字符串。<br/>Runtime state string.</param>
/// <param name="SupportsPause">是否支持暂停。<br/>Whether pause is supported.</param>
/// <param name="Controls">控制列表。<br/>List of controls.</param>
/// <param name="Functions">函数/动作列表。<br/>List of functions/actions.</param>
public sealed record BrowserPluginDescriptorDto(
    string Id,
    string Name,
    string? Description,
    string State,
    bool SupportsPause,
    IReadOnlyList<BrowserPluginControlDto> Controls,
    IReadOnlyList<BrowserPluginFunctionDto> Functions);

/// <summary>
/// 插件错误描述 DTO，包含程序集、摘要与详细信息。<br/>
/// Plugin error descriptor DTO containing assembly, summary and detail.
/// </summary>
/// <param name="Assembly">出错的程序集名称（可空）。<br/>Assembly name where error occurred (nullable).</param>
/// <param name="Summary">错误摘要。<br/>Error summary.</param>
/// <param name="Detail">详细错误信息。<br/>Detailed error information.</param>
public sealed record BrowserPluginErrorDescriptorDto(string? Assembly, string Summary, string Detail);

/// <summary>
/// 插件目录响应 DTO，包含插件列表与错误信息。<br/>
/// Plugin catalog response DTO containing plugin list and errors.
/// </summary>
/// <param name="Plugins">插件描述符数组（可空项）。<br/>Plugin descriptors array (nullable items allowed).</param>
/// <param name="Errors">错误消息列表。<br/>List of error messages.</param>
/// <param name="ErrorsDetailed">详细错误描述列表。<br/>List of detailed error descriptors.</param>
public sealed record BrowserPluginCatalogResponseDto(IReadOnlyList<BrowserPluginDescriptorDto?> Plugins, IReadOnlyList<string> Errors, IReadOnlyList<BrowserPluginErrorDescriptorDto> ErrorsDetailed);
