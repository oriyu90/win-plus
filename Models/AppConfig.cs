using System.Text.Json.Serialization;

namespace WinPlus.Models;

public sealed class AppAction
{
    public string Type { get; set; } = "none";
    public string Payload { get; set; } = "";

    public static AppAction None() => new() { Type = "none" };
    public static AppAction TaskView() => new() { Type = "taskView" };
    public static AppAction AltTab() => new() { Type = "altTab" };
    public static AppAction VDeskLeft() => new() { Type = "vdeskLeft" };
    public static AppAction VDeskRight() => new() { Type = "vdeskRight" };
    public static AppAction TypeText(string text) => new() { Type = "typeText", Payload = text };
    public static AppAction SendKeys(string keys) => new() { Type = "sendKeys", Payload = keys };
    public static AppAction Launch(string path) => new() { Type = "launch", Payload = path };
    public static AppAction Screenshot(string profile) => new() { Type = "screenshot", Payload = profile };
    public static AppAction Memo() => new() { Type = "memo" };

    public string Describe() => Type switch
    {
        "taskView" => Services.S.T("Action_TaskView"),
        "altTab" => Services.S.T("Action_AltTab"),
        "vdeskLeft" => Services.S.T("Action_VDeskLeft"),
        "vdeskRight" => Services.S.T("Action_VDeskRight"),
        "typeText" => $"{Services.S.T("Action_TypeText")}: {Trim(Payload)}",
        "sendKeys" => $"{Services.S.T("Action_SendKeys")}: {Payload}",
        "launch" => $"{Services.S.T("Action_Launch")}: {Payload}",
        "screenshot" => $"{Services.S.T("Action_Screenshot")}: {Payload}",
        "memo" => Services.S.T("Action_Memo"),
        _ => Services.S.T("Action_None"),
    };

    private static string Trim(string s) => s.Length > 24 ? s[..24] + "…" : s;
}

public sealed class GestureBinding
{
    public string Gesture { get; set; } = "";
    public AppAction Action { get; set; } = AppAction.None();
    public bool Enabled { get; set; } = true;
}

public sealed class ShortcutBinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Hotkey { get; set; } = "Win+U";
    public AppAction Action { get; set; } = AppAction.None();
    public bool Enabled { get; set; } = true;
    public string PayloadShort => Action.Payload.Length > 28 ? Action.Payload[..28] + "…" : Action.Payload;
    public string ActionLabel => Action.Describe();
}

public sealed class ScreenshotProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Hotkey { get; set; } = "PrintScreen";
    public string Scope { get; set; } = "Full";
    public string Folder { get; set; } = "";
    public string Format { get; set; } = "PNG";
    public int PreviewSeconds { get; set; } = 5;
    public bool IncludeCursor { get; set; } = true;
}

public sealed class DeviceRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Trigger { get; set; } = "";
    public AppAction Action { get; set; } = AppAction.None();
    public bool Enabled { get; set; } = true;
    public string ActionLabel => Action.Describe();
}

public sealed class MemoSettings
{
    public string Hotkey { get; set; } = "Win+Shift+M";
    public int AutoCloseSeconds { get; set; } = 6;
    public string Theme { get; set; } = "System";
    public MemoWindowBounds Bounds { get; set; } = new();
}

public sealed class MemoWindowBounds
{
    public bool Custom { get; set; } = false;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 460;
    public int Height { get; set; } = 420;
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public bool StartAtLogon { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool UsbOnlyFilter { get; set; } = false;
    public string Language { get; set; } = "System";
    public List<GestureBinding> Gestures { get; set; } = DefaultGestures();
    public List<ShortcutBinding> Shortcuts { get; set; } = new() { new ShortcutBinding() };
    public List<ScreenshotProfile> ShotProfiles { get; set; } = new() { new ScreenshotProfile() };
    public List<DeviceRule> DeviceRules { get; set; } = new();
    public MemoSettings Memo { get; set; } = new();

    public static List<GestureBinding> DefaultGestures() => new()
    {
        new GestureBinding { Gesture = "ThreeFingerTap", Action = AppAction.TaskView() },
        new GestureBinding { Gesture = "ThreeFingerUp", Action = AppAction.VDeskRight() },
        new GestureBinding { Gesture = "ThreeFingerDown", Action = AppAction.VDeskLeft() },
        new GestureBinding { Gesture = "ThreeFingerLeft", Action = AppAction.VDeskLeft() },
        new GestureBinding { Gesture = "ThreeFingerRight", Action = AppAction.VDeskRight() },
    };

    public static string GestureDisplayName(string g) => g switch
    {
        "ThreeFingerTap" => Services.S.T("Gesture_Tap"),
        "ThreeFingerUp" => Services.S.T("Gesture_Up"),
        "ThreeFingerDown" => Services.S.T("Gesture_Down"),
        "ThreeFingerLeft" => Services.S.T("Gesture_Left"),
        "ThreeFingerRight" => Services.S.T("Gesture_Right"),
        _ => g,
    };
}

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(List<ShortcutBinding>))]
internal partial class AppJsonContext : JsonSerializerContext { }
