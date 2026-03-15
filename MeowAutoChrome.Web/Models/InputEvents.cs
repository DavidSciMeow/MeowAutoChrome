namespace MeowAutoChrome.Web.Models;

/// <summary>
/// 表示来自客户端的鼠标事件数据。
/// </summary>
/// <param name="Type">事件类型："mouseMoved" | "mousePressed" | "mouseReleased" | "mouseWheel"。</param>
/// <param name="X">事件发生时的 X 坐标（相对于页面或可视区域）。</param>
/// <param name="Y">事件发生时的 Y 坐标（相对于页面或可视区域）。</param>
/// <param name="Button">触发事件的按钮："none" | "left" | "middle" | "right"。</param>
/// <param name="Buttons">当前被按住的按钮位掩码（整数）。</param>
/// <param name="ClickCount">点击次数（用于双击等场景）。</param>
/// <param name="Modifiers">修饰键位掩码：Alt=1 Ctrl=2 Meta=4 Shift=8。</param>
/// <param name="DeltaX">对于滚轮事件的 X 方向增量（如果适用）。</param>
/// <param name="DeltaY">对于滚轮事件的 Y 方向增量（如果适用）。param>
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
/// 表示来自客户端的键盘事件数据。
/// </summary>
/// <param name="Type">事件类型："rawKeyDown" | "keyUp" | "char"。</param>
/// <param name="Key">逻辑按键名称，例如 "Enter"、"a"。</param>
/// <param name="Code">物理按键代码，例如 "KeyA"、"Enter"。</param>
/// <param name="Text">可打印文本，仅在 char 事件时有值。</param>
/// <param name="Modifiers">修饰键位掩码：Alt=1 Ctrl=2 Meta=4 Shift=8。</param>
/// <param name="WindowsVirtualKeyCode">Windows 虚拟键码（整数）。</param>
/// <param name="NativeVirtualKeyCode">本地平台的原生虚拟键码（整数）。</param>
/// <param name="AutoRepeat">是否为按键重复事件（按住时产生的重复）。</param>
/// <param name="IsKeypad">是否来自小键盘区域。</param>
/// <param name="IsSystemKey">是否为系统键（例如 Alt+某些组合）。</param>
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
