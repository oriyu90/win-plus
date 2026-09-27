using System.Runtime.InteropServices;
using WinPlus.Services.Interop;

namespace WinPlus.Services;

public sealed class HotkeyEntry
{
    public int Id { get; init; }
    public string Spec { get; init; } = "";
    public uint Mods { get; init; }
    public uint Vk { get; init; }
    public bool RegisteredByOs { get; set; }
}

public sealed class HotkeyService : IDisposable
{
    private readonly MessageWindow _window = new();
    private readonly Win32.HookProc _hookProc;
    private nint _hook;
    private readonly Dictionary<int, HotkeyEntry> _entries = new();
    private readonly Dictionary<string, List<Func<Task>>> _llHandlers = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;
    private bool _disposed;

    public event Action<int>? HotkeyPressed;

    public HotkeyService()
    {
        _hookProc = LowLevelKeyboardProc;
        _window.MessageReceived += OnMessage;
    }

    private void OnMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg == Win32.WM_HOTKEY)
            HotkeyPressed?.Invoke(wParam.ToInt32());
    }

    public HotkeyEntry? Register(string spec)
    {
        if (!HotkeySpec.TryParse(spec, out var mods, out var vk)) return null;
        int id = _nextId++;
        var entry = new HotkeyEntry { Id = id, Spec = spec, Mods = mods, Vk = vk };
        var hwnd = _window.Handle;
        int err = -1;
        bool ok = false;
        try
        {
            var res = _window.InvokeAsync(() =>
            {
                bool r;
                try { r = Win32.RegisterHotKey(hwnd, id, mods, vk); }
                catch { r = false; }
                return (object)(r ? 0 : Marshal.GetLastWin32Error());
            }).GetAwaiter().GetResult();
            err = res is int e ? e : -1;
            ok = err == 0;
        }
        catch { ok = false; }
        entry.RegisteredByOs = ok;
        _entries[id] = entry;
        if (!ok)
        {
            try { AppLogger.Write($"hotkey '{spec}' os-register failed err={err}"); } catch { }
            EnsureHook();
        }
        return entry;
    }

    public void RegisterLlFallback(string spec, Func<Task> handler)
    {
        if (!_llHandlers.TryGetValue(spec, out var list))
        {
            list = new List<Func<Task>>();
            _llHandlers[spec] = list;
        }
        list.Add(handler);
        EnsureHook();
    }

    public void Unregister(int id)
    {
        if (_entries.Remove(id, out _))
        {
            try
            {
                var hwnd = _window.Handle;
                _window.InvokeAsync(() =>
                {
                    try { Win32.UnregisterHotKey(hwnd, id); } catch { }
                    return (object)0;
                }).GetAwaiter().GetResult();
            }
            catch { }
        }
        if (_entries.Count == 0 && _llHandlers.Count == 0) RemoveHook();
    }

    public void Clear()
    {
        foreach (var id in _entries.Keys.ToArray()) Unregister(id);
        _llHandlers.Clear();
        RemoveHook();
    }

    public IReadOnlyDictionary<int, HotkeyEntry> Entries => _entries;

    private void EnsureHook()
    {
        if (_hook != nint.Zero) return;
        try { _hook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _hookProc, Win32.GetModuleHandle(null), 0); }
        catch { _hook = nint.Zero; }
        try { AppLogger.Write($"llhook installed={_hook:x}"); } catch { }
    }

    private void RemoveHook()
    {
        if (_hook != nint.Zero)
        {
            try { Win32.UnhookWindowsHookEx(_hook); } catch { }
            _hook = nint.Zero;
        }
    }

    private nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == Win32.WM_KEYDOWN || wParam == Win32.WM_SYSKEYDOWN))
        {
            try
            {
                var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                bool ctrl = Win32.IsKeyDown(0x11) || Win32.IsKeyDown(0xA2) || Win32.IsKeyDown(0xA3);
                bool shift = Win32.IsKeyDown(0x10) || Win32.IsKeyDown(0xA0) || Win32.IsKeyDown(0xA1);
                bool alt = Win32.IsKeyDown(0x12) || Win32.IsKeyDown(0xA4) || Win32.IsKeyDown(0xA5);
                bool win = Win32.IsKeyDown(0x5B) || Win32.IsKeyDown(0x5C);
                uint mods = 0;
                if (ctrl) mods |= Win32.MOD_CONTROL;
                if (shift) mods |= Win32.MOD_SHIFT;
                if (alt) mods |= Win32.MOD_ALT;
                if (win) mods |= Win32.MOD_WIN;
                bool swallow = false;
                foreach (var e in _entries.Values)
                {
                    if (!e.RegisteredByOs && e.Vk == kb.vkCode && e.Mods == mods)
                    {
                        HotkeyPressed?.Invoke(e.Id);
                        swallow = true;
                    }
                }
                foreach (var kv in _llHandlers)
                {
                    if (HotkeySpec.TryParse(kv.Key, out var m, out var v) && v == kb.vkCode && m == mods)
                    {
                        foreach (var h in kv.Value) _ = Task.Run(h);
                        swallow = true;
                    }
                }
                if (swallow) return 1;
            }
            catch { }
        }
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        _window.Dispose();
    }
}
