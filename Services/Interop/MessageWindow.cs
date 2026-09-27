using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WinPlus.Services.Interop;

namespace WinPlus.Services.Interop;

internal sealed class MessageWindow : IDisposable
{
    private readonly Win32.WndProc _wndProc;
    private readonly Thread _thread;
    private nint _hwnd;
    private uint _threadId;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ConcurrentQueue<(Func<object?> Fn, TaskCompletionSource<object?> Tcs)> _queue = new();
    private readonly string _className = "WinPlusMsg" + Guid.NewGuid().ToString("N");
    private bool _disposed;

    public nint Handle
    {
        get { _ready.Wait(); return _hwnd; }
    }

    public event Action<uint, nint, nint>? MessageReceived;

    public MessageWindow()
    {
        _wndProc = WndProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "WinPlusMsgLoop" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Run()
    {
        var hInst = Win32.GetModuleHandle(null);
        var wc = new Win32.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = hInst,
            lpszClassName = _className,
        };
        Win32.RegisterClassEx(ref wc);
        _threadId = Win32.GetCurrentThreadId();
        _hwnd = Win32.CreateWindowEx(0, _className, "", 0, 0, 0, 0, 0,
            new nint(-3), nint.Zero, hInst, nint.Zero);
        Win32.PeekMessage(out _, nint.Zero, 0, 0, 0);
        _ready.Set();
        while (Win32.GetMessage(out var msg, nint.Zero, 0, 0))
        {
            if (msg.message == Win32.WM_INVOKE)
            {
                DrainQueue();
                continue;
            }
            if (msg.message == 0x0012)
            {
                Win32.DestroyWindow(_hwnd);
                Win32.PostQuitMessage(0);
                break;
            }
            try
            {
                var m = msg;
                Win32.TranslateMessage(ref m);
                Win32.DispatchMessage(ref m);
            }
            catch { }
        }
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var item))
        {
            try { item.Tcs.TrySetResult(item.Fn()); }
            catch (Exception ex) { item.Tcs.TrySetException(ex); }
        }
    }

    public Task<object?> InvokeAsync(Func<object?> fn)
    {
        try
        {
            if (_threadId != 0 && Win32.GetCurrentThreadId() == _threadId)
                return Task.FromResult(fn());
        }
        catch { }
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue((fn, tcs));
        bool posted = false;
        try { posted = Win32.PostThreadMessage(_threadId, Win32.WM_INVOKE, nint.Zero, nint.Zero); } catch { }
        if (!posted)
        {
            if (_queue.TryDequeue(out var item) && ReferenceEquals(item.Tcs, tcs))
            {
                try { tcs.TrySetResult(fn()); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }
        }
        return tcs.Task;
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        try { MessageReceived?.Invoke(msg, wParam, lParam); }
        catch { }
        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
