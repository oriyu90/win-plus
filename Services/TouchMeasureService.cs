using System.Runtime.InteropServices;
using WinPlus.Services.Interop;

namespace WinPlus.Services;

internal static class HidP
{
    public const uint STATUS_SUCCESS = 0x00110000;

    [StructLayout(LayoutKind.Explicit)]
    public struct Caps
    {
        [FieldOffset(0)] public ushort Usage;
        [FieldOffset(2)] public ushort UsagePage;
        [FieldOffset(80)] public ushort NumberInputButtonCaps;
        [FieldOffset(82)] public ushort NumberInputValueCaps;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ButtonCap
    {
        [FieldOffset(0)] public ushort UsagePage;
        [FieldOffset(2)] public byte ReportID;
        [FieldOffset(6)] public ushort LinkCollection;
        [FieldOffset(12)] public byte IsRange;
        [FieldOffset(56)] public ushort UsageMin;
        [FieldOffset(58)] public ushort UsageMax;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ValueCap
    {
        [FieldOffset(0)] public ushort UsagePage;
        [FieldOffset(2)] public byte ReportID;
        [FieldOffset(6)] public ushort LinkCollection;
        [FieldOffset(12)] public byte IsRange;
        [FieldOffset(56)] public ushort UsageMin;
        [FieldOffset(58)] public ushort UsageMax;
        [FieldOffset(72)] public ushort BitSize;
        [FieldOffset(74)] public ushort ReportCount;
    }

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool HidD_GetPreparsedData(nint hidDeviceObject, ref nint preparsedData);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool HidD_FreePreparsedData(nint preparsedData);
    [DllImport("hid.dll")]
    public static extern uint HidP_GetCaps(nint preparsedData, ref Caps capabilities);
    [DllImport("hid.dll")]
    public static extern uint HidP_GetButtonCaps(uint reportType, [In, Out] ButtonCap[] buttonCaps, ref ushort buttonCapsLength, nint preparsedData);
    [DllImport("hid.dll")]
    public static extern uint HidP_GetValueCaps(uint reportType, [In, Out] ValueCap[] valueCaps, ref ushort valueCapsLength, nint preparsedData);
    [DllImport("hid.dll")]
    public static extern uint HidP_GetUsageValue(uint reportType, ushort usagePage, ushort linkCollection, ushort usage, out uint usageValue, nint preparsedData, byte[] report, uint reportLength);
    [DllImport("hid.dll")]
    public static extern uint HidP_GetUsages(uint reportType, ushort usagePage, ushort linkCollection, [In, Out] ushort[] usageList, ref uint usageLength, nint preparsedData, byte[] report, uint reportLength);
}

public sealed class TouchMeasureService : IDisposable
{
    private sealed class PtpDev
    {
        public string Path { get; init; } = "";
        public nint Preparsed { get; set; }
        public ushort LinkCount { get; set; }
        public bool HasCount { get; set; }
        public ushort LinkTip { get; set; }
        public bool HasTip { get; set; }
    }

    private readonly MessageWindow? _sinkWindow;
    private readonly List<PtpDev> _devices = new();
    private readonly Dictionary<nint, string> _handlePaths = new();
    private readonly object _parseLock = new();
    private bool _disposed;

    public int MaxSeen { get; private set; }
    public DateTime LastActive { get; private set; } = DateTime.MinValue;
    public bool Available => _devices.Count > 0;

    public event Action<int>? Live;

    public TouchMeasureService()
    {
        try
        {
            Scan();
            if (_devices.Count == 0) return;
            _sinkWindow = new MessageWindow();
            _sinkWindow.MessageReceived += OnMessage;
            var hwnd = _sinkWindow.Handle;
            var regs = new Win32.RAWINPUTDEVICE[]
            {
                new() { usUsagePage = 0x0D, usUsage = 0x05, dwFlags = Win32.RIDEV_INPUTSINK, hwndTarget = hwnd },
                new() { usUsagePage = 0x0D, usUsage = 0x04, dwFlags = Win32.RIDEV_INPUTSINK, hwndTarget = hwnd },
            };
            Win32.RegisterRawInputDevices(regs, (uint)regs.Length, (uint)Marshal.SizeOf<Win32.RAWINPUTDEVICE>());
        }
        catch { }
    }

