using WinPlus.Services.Interop;

namespace WinPlus.Services;

public static class HotkeySpec
{
    public static bool TryParse(string spec, out uint mods, out uint vk)
    {
        mods = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(spec)) return false;
        var tokens = spec.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? key = null;
        foreach (var t in tokens)
        {
            var u = t.ToUpperInvariant();
            if (u is "CTRL" or "CONTROL") mods |= Win32.MOD_CONTROL;
            else if (u is "ALT" or "MENU") mods |= Win32.MOD_ALT;
            else if (u is "SHIFT") mods |= Win32.MOD_SHIFT;
            else if (u is "WIN" or "WINDOWS" or "LOGO" or "META") mods |= Win32.MOD_WIN;
            else key = t;
        }
        if (key is null) return false;
        vk = KeyTokenToVk(key);
        return vk != 0;
    }

    public static uint KeyTokenToVk(string key)
    {
        var u = key.ToUpperInvariant();
        if (u.Length == 1)
        {
            char c = u[0];
            if (c is >= 'A' and <= 'Z') return (uint)c;
            if (c is >= '0' and <= '9') return (uint)c;
        }
        if (u.StartsWith("F") && int.TryParse(u[1..], out int f) && f is >= 1 and <= 24)
            return (uint)(0x6F + f);
        return u switch
        {
            "PRINTSCREEN" or "PRTSC" or "PRTSCR" => 0x2C,
            "INSERT" => 0x2D,
            "DELETE" or "DEL" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "BACKSPACE" or "BS" => 0x08,
            "BROWSERBACK" => 0xA6,
            "BROWSERFORWARD" => 0xA7,
            "MEDIAPLAYPAUSE" or "PLAYPAUSE" or "PLAY" => 0xB3,
            "MEDIANEXT" or "NEXTTRACK" => 0xB0,
            "MEDIAPREV" or "PREVTRACK" => 0xB1,
            "VOLUMEUP" or "VOLUP" => 0xAF,
            "VOLUMEDOWN" or "VOLDOWN" => 0xAE,
            "VOLUMEMUTE" or "MUTE" => 0xAD,
            _ => 0,
        };
    }
}
