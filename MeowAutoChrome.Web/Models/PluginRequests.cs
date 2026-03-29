using System.Collections.Generic;

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
}
