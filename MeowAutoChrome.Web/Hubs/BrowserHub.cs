using MeowAutoChrome.Web.Models;
using MeowAutoChrome.Contracts.SignalR;
using Microsoft.AspNetCore.SignalR;

namespace MeowAutoChrome.Web.Hubs;

/// <summary>
/// BrowserHub 是一个 SignalR Hub，用于在浏览器客户端和服务端之间转发输入事件（鼠标与键盘）并管理客户端连接生命周期。
/// </summary>
/// <param name="client">Playwright 封装器，用于与浏览器进行交互（通过依赖注入提供）。</param>
/// <param name="screencast">屏幕投影服务，负责处理事件分发和连接管理（通过依赖注入提供）。</param>
public class BrowserHub : Hub<IBrowserClient>
{
    private readonly Services.BrowserInstanceManager _browserInstances;
    private readonly Core.Services.ScreencastServiceCore _screencastCore;

    public BrowserHub(Services.BrowserInstanceManager browserInstances, Core.Services.ScreencastServiceCore screencastCore)
    {
        _browserInstances = browserInstances;
        _screencastCore = screencastCore;
    }
    /// <summary>
    /// Playwright 封装器的当前实例（由 BrowserInstanceManager 提供）。
    /// 可能为 null（当尚未创建任何实例时）。
    /// 保持为 public 以兼容现有 API。
    /// </summary>
    public BrowserInstanceInfoDto? Client
    {
        get
        {
            var instances = _browserInstances.GetInstancesDto();
            var id = _browserInstances.CurrentInstanceId;
            return instances.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 当 SignalR 客户端连接时调用，通知 ScreencastService 处理连接逻辑。
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await _screencastCore.OnClientConnectedAsync();
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 当 SignalR 客户端断开连接时调用，通知 ScreencastService 处理断开逻辑。
    /// </summary>
    /// <param name="exception">断开时的异常（如果有）。</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _screencastCore.OnClientDisconnectedAsync();
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 接收来自客户端的鼠标事件数据并转发给 ScreencastService 进行分发到浏览器。
    /// </summary>
    /// <param name="data">鼠标事件的数据模型。</param>
    public async Task SendMouseEvent(MouseEventData data)
        => await _screencastCore.DispatchMouseEventAsync(new Dictionary<string, object>
        {
            ["type"] = data.Type,
            ["x"] = data.X,
            ["y"] = data.Y,
            ["button"] = data.Button,
            ["buttons"] = data.Buttons,
            ["clickCount"] = data.ClickCount,
            ["modifiers"] = data.Modifiers,
            ["deltaX"] = data.DeltaX,
            ["deltaY"] = data.DeltaY,
        });

    /// <summary>
    /// 接收来自客户端的键盘事件数据并转发给 ScreencastService 进行分发到浏览器。
    /// </summary>
    /// <param name="data">键盘事件的数据模型。</param>
    public async Task SendKeyEvent(KeyEventData data)
        => await _screencastCore.DispatchKeyEventAsync(new Dictionary<string, object>
        {
            ["type"] = data.Type,
            ["key"] = data.Key,
            ["code"] = data.Code,
            ["text"] = data.Text ?? string.Empty,
            ["modifiers"] = data.Modifiers,
            ["windowsVirtualKeyCode"] = data.WindowsVirtualKeyCode,
            ["nativeVirtualKeyCode"] = data.NativeVirtualKeyCode,
            ["autoRepeat"] = data.AutoRepeat,
            ["isKeypad"] = data.IsKeypad,
            ["isSystemKey"] = data.IsSystemKey,
        });
}



