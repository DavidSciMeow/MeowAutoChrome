using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MeowAutoChrome.Web.Models
{
    public class PluginControlRequest
    {
        public string PluginId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public Dictionary<string, string?> Arguments { get; set; } = new();
    }

    public class PluginRunRequest
    {
        public string PluginId { get; set; } = string.Empty;
        public string FunctionId { get; set; } = string.Empty;
        public Dictionary<string, string?> Arguments { get; set; } = new();
    }

    public class PluginLoadRequest
    {
        [Required]
        [RegularExpression(@"\S+", ErrorMessage = "path required")]
        public string Path { get; set; } = string.Empty;
    }

    public class PluginIdRequest
    {
        [Required]
        [RegularExpression(@"\S+", ErrorMessage = "pluginId required")]
        public string PluginId { get; set; } = string.Empty;
    }
}
