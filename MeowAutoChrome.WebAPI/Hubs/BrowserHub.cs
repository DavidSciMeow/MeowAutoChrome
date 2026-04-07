using MeowAutoChrome.WebAPI.Models;
using MeowAutoChrome.Contracts.SignalR;
using Microsoft.AspNetCore.SignalR;
using MeowAutoChrome.WebAPI.Services;
using MeowAutoChrome.Core.Services;

namespace MeowAutoChrome.WebAPI.Hubs;

/// <summary>
/// 浏览器实时通信 Hub，负责输入事件转发和推流连接生命周期。<br/>
/// Browser realtime communication hub responsible for input forwarding and screencast connection lifecycle.
/// </summary>
public class BrowserHub(BrowserInstanceManager browserInstances, ScreencastServiceCore screencastCore) : Hub<IBrowserClient>
{

    /// <summary>
    /// 当前连接对应的浏览器实例信息。<br/>
    /// Browser instance information associated with the current connection.
    /// </summary>
    public BrowserInstanceInfoDto? Client
    {
        get
        {
            var instances = browserInstances.GetInstancesDto();
            var id = browserInstances.CurrentInstanceId;
            return instances.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 处理客户端连接建立。<br/>
    /// Handle client connection establishment.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await screencastCore.OnClientConnectedAsync();
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 处理客户端连接断开。<br/>
    /// Handle client disconnection.
    /// </summary>
    /// <param name="exception">断开时的异常。<br/>Exception associated with the disconnect event.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await screencastCore.OnClientDisconnectedAsync();
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 接收并转发鼠标事件到当前浏览器实例。<br/>
    /// Receive and forward mouse events to the current browser instance.
    /// </summary>
    /// <param name="data">鼠标事件数据。<br/>Mouse event payload.</param>
    public async Task SendMouseEvent(MouseEventData data)
    {
        var payload = new Dictionary<string, object>
        {
            ["type"] = data.Type,
            ["x"] = data.X,
            ["y"] = data.Y,
            ["button"] = data.Button,
            ["buttons"] = data.Buttons,
            ["clickCount"] = data.ClickCount,
            ["modifiers"] = data.Modifiers,
        };

        if (data.DeltaX.HasValue)
            payload["deltaX"] = data.DeltaX.Value;

        if (data.DeltaY.HasValue)
            payload["deltaY"] = data.DeltaY.Value;

        await screencastCore.DispatchMouseEventAsync(payload);
    }

    /// <summary>
    /// 接收并转发键盘事件到当前浏览器实例。<br/>
    /// Receive and forward keyboard events to the current browser instance.
    /// </summary>
    /// <param name="data">键盘事件数据。<br/>Keyboard event payload.</param>
    public async Task SendKeyEvent(KeyEventData data)
        => await screencastCore.DispatchKeyEventAsync(new Dictionary<string, object>
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
