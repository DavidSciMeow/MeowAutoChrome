namespace MeowAutoChrome.WebAPI.Models;

/// <summary>
/// 鼠标事件 DTO。<br/>
/// DTO representing a mouse event.
/// </summary>
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
/// 键盘事件 DTO。<br/>
/// DTO representing a keyboard event.
/// </summary>
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
