using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace WinPlus.Services;

public static class HotkeyCapture
{
    public static void Attach(TextBox box)
    {
        string saved = "";
        bool capturing = false;
        void Arm()
        {
            if (capturing) return;
            capturing = true;
            saved = box.Text;
            box.Text = S.T("Short_CaptureHint");
        }
        void Disarm(bool restore)
        {
            if (!capturing) return;
            capturing = false;
            if (restore) box.Text = saved;
        }
        box.GotFocus += (_, _) => Arm();
        box.PointerPressed += (_, _) => Arm();
        box.LostFocus += (_, _) =>
        {
            if (!capturing) return;
            capturing = false;
            if (!HotkeySpec.TryParse(box.Text.Trim(), out _, out _)) box.Text = saved;
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (!capturing) return;
            e.Handled = true;
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Disarm(restore: true);
                return;
            }
            if (IsModifier(e.Key)) return;
            var spec = BuildSpec(e.Key);
            if (spec is null) return;
            box.Text = spec;
            capturing = false;
        };
    }

    private static bool IsModifier(Windows.System.VirtualKey key) => key switch
    {
        Windows.System.VirtualKey.Control => true,
        Windows.System.VirtualKey.Shift => true,
        Windows.System.VirtualKey.Menu => true,
        Windows.System.VirtualKey.LeftWindows => true,
        Windows.System.VirtualKey.RightWindows => true,
        Windows.System.VirtualKey.LeftControl => true,
        Windows.System.VirtualKey.RightControl => true,
        Windows.System.VirtualKey.LeftShift => true,
        Windows.System.VirtualKey.RightShift => true,
        Windows.System.VirtualKey.LeftMenu => true,
        Windows.System.VirtualKey.RightMenu => true,
        _ => false,
    };

    private static string? BuildSpec(Windows.System.VirtualKey key)
    {
        bool Down(Windows.System.VirtualKey k) =>
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(k) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        bool ctrl = Down(Windows.System.VirtualKey.Control);
        bool shift = Down(Windows.System.VirtualKey.Shift);
        bool alt = Down(Windows.System.VirtualKey.Menu);
        bool win = Down(Windows.System.VirtualKey.LeftWindows) || Down(Windows.System.VirtualKey.RightWindows);
        string? name = KeyName(key);
        if (name is null) return null;
        var parts = new System.Collections.Generic.List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (shift) parts.Add("Shift");
        if (alt) parts.Add("Alt");
        if (win) parts.Add("Win");
        parts.Add(name);
        return string.Join("+", parts);
    }

    private static string? KeyName(Windows.System.VirtualKey key)
    {
        if (key is >= Windows.System.VirtualKey.A and <= Windows.System.VirtualKey.Z)
            return key.ToString();
        if (key is >= Windows.System.VirtualKey.Number0 and <= Windows.System.VirtualKey.Number9)
            return ((int)key - (int)Windows.System.VirtualKey.Number0).ToString();
        if (key is >= Windows.System.VirtualKey.NumberPad0 and <= Windows.System.VirtualKey.NumberPad9)
            return ((int)key - (int)Windows.System.VirtualKey.NumberPad0).ToString();
        if (key is >= Windows.System.VirtualKey.F1 and <= Windows.System.VirtualKey.F24)
            return "F" + ((int)key - (int)Windows.System.VirtualKey.F1 + 1);
        return key switch
        {
            Windows.System.VirtualKey.Left => "Left",
            Windows.System.VirtualKey.Up => "Up",
            Windows.System.VirtualKey.Right => "Right",
            Windows.System.VirtualKey.Down => "Down",
            Windows.System.VirtualKey.Tab => "Tab",
            Windows.System.VirtualKey.Enter => "Enter",
            Windows.System.VirtualKey.Space => "Space",
            Windows.System.VirtualKey.Insert => "Insert",
            Windows.System.VirtualKey.Delete => "Delete",
            Windows.System.VirtualKey.Home => "Home",
            Windows.System.VirtualKey.End => "End",
            Windows.System.VirtualKey.PageUp => "PageUp",
            Windows.System.VirtualKey.PageDown => "PageDown",
            Windows.System.VirtualKey.Back => "Backspace",
            _ => null,
        };
    }
}
