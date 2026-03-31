namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 表示来自客户端的鼠标事件数据。<br/>
/// Represents mouse event data coming from the client.
/// </summary>
/// <param name="Type">事件类型："mouseMoved" | "mousePressed" | "mouseReleased" | "mouseWheel"。<br/>Event type: "mouseMoved" | "mousePressed" | "mouseReleased" | "mouseWheel".</param>
/// <param name="X">事件发生时的 X 坐标（相对于页面或可视区域）。<br/>X coordinate at the time of the event (relative to the page or viewport).</param>
/// <param name="Y">事件发生时的 Y 坐标（相对于页面或可视区域）。<br/>Y coordinate at the time of the event (relative to the page or viewport).</param>
/// <param name="Button">触发事件的按钮："none" | "left" | "middle" | "right"。<br/>Button that triggered the event: "none" | "left" | "middle" | "right".</param>
/// <param name="Buttons">当前被按住的按钮位掩码（整数）。<br/>Bitmask of buttons currently pressed (integer).</param>
/// <param name="ClickCount">点击次数（用于双击等场景）。<br/>Number of clicks (used for double-clicks etc.).</param>
/// <param name="Modifiers">修饰键位掩码：Alt=1 Ctrl=2 Meta=4 Shift=8。<br/>Modifier bitmask: Alt=1 Ctrl=2 Meta=4 Shift=8.</param>
/// <param name="DeltaX">对于滚轮事件的 X 方向增量（如果适用）。<br/>Delta in X direction for wheel events (if applicable).</param>
/// <param name="DeltaY">对于滚轮事件的 Y 方向增量（如果适用）。<br/>Delta in Y direction for wheel events (if applicable).</param>
public record MouseEventData(
    string Type,
    double X,
    double Y,
    string Button,
    int Buttons,
    int ClickCount,
    int Modifiers,
    double? DeltaX,
    double? DeltaY
);

/// <summary>
/// 表示来自客户端的键盘事件数据。<br/>
/// Represents keyboard event data coming from the client.
/// </summary>
/// <param name="Type">事件类型："rawKeyDown" | "keyUp" | "char"。<br/>Event type: "rawKeyDown" | "keyUp" | "char".</param>
/// <param name="Key">逻辑按键名称，例如 "Enter"、"a"。<br/>Logical key name, e.g. "Enter", "a".</param>
/// <param name="Code">物理按键代码，例如 "KeyA"、"Enter"。<br/>Physical key code, e.g. "KeyA", "Enter".</param>
/// <param name="Text">可打印文本，仅在 char 事件时有值。<br/>Printable text, present only for char events.</param>
/// <param name="Modifiers">修饰键位掩码：Alt=1 Ctrl=2 Meta=4 Shift=8。<br/>Modifier bitmask: Alt=1 Ctrl=2 Meta=4 Shift=8.</param>
/// <param name="WindowsVirtualKeyCode">Windows 虚拟键码（整数）。<br/>Windows virtual key code (integer).</param>
/// <param name="NativeVirtualKeyCode">本地平台的原生虚拟键码（整数）。<br/>Native virtual key code of the platform (integer).</param>
/// <param name="AutoRepeat">是否为按键重复事件（按住时产生的重复）。<br/>Whether this is an auto-repeat key event (when key is held down).</param>
/// <param name="IsKeypad">是否来自小键盘区域。<br/>Whether the key is from the keypad area.</param>
/// <param name="IsSystemKey">是否为系统键（例如 Alt+某些组合）。<br/>Whether this is a system key (e.g. Alt+ combinations).</param>
public record KeyEventData(
    string Type,
    string Key,
    string Code,
    string? Text,
    int Modifiers,
    int WindowsVirtualKeyCode,
    int NativeVirtualKeyCode,
    bool AutoRepeat,
    bool IsKeypad,
    bool IsSystemKey
);