    private void Scan()
    {
        try
        {
            foreach (var d in DeviceService.Enumerate())
            {
                if (d.Type != Win32.RIM_TYPEHID || string.IsNullOrEmpty(d.DeviceName)) continue;
                try
                {
                    var h = Win32.CreateFile(d.DeviceName, 0,
                        Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
                        nint.Zero, Win32.OPEN_EXISTING, 0, nint.Zero);
                    if (h == nint.Zero || h == new nint(-1)) continue;
                    try
                    {
                        nint preparsed = nint.Zero;
                        if (!HidP.HidD_GetPreparsedData(h, ref preparsed) || preparsed == nint.Zero)
                            continue;
                        var caps = new HidP.Caps();
                        if (HidP.HidP_GetCaps(preparsed, ref caps) != HidP.STATUS_SUCCESS)
                        {
                            try { HidP.HidD_FreePreparsedData(preparsed); } catch { }
                            continue;
                        }
                        if (caps.UsagePage != 0x0D || (caps.Usage != 0x05 && caps.Usage != 0x04))
                        {
                            try { HidP.HidD_FreePreparsedData(preparsed); } catch { }
                            continue;
                        }
                        var dev = new PtpDev { Path = d.DeviceName, Preparsed = preparsed };
                        ushort nVal = (ushort)Math.Min(64, (int)caps.NumberInputValueCaps);
                        if (nVal > 0)
                        {
                            var vals = new HidP.ValueCap[nVal];
                            ushort len = nVal;
                            if (HidP.HidP_GetValueCaps(0, vals, ref len, preparsed) == HidP.STATUS_SUCCESS)
                            {
                                foreach (var v in vals.Take(len))
                                {
                                    if (v.UsagePage != 0x0D) continue;
                                    bool hit = v.IsRange != 0
                                        ? (v.UsageMin <= 0x54 && 0x54 <= v.UsageMax)
                                        : v.UsageMin == 0x54;
                                    if (hit) { dev.HasCount = true; dev.LinkCount = v.LinkCollection; break; }
                                }
                            }
                        }
                        ushort nBtn = (ushort)Math.Min(64, (int)caps.NumberInputButtonCaps);
                        if (nBtn > 0)
                        {
                            var btns = new HidP.ButtonCap[nBtn];
                            ushort len = nBtn;
                            if (HidP.HidP_GetButtonCaps(0, btns, ref len, preparsed) == HidP.STATUS_SUCCESS)
                            {
                                foreach (var b in btns.Take(len))
                                {
                                    if (b.UsagePage != 0x09) continue;
                                    bool hit = b.IsRange != 0
                                        ? (b.UsageMin <= 0x42 && 0x42 <= b.UsageMax)
                                        : b.UsageMin == 0x42;
                                    if (hit) { dev.HasTip = true; dev.LinkTip = b.LinkCollection; break; }
                                }
                            }
                        }
                        if (dev.HasCount || dev.HasTip)
                            _devices.Add(dev);
                        else
                        {
                            try { HidP.HidD_FreePreparsedData(preparsed); } catch { }
                        }
                    }
                    finally { try { Win32.CloseHandle(h); } catch { } }
                }
                catch { }
                if (_devices.Count >= 4) break;
            }
        }
        catch { }
    }

    private void OnMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg != Win32.WM_INPUT) return;
        lock (_parseLock)
        {
            if (_disposed) return;
            try
            {
                ParseInput(lParam);
            }
            catch { }
        }
    }

    private void ParseInput(nint lParam)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<Win32.RAWINPUTHEADER>();
        Win32.GetRawInputData(lParam, Win32.RID_INPUT, nint.Zero, ref size, headerSize);
        if (size < headerSize + 8) return;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (Win32.GetRawInputData(lParam, Win32.RID_INPUT, buf, ref size, headerSize) != size)
                return;
            var header = Marshal.PtrToStructure<Win32.RAWINPUTHEADER>(buf);
            if (header.dwType != Win32.RIM_TYPEHID) return;
            if (!_handlePaths.TryGetValue(header.hDevice, out var path))
            {
                path = ReadPath(header.hDevice);
                _handlePaths[header.hDevice] = path ?? "";
                if (string.IsNullOrEmpty(path)) return;
            }
            var dev = _devices.FirstOrDefault(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (dev is null || dev.Preparsed == nint.Zero) return;
            int dataOff = (int)headerSize + 8;
            uint dwSizeHid = (uint)Marshal.ReadInt32(buf, dataOff);
            uint dwCount = (uint)Marshal.ReadInt32(buf, dataOff + 4);
            if (dwSizeHid == 0 || dwSizeHid > 1024 || dwCount == 0 || dwCount > 64) return;
            int active = 0;
            for (uint i = 0; i < dwCount; i++)
            {
                var report = new byte[dwSizeHid];
                Marshal.Copy(buf + dataOff + 8 + (int)(i * dwSizeHid), report, 0, (int)dwSizeHid);
                int a = ParseReport(dev, report);
                if (a > active) active = a;
            }
            if (active > 0)
            {
                if (active > MaxSeen) MaxSeen = active;
                LastActive = DateTime.Now;
                Live?.Invoke(active);
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private int ParseReport(PtpDev dev, byte[] report)
    {
        int active = 0;
        try
        {
            if (dev.HasCount)
            {
                uint v;
                if (HidP.HidP_GetUsageValue(0, 0x0D, dev.LinkCount, 0x54, out v, dev.Preparsed, report, (uint)report.Length) == HidP.STATUS_SUCCESS)
                    active = Math.Max(active, (int)Math.Min(v, 32));
            }
            if (dev.HasTip)
            {
                var list = new ushort[32];
                uint len = (uint)list.Length;
                if (HidP.HidP_GetUsages(0, 0x09, dev.LinkTip, list, ref len, dev.Preparsed, report, (uint)report.Length) == HidP.STATUS_SUCCESS)
                    active = Math.Max(active, (int)Math.Min(len, 32));
            }
        }
        catch { }
        return active;
    }

    private static string? ReadPath(nint hDevice)
    {
        try
        {
            uint size = 0;
            Win32.GetRawInputDeviceInfo(hDevice, Win32.RIDI_DEVICENAME, nint.Zero, ref size);
            if (size == 0 || size > 1024) return null;
            var buf = Marshal.AllocHGlobal((int)(size * 2 + 8));
            try
            {
                uint r = Win32.GetRawInputDeviceInfo(hDevice, Win32.RIDI_DEVICENAME, buf, ref size);
                if (r == 0 || r > 1024) return null;
                return DeviceService.ReadNativeString(buf, (int)(size * 2 + 8));
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { return null; }
    }

    public void Dispose()
    {
        lock (_parseLock)
        {
            if (_disposed) return;
            _disposed = true;
            try { _sinkWindow?.Dispose(); } catch { }
            foreach (var d in _devices)
            {
                try { if (d.Preparsed != nint.Zero) HidP.HidD_FreePreparsedData(d.Preparsed); } catch { }
                d.Preparsed = nint.Zero;
            }
            _devices.Clear();
        }
    }
}
