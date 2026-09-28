using WinPlus.Models;

namespace WinPlus.Services;

public sealed class AppState
{
    public static AppState Current { get; } = new();
    public const string AppVersion = "1.1.0";

    public ConfigStore ConfigStore { get; } = new();
    public ActionEngine Actions { get; } = new();
    public HotkeyService Hotkeys { get; } = new();
    public TouchpadService Touchpad { get; } = new();
    public ScreenshotService Screenshots { get; } = new();
    public MemoService Memo { get; } = new();
    public TrayIconService Tray { get; } = new();
    public DeviceService? Devices { get; private set; }

    public AppConfig Config => ConfigStore.Config;

    public bool RealExitRequested { get; set; }

    private readonly Dictionary<int, Action> _hotkeyMap = new();

    public void Init()
    {
        ConfigStore.Load();
        Memo.Load();
        try { Devices = new DeviceService(enableSink: true); } catch { Devices = null; }
        try
        {
            var devs = DeviceService.Enumerate();
            AppLogger.Write($"devices enumerated={devs.Count} usb={devs.Count(d => d.IsUsb)} ptp={Touchpad.PtpPresent}");
            foreach (var d in devs.Take(24))
                AppLogger.Write($"dev [{d.BusDisplay}] {d.DisplayName}");
        }
        catch (Exception ex) { AppLogger.Error("devices", ex); }
        Touchpad.RemapEnabled = true;
        Touchpad.Start();
        Touchpad.GestureDetected += OnGesture;
        Hotkeys.HotkeyPressed += id => { if (_hotkeyMap.TryGetValue(id, out var a)) { try { a(); } catch { } } };
        RebuildHotkeys();
        Actions.ScreenshotRequested += name => ScreenshotRequested?.Invoke(name);
        Actions.MemoRequested += () => MemoRequested?.Invoke();
    }

    public event Action<string>? ScreenshotRequested;
    public event Action? MemoRequested;

    private void OnGesture(string gesture)
    {
        var b = Config.Gestures.FirstOrDefault(g => g.Enabled && g.Gesture == gesture);
        if (b is not null) Actions.Execute(b.Action);
    }

    public void RebuildHotkeys()
    {
        Hotkeys.Clear();
        _hotkeyMap.Clear();
        var map = _hotkeyMap;
        int osCount = 0, hookCount = 0;
        void AddTrigger(string spec, Action action)
        {
            bool forceHook = HotkeySpec.TryParse(spec, out _, out var snapVk) && snapVk == 0x2C;
            var e = forceHook ? null : Hotkeys.Register(spec);
            if (e is null) { Hotkeys.RegisterLlFallback(spec, () => { action(); return Task.CompletedTask; }); hookCount++; return; }
            if (e.RegisteredByOs) { map[e.Id] = action; osCount++; }
            else { Hotkeys.RegisterLlFallback(spec, () => { action(); return Task.CompletedTask; }); hookCount++; }
        }
        foreach (var s in Config.Shortcuts.Where(s => s.Enabled))
        {
            var act = s.Action;
            AddTrigger(s.Hotkey, () => Actions.Execute(act));
        }
        foreach (var p in Config.ShotProfiles)
        {
            var prof = p;
            AddTrigger(prof.Hotkey, () => ScreenshotRequested?.Invoke(prof.Name));
        }
        var memo = Config.Memo;
        AddTrigger(memo.Hotkey, () => MemoRequested?.Invoke());
        foreach (var r in Config.DeviceRules.Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.Trigger)))
        {
            var rule = r;
            AddTrigger(rule.Trigger, () => Actions.Execute(rule.Action));
        }
        AppLogger.Write($"hotkeys rebuilt os={osCount} hook={hookCount}");
    }

    public void Save() => ConfigStore.Save();
}
