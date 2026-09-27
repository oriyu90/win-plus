using WinPlus.Models;
using WinPlus.Services;

namespace WinPlus.Views;

internal sealed record ActionPreset(string Type, string Label, string Payload, bool Fixed);

internal static class ActionOptions
{
    public static List<ActionPreset> All => new()
    {
        new("none", S.T("Action_None"), "", true),
        new("taskView", S.T("Action_TaskView"), "", true),
        new("windowList", S.T("Action_WindowList"), "", true),
        new("altTab", S.T("Action_AltTab"), "", true),
        new("vdeskLeft", S.T("Action_VDeskLeft"), "", true),
        new("vdeskRight", S.T("Action_VDeskRight"), "", true),
        new("showDesktop", S.T("Action_ShowDesktop"), "", true),
        new("maximize", S.T("Action_Maximize"), "", true),
        new("minimize", S.T("Action_Minimize"), "", true),
        new("closeWindow", S.T("Action_CloseWindow"), "", true),
        new("back", S.T("Action_Back"), "", true),
        new("forward", S.T("Action_Forward"), "", true),
        new("nextPage", S.T("Action_NextPage"), "", true),
        new("prevPage", S.T("Action_PrevPage"), "", true),
        new("search", S.T("Action_Search"), "", true),
        new("explorer", S.T("Action_Explorer"), "", true),
        new("terminal", S.T("Action_Terminal"), "", true),
        new("calc", S.T("Action_Calc"), "", true),
        new("taskmgr", S.T("Action_TaskMgr"), "", true),
        new("lock", S.T("Action_Lock"), "", true),
        new("playPause", S.T("Action_PlayPause"), "", true),
        new("nextTrack", S.T("Action_NextTrack"), "", true),
        new("prevTrack", S.T("Action_PrevTrack"), "", true),
        new("volUp", S.T("Action_VolUp"), "", true),
        new("volDown", S.T("Action_VolDown"), "", true),
        new("f11", S.T("Action_F11"), "", true),
        new("copy", S.T("Action_Copy"), "", true),
        new("paste", S.T("Action_Paste"), "", true),
        new("undo", S.T("Action_Undo"), "", true),
        new("memo", S.T("Action_Memo"), "", true),
        new("typeText", S.T("Action_TypeText"), "", false),
        new("sendKeys", S.T("Action_SendKeys"), "", false),
        new("launch", S.T("Action_Launch"), "", false),
        new("screenshot", S.T("Action_Screenshot"), "", false),
    };

    public static bool NeedsPayload(ActionPreset preset) => !preset.Fixed;

    public static string PayloadHint(string type) => type switch
    {
        "typeText" => S.T("ActionHint_TypeText"),
        "sendKeys" => S.T("ActionHint_SendKeys"),
        "launch" => S.T("ActionHint_Launch"),
        "screenshot" => S.T("ActionHint_Screenshot"),
        _ => "",
    };

    public static AppAction BuildPreset(ActionPreset preset, string freePayload) => preset.Type switch
    {
        "taskView" or "windowList" => AppAction.TaskView(),
        "altTab" => AppAction.AltTab(),
        "vdeskLeft" => AppAction.VDeskLeft(),
        "vdeskRight" => AppAction.VDeskRight(),
        "showDesktop" => AppAction.SendKeys("Win+D"),
        "maximize" => AppAction.SendKeys("Win+Up"),
        "minimize" => AppAction.SendKeys("Win+Down"),
        "closeWindow" => AppAction.SendKeys("Alt+F4"),
        "back" => AppAction.SendKeys("BrowserBack"),
        "forward" => AppAction.SendKeys("BrowserForward"),
        "nextPage" => AppAction.SendKeys("PageDown"),
        "prevPage" => AppAction.SendKeys("PageUp"),
        "search" => AppAction.SendKeys("Win+S"),
        "explorer" => AppAction.SendKeys("Win+E"),
        "terminal" => AppAction.Launch("wt.exe"),
        "calc" => AppAction.Launch("calc.exe"),
        "taskmgr" => AppAction.SendKeys("Ctrl+Shift+Esc"),
        "lock" => AppAction.SendKeys("Win+L"),
        "playPause" => AppAction.SendKeys("MediaPlayPause"),
        "nextTrack" => AppAction.SendKeys("MediaNext"),
        "prevTrack" => AppAction.SendKeys("MediaPrev"),
        "volUp" => AppAction.SendKeys("VolumeUp"),
        "volDown" => AppAction.SendKeys("VolumeDown"),
        "f11" => AppAction.SendKeys("F11"),
        "copy" => AppAction.SendKeys("Ctrl+C"),
        "paste" => AppAction.SendKeys("Ctrl+V"),
        "undo" => AppAction.SendKeys("Ctrl+Z"),
        "memo" => AppAction.Memo(),
        "typeText" => AppAction.TypeText(freePayload),
        "sendKeys" => AppAction.SendKeys(freePayload),
        "launch" => AppAction.Launch(freePayload),
        "screenshot" => AppAction.Screenshot(freePayload),
        _ => AppAction.None(),
    };

    public static AppAction Build(string type, string payload)
    {
        var preset = All.FirstOrDefault(p => p.Type == type);
        if (preset is not null && preset.Fixed) return BuildPreset(preset, "");
        return BuildPreset(new ActionPreset(type, "", payload, false), payload);
    }
}
