using System.Diagnostics;
using WinPlus.Models;
using WinPlus.Services.Interop;

namespace WinPlus.Services;

public sealed class ActionEngine
{
    public event Action<string>? ScreenshotRequested;
    public event Action? MemoRequested;

    public void Execute(AppAction action)
    {
        try
        {
            switch (action.Type)
            {
                case "taskView": SendCombo(Win32.VK_LWIN, Win32.VK_TAB); break;
                case "altTab": SendAltTab(); break;
                case "vdeskLeft": SendCombo(Win32.VK_LWIN, 0x11, Win32.VK_LEFT, Win32.VK_LWIN); break;
                case "vdeskRight": SendCombo(Win32.VK_LWIN, 0x11, Win32.VK_RIGHT, Win32.VK_LWIN); break;
                case "typeText": TypeUnicode(action.Payload); break;
                case "sendKeys": SendKeySequence(action.Payload); break;
                case "launch": Launch(action.Payload); break;
                case "screenshot": ScreenshotRequested?.Invoke(action.Payload); break;
                case "memo": MemoRequested?.Invoke(); break;
            }
        }
        catch { }
    }

    public static void KeyDown(byte vk)
    {
        var inp = new Win32.INPUT { type = Win32.INPUT_KEYBOARD, U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = vk } } };
        Win32.SendInput(1, new[] { inp }, Marshal_Size());
    }

    public static void KeyUp(byte vk)
    {
        var inp = new Win32.INPUT { type = Win32.INPUT_KEYBOARD, U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = Win32.KEYEVENTF_KEYUP } } };
        Win32.SendInput(1, new[] { inp }, Marshal_Size());
    }

    private static int Marshal_Size() => System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>();

    private static void SendCombo(params byte[] vks)
    {
        foreach (var vk in vks) KeyDown(vk);
        for (int i = vks.Length - 1; i >= 0; i--) KeyUp(vks[i]);
    }

    private static void SendAltTab()
    {
        KeyDown(0x12);
        KeyDown(Win32.VK_TAB);
        KeyUp(Win32.VK_TAB);
        KeyUp(0x12);
    }

    public static void TypeUnicode(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var list = new List<Win32.INPUT>(text.Length * 2);
        foreach (var ch in text)
        {
            list.Add(new Win32.INPUT { type = Win32.INPUT_KEYBOARD, U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = Win32.KEYEVENTF_UNICODE } } });
            list.Add(new Win32.INPUT { type = Win32.INPUT_KEYBOARD, U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP } } });
        }
        Win32.SendInput((uint)list.Count, list.ToArray(), Marshal_Size());
    }

    private static void SendKeySequence(string spec)
    {
        if (!HotkeySpec.TryParse(spec, out var mods, out var vk) || vk == 0) return;
        uint ext = IsExtendedKey(vk) ? Win32.KEYEVENTF_EXTENDEDKEY : 0;
        var held = new List<byte>();
        if ((mods & Win32.MOD_CONTROL) != 0) held.Add(0x11);
        if ((mods & Win32.MOD_SHIFT) != 0) held.Add(0x10);
        if ((mods & Win32.MOD_ALT) != 0) held.Add(0x12);
        if ((mods & Win32.MOD_WIN) != 0) held.Add(Win32.VK_LWIN);
        foreach (var h in held) KeyDown(h);
        KeyDownEx((byte)vk, ext); KeyUpEx((byte)vk, ext);
        held.Reverse();
        foreach (var h in held) KeyUp(h);
    }

    private static bool IsExtendedKey(uint vk) =>
        (vk is >= 0xA6 and <= 0xB7) || vk is 0x2C or 0x2D or 0x2E || vk is 0x5B or 0x5C;

    private static void KeyDownEx(byte vk, uint extraFlags)
    {
        var inp = new Win32.INPUT { type = Win32.INPUT_KEYBOARD, U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = extraFlags } } };
        Win32.SendInput(1, new[] { inp }, Marshal_Size());
    }

    private static void KeyUpEx(byte vk, uint extraFlags)
    {
        var inp = new Win32.INPUT { type = Win32.INPUT_KEYBOARD, U = new Win32.InputUnion { ki = new Win32.KEYBDINPUT { wVk = vk, dwFlags = Win32.KEYEVENTF_KEYUP | extraFlags } } };
        Win32.SendInput(1, new[] { inp }, Marshal_Size());
    }

    private static void Launch(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        target = target.Trim().Trim('"');
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            return;
        }
        if (!System.IO.File.Exists(target) && !System.IO.Directory.Exists(target)) return;
        var ext = System.IO.Path.GetExtension(target).ToLowerInvariant();
        if (ext is ".exe" or ".lnk" or "" || System.IO.Directory.Exists(target))
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
    }
}
