namespace MeowAutoChrome.Web.Models;

public record MouseEventData(
    string Type,       // mouseMoved | mousePressed | mouseReleased | mouseWheel
    double X,
    double Y,
    string Button,     // none | left | middle | right
    int Buttons,       // bitmask of currently held buttons
    int ClickCount,
    int Modifiers,     // Alt=1 Ctrl=2 Meta=4 Shift=8
    double? DeltaX,
    double? DeltaY
);

public record KeyEventData(
    string Type,       // rawKeyDown | keyUp | char
    string Key,        // e.g. "Enter", "a"
    string Code,       // e.g. "KeyA", "Enter"
    string? Text,      // printable text (char event only)
    int Modifiers,
    int WindowsVirtualKeyCode,
    int NativeVirtualKeyCode,
    bool AutoRepeat,
    bool IsKeypad,
    bool IsSystemKey
);
