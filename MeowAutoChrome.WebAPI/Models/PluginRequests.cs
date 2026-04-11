using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 插件控制请求。<br/>
/// Request used to execute a plugin control command.
/// </summary>
public class PluginControlRequest
{
    /// <summary>
    /// 插件 ID。<br/>
    /// Plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// 控制命令。<br/>
    /// Control command.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 命令参数。<br/>
    /// Command arguments.
    /// </summary>
    public Dictionary<string, string?> Arguments { get; set; } = new();
}

/// <summary>
/// 插件函数调用请求。<br/>
/// Request used to invoke a plugin function.
/// </summary>
public class PluginRunRequest
{
    /// <summary>
    /// 插件 ID。<br/>
    /// Plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// 函数 ID。<br/>
    /// Function identifier.
    /// </summary>
    public string FunctionId { get; set; } = string.Empty;

    /// <summary>
    /// 函数参数。<br/>
    /// Function arguments.
    /// </summary>
    public Dictionary<string, string?> Arguments { get; set; } = new();
}

/// <summary>
/// 插件加载请求。<br/>
/// Request used to load a plugin assembly from a path.
/// </summary>
public class PluginLoadRequest
{
    /// <summary>
    /// 插件程序集路径。<br/>
    /// Plugin assembly path.
    /// </summary>
    [Required]
    [RegularExpression(@"\S+", ErrorMessage = "path required")]
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// 插件标识请求。<br/>
/// Request containing a plugin identifier.
/// </summary>
public class PluginIdRequest
{
    /// <summary>
    /// 插件 ID。<br/>
    /// Plugin identifier.
    /// </summary>
    [Required]
    [RegularExpression(@"\S+", ErrorMessage = "pluginId required")]
    public string PluginId { get; set; } = string.Empty;
}

/// <summary>
/// 插件程序集启用状态切换请求。<br/>
/// Request used to toggle one plugin assembly on or off.
/// </summary>
public class PluginAssemblyStateRequest
{
    /// <summary>
    /// 插件程序集路径。<br/>
    /// Plugin assembly path.
    /// </summary>
    [Required]
    [RegularExpression(@"\S+", ErrorMessage = "assemblyPath required")]
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用该程序集。<br/>
    /// Whether the assembly should be enabled.
    /// </summary>
    public bool Enabled { get; set; }
}
