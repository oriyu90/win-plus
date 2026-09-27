using WinPlus.Services.Interop;

namespace WinPlus.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MessageWindow _window = new();
    private bool _added;
    private bool _disposed;
    private nint _icon;

    public event Action? OpenSettings;
    public event Action? OpenMemo;
    public event Action? TakeScreenshot;
    public event Action? ExitApp;

    public TrayIconService()
    {
        _window.MessageReceived += OnMessage;
    }

    public void Show(string? tooltip = null)
    {
        var hwnd = _window.Handle;
        tooltip ??= S.T("Tray_Tooltip");
        try
        {
            string? icoPath = null;
            try { icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"); } catch { }
            int smCx = 16, smCy = 16;
            try
            {
                smCx = Win32.GetSystemMetrics(Win32.SM_CXSMICON);
                smCy = Win32.GetSystemMetrics(Win32.SM_CYSMICON);
                if (smCx <= 0 || smCx > 64) smCx = 16;
                if (smCy <= 0 || smCy > 64) smCy = 16;
            }
            catch { }
            if (icoPath is not null && File.Exists(icoPath))
                _icon = Win32Menu.LoadImage(nint.Zero, icoPath, Win32Menu.IMAGE_ICON, smCx, smCy, Win32Menu.LR_LOADFROMFILE);
            if (_icon == nint.Zero)
                _icon = Win32.LoadIcon(nint.Zero, new nint(32512));
            var data = new Win32.NOTIFYICONDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = 1,
                uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
                uCallbackMessage = (uint)Win32.WM_TRAY,
                hIcon = _icon,
                szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            };
            _added = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
            try { AppLogger.Write($"tray show hwnd={hwnd:x} icon={_icon:x} added={_added}"); } catch { }
        }
        catch { }
    }

    public void ShowBalloon(string title, string text)
    {
        if (!_added) return;
        try
        {
            var data = new Win32.NOTIFYICONDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
                hWnd = _window.Handle,
                uID = 1,
                uFlags = Win32.NIF_INFO,
                szInfo = text.Length > 255 ? text[..255] : text,
                szInfoTitle = title.Length > 63 ? title[..63] : title,
                dwInfoFlags = 0x00000001,
            };
            Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
        }
        catch { }
    }

    private void OnMessage(uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (msg == Win32.WM_TRAY)
            {
                int evt = lParam.ToInt32();
                if (evt == Win32Menu.WM_LBUTTONUP) OpenSettings?.Invoke();
                else if (evt == Win32Menu.WM_RBUTTONUP) ShowMenu();
            }
            else if (msg == Win32Menu.WM_COMMAND)
            {
                int id = wParam.ToInt32() & 0xFFFF;
                switch (id)
                {
                    case 10: OpenSettings?.Invoke(); break;
                    case 11: OpenMemo?.Invoke(); break;
                    case 12: TakeScreenshot?.Invoke(); break;
                    case 13: ExitApp?.Invoke(); break;
                }
            }
        }
        catch { }
    }

    private void ShowMenu()
    {
        var hwnd = _window.Handle;
        var menu = Win32Menu.CreatePopupMenu();
        if (menu == nint.Zero) { OpenSettings?.Invoke(); return; }
        try
        {
            Win32Menu.AppendMenu(menu, Win32Menu.MF_STRING, 10, S.T("Tray_Settings"));
            Win32Menu.AppendMenu(menu, Win32Menu.MF_STRING, 11, S.T("Tray_Memo"));
            Win32Menu.AppendMenu(menu, Win32Menu.MF_STRING, 12, S.T("Tray_Shot"));
            Win32Menu.AppendMenu(menu, Win32Menu.MF_SEPARATOR, 0, "");
            Win32Menu.AppendMenu(menu, Win32Menu.MF_STRING, 13, S.T("Tray_Exit"));
            Win32.SetForegroundWindow(hwnd);
            if (Win32Menu.GetCursorPos(out var pt))
                Win32Menu.TrackPopupMenu(menu, Win32Menu.TPM_LEFTALIGN | Win32Menu.TPM_BOTTOMALIGN, pt.X, pt.Y, 0, hwnd, nint.Zero);
            Win32Menu.PostMessage(hwnd, 0, nint.Zero, nint.Zero);
        }
        finally { Win32Menu.DestroyMenu(menu); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_added)
            {
                var data = new Win32.NOTIFYICONDATA
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
                    hWnd = _window.Handle,
                    uID = 1,
                };
                Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
            }
        }
        catch { }
        _window.Dispose();
    }
}
