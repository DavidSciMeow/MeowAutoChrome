using MeowAutoChrome.Web.Hubs;
// Core models should not be referenced directly by Web controllers; use Core through adapters and DTOs.
using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Web.Services;
using MeowAutoChrome.Core.Services.PluginHost;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.IO.Compression;
using System.Text.Json;

namespace MeowAutoChrome.Web.Controllers
{
    /// <summary>
    /// 提供浏览器相关的 HTTP API（导航、选项卡管理、Screencast 设置、插件控制等），被前端调用以控制后端浏览器实例。
    /// </summary>
    /// <param name="browserInstances">浏览器实例管理器</param>
    /// <param name="hub">SignalR Hub 上下文</param>
    /// <param name="screencastService">Screencast 服务</param>
    /// <param name="pluginHost">插件宿主</param>
    /// <param name="resourceMetricsService">资源监控服务</param>
    /// <param name="programSettingsService">程序设置服务</param>
public partial class BrowserController(BrowserInstanceManager browserInstances, IHubContext<BrowserHub> hub, Core.Services.ScreencastServiceCore screencastService, Core.Interface.IPluginHostCore pluginHost, Core.Services.ResourceMetricsService resourceMetricsService, Core.Interface.IProgramSettingsProvider programSettingsService) : Controller
    {
        /// <summary>
        /// 用于从请求头中传递 BrowserHub 连接 ID 的自定义 header 名称，允许插件输出定向发送到特定客户端（例如仅发送给发起控制请求的页面）。如果未提供该 header，则插件输出将发送给所有连接的客户端。
        /// </summary>
        private const string BrowserHubConnectionIdHeaderName = "X-BrowserHub-ConnectionId";

        /// <summary>
        /// SignalR Hub 上下文
        /// </summary>
        public IHubContext<BrowserHub> Hub { get; } = hub;

        /// <summary>
        /// 浏览器页面的索引视图。
        /// </summary>
        public IActionResult Index() => View();

        // Management endpoints were moved into `BrowserController.Management.cs` as a separate controller
        // to reduce the size of this type. See `BrowserManagementController` for status/instance/navigation APIs.
        /// <summary>
        /// 从请求头中读取可选的 BrowserHub 连接 ID（用于将插件输出仅发送给特定客户端）。
        /// </summary>
        private string? GetBrowserHubConnectionId()
        {
            if (!Request.Headers.TryGetValue(BrowserHubConnectionIdHeaderName, out var values)) return null;
            var connectionId = values.FirstOrDefault()?.Trim();
            return string.IsNullOrWhiteSpace(connectionId) ? null : connectionId;
        }
    }
}
