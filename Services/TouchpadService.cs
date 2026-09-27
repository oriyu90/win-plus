using System.Runtime.InteropServices;
using WinPlus.Services.Interop;

namespace WinPlus.Services;

public sealed class TouchpadService : IDisposable
{
    private readonly Win32.HookProc _proc;
    private nint _hook;
    private bool _disposed;

    public bool PtpPresent { get; }
    public bool RemapEnabled { get; set; }
    public event Action<string>? GestureDetected;

    public TouchpadService()
    {
        _proc = MouseProc;
        try { PtpPresent = DeviceService.HasPrecisionTouchpad(); }
        catch { PtpPresent = false; }
    }

    public void Start()
    {
        if (_hook != nint.Zero) return;
        try { _hook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _proc, Win32.GetModuleHandle(null), 0); }
        catch { _hook = nint.Zero; }
    }

    public void Stop()
    {
        if (_hook != nint.Zero)
        {
            try { Win32.UnhookWindowsHookEx(_hook); } catch { }
            _hook = nint.Zero;
        }
    }

    private nint MouseProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && RemapEnabled)
        {
            try
            {
                int msg = wParam.ToInt32();
                if (msg == Win32.WM_MBUTTONDOWN)
                {
                    GestureDetected?.Invoke("ThreeFingerTap");
                    return 1;
                }
            }
            catch { }
        }
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
