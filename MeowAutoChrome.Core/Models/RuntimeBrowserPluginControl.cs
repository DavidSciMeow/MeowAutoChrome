namespace MeowAutoChrome.Core.Models;

/// <summary>
/// 运行时插件控制（命令）元数据，包含命令标识、名称与参数。<br/>
/// Metadata for a runtime plugin control/command including id, name and parameters.
/// </summary>
public sealed class RuntimeBrowserPluginControl
{
    /// <summary>
    /// 控制命令标识。<br/>
    /// Control command identifier.
    /// </summary>
    public string Command { get; }
    /// <summary>
    /// 控制的显示名称。<br/>
    /// Display name of the control.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// 控制描述（可空）。<br/>
    /// Control description (nullable).
    /// </summary>
    public string? Description { get; }
    /// <summary>
    /// 参数描述数组。<br/>
    /// Array of parameter descriptors.
    /// </summary>
    public RuntimeBrowserPluginParameter[] Parameters { get; }

    /// <summary>
    /// 构造运行时插件控制（命令）元数据实例。<br/>
    /// Construct a runtime plugin control/command metadata instance.
    /// </summary>
    /// <param name="command">控制命令标识 / control command identifier.</param>
    /// <param name="name">显示名称 / display name of the control.</param>
    /// <param name="description">控制描述（可空）/ control description (nullable).</param>
    /// <param name="parameters">参数描述数组 / array of parameter descriptors.</param>
    public RuntimeBrowserPluginControl(string command, string name, string? description, RuntimeBrowserPluginParameter[] parameters)
    {
        Command = command;
        Name = name;
        Description = description;
        Parameters = parameters;
    }
}
