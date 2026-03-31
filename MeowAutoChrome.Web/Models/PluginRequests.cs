using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.Web.Models
{
    /// <summary>
    /// 请求对插件执行控制命令（例如 start/stop/pause/resume）。<br/>
    /// Request to send a control command to a plugin (e.g., start/stop/pause/resume).
    /// </summary>
    public class PluginControlRequest
    {
        /// <summary>
        /// 插件 Id。<br/>
        /// Plugin id.
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 控制命令。<br/>
        /// Control command.
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// 可选的键值参数字典。<br/>
        /// Optional key/value arguments dictionary.
        /// </summary>
        public Dictionary<string, string?> Arguments { get; set; } = new();
    }

    /// <summary>
    /// 请求执行插件动作（functionId），并附带可选参数。<br/>
    /// Request to execute a plugin action (functionId) with optional arguments.
    /// </summary>
    public class PluginRunRequest
    {
        /// <summary>
        /// 插件 Id。<br/>
        /// Plugin id.
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 动作/函数 Id。<br/>
        /// Action/function id.
        /// </summary>
        public string FunctionId { get; set; } = string.Empty;

        /// <summary>
        /// 可选的键值参数字典。<br/>
        /// Optional key/value arguments dictionary.
        /// </summary>
        public Dictionary<string, string?> Arguments { get; set; } = new();
    }

    /// <summary>
    /// 请求加载插件程序集的 DTO，包含路径并带有验证特性。<br/>
    /// DTO for requesting plugin assembly load, with validation attributes.
    /// </summary>
    public class PluginLoadRequest
    {
        /// <summary>
        /// 要加载的程序集文件路径（相对或绝对）。<br/>
        /// Path to the assembly to load (relative or absolute).
        /// </summary>
        [Required]
        [RegularExpression(@"\S+", ErrorMessage = "path required")]
        public string Path { get; set; } = string.Empty;
    }

    /// <summary>
    /// 只包含插件 Id 的请求 DTO（用于卸载等场景）。<br/>
    /// Request DTO containing only a plugin id (used for unload, etc.).
    /// </summary>
    public class PluginIdRequest
    {
        /// <summary>
        /// 要操作的插件 Id。<br/>
        /// Plugin id to operate on.
        /// </summary>
        [Required]
        [RegularExpression(@"\S+", ErrorMessage = "pluginId required")]
        public string PluginId { get; set; } = string.Empty;
    }
}
