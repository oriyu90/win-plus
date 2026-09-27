using System.Runtime.InteropServices;
using System.Text;
using WinPlus.Services.Interop;

namespace WinPlus.Services;

public sealed class InputDeviceInfo
{
    public nint Handle { get; init; }
    public uint Type { get; init; }
    public string DeviceName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Product { get; init; } = "";
    public string Bus { get; init; } = "";
    public bool IsUsb { get; init; }
    public string Vid { get; init; } = "";
    public string Pid { get; init; } = "";
    public string TypeName => Type switch
    {
        Win32.RIM_TYPEKEYBOARD => S.T("Dev_Keyboard"),
        Win32.RIM_TYPEMOUSE => S.T("Dev_Mouse"),
        Win32.RIM_TYPEHID => S.T("Dev_Hid"),
        _ => S.T("Dev_Unknown"),
    };
    public string BusDisplay => Bus switch
    {
        "USB" => S.T("Dev_BusUsb"),
        "Bluetooth" => S.T("Dev_BusBt"),
        "Internal" => S.T("Dev_BusInternal"),
        _ => S.T("Dev_BusOther"),
    };
    public string SubLine
    {
        get
        {
            var sb = new StringBuilder($"{BusDisplay} · {TypeName}");
            if (!string.IsNullOrEmpty(Vid)) sb.Append($" (VID_{Vid} PID_{Pid})");
            return sb.ToString();
        }
    }
}

public sealed class DeviceService : IDisposable
{
    private readonly MessageWindow? _sinkWindow;
    private bool _disposed;

    public event Action<nint>? InputReceived;

    private static readonly Guid GuidHid = new("4d1e55b2-f16f-11cf-88cb-001111000030");
    private static readonly Guid GuidKeyboard = new("884b96c3-56ef-11d1-bc8c-00a0c91405dd");
    private static readonly Guid GuidMouse = new("378de44c-56ef-11d1-bc8c-00a0c91405dd");

    public DeviceService(bool enableSink = true)
    {
        if (!enableSink) return;
        try
        {
            _sinkWindow = new MessageWindow();
            _sinkWindow.MessageReceived += OnMessage;
            var hwnd = _sinkWindow.Handle;
            var core = new Win32.RAWINPUTDEVICE[]
            {
                new() { usUsagePage = 0x01, usUsage = 0x06, dwFlags = Win32.RIDEV_INPUTSINK, hwndTarget = hwnd },
                new() { usUsagePage = 0x01, usUsage = 0x02, dwFlags = Win32.RIDEV_INPUTSINK, hwndTarget = hwnd },
            };
            Win32.RegisterRawInputDevices(core, (uint)core.Length, (uint)Marshal.SizeOf<Win32.RAWINPUTDEVICE>());
            try
            {
                var ptp = new Win32.RAWINPUTDEVICE[]
                {
                    new() { usUsagePage = 0x0D, usUsage = 0x05, dwFlags = Win32.RIDEV_INPUTSINK, hwndTarget = hwnd },
                };
                Win32.RegisterRawInputDevices(ptp, (uint)ptp.Length, (uint)Marshal.SizeOf<Win32.RAWINPUTDEVICE>());
            }
            catch { }
        }
        catch { }
    }

    private void OnMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg != Win32.WM_INPUT) return;
        try
        {
            uint size = 0;
            Win32.GetRawInputData(lParam, Win32.RID_INPUT, nint.Zero, ref size, (uint)Marshal.SizeOf<Win32.RAWINPUTHEADER>());
            if (size == 0) return;
            var buf = Marshal.AllocHGlobal((int)size);
            try
            {
                if (Win32.GetRawInputData(lParam, Win32.RID_INPUT, buf, ref size, (uint)Marshal.SizeOf<Win32.RAWINPUTHEADER>()) == size)
                {
                    var header = Marshal.PtrToStructure<Win32.RAWINPUTHEADER>(buf);
                    InputReceived?.Invoke(header.hDevice);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { }
    }

    private sealed class SetupEntry
    {
        public string Path { get; init; } = "";
        public string Friendly { get; init; } = "";
        public string Desc { get; init; } = "";
        public uint DevInst { get; init; }
    }

    public static List<InputDeviceInfo> Enumerate()
    {
        var result = new List<InputDeviceInfo>();
        try
        {
            var setup = new List<SetupEntry>();
            setup.AddRange(EnumSetupInterface(GuidHid));
            setup.AddRange(EnumSetupInterface(GuidKeyboard));
            setup.AddRange(EnumSetupInterface(GuidMouse));

            uint count = 0;
            uint sz = (uint)Marshal.SizeOf<Win32.RAWINPUTDEVICELIST>();
            if (Win32.GetRawInputDeviceList(nint.Zero, ref count, sz) != 0 || count == 0)
                return result;
            var buf = Marshal.AllocHGlobal((int)(count * sz));
            try
            {
                if (Win32.GetRawInputDeviceList(buf, ref count, sz) != unchecked((uint)-1))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var item = Marshal.PtrToStructure<Win32.RAWINPUTDEVICELIST>(buf + i * (int)sz);
                        try { result.Add(BuildInfo(item, setup)); } catch { }
                    }
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { }
        return result;
    }

    private static InputDeviceInfo BuildInfo(Win32.RAWINPUTDEVICELIST item, List<SetupEntry> setup)
    {
        string path = GetDeviceName(item.hDevice);
        var match = setup.FirstOrDefault(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase));
        string bus = match is not null && match.DevInst != 0 ? ClassifyBus(match.DevInst) : GuessBusFromPath(path, item.dwType);
        string manufacturer = "", product = "", vid = "", pid = "";
        if (item.dwType == Win32.RIM_TYPEHID && !string.IsNullOrEmpty(path))
        {
            try { ReadHidStrings(path, out manufacturer, out product, out vid, out pid); } catch { }
        }
        string display = BuildDisplayName(manufacturer, product, match?.Friendly, match?.Desc, path, item.dwType);
        return new InputDeviceInfo
        {
            Handle = item.hDevice,
            Type = item.dwType,
            DeviceName = path,
            DisplayName = display,
            Manufacturer = manufacturer,
            Product = product,
            Bus = bus,
            IsUsb = bus == "USB",
            Vid = vid,
            Pid = pid,
        };
    }

    private static string BuildDisplayName(string manufacturer, string product, string? friendly, string? desc, string path, uint type)
    {
        var mp = $"{manufacturer} {product}".Trim();
        if (!string.IsNullOrWhiteSpace(mp) && mp.Length > 2) return mp.Length > 64 ? mp[..64] + "…" : mp;
        if (!string.IsNullOrWhiteSpace(friendly)) return friendly;
        if (!string.IsNullOrWhiteSpace(desc)) return desc;
        return FriendlyName(path, type);
    }

    private static void ReadHidStrings(string path, out string manufacturer, out string product, out string vid, out string pid)
    {
        manufacturer = ""; product = ""; vid = ""; pid = "";
        var h = Win32.CreateFile(path, 0, Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE,
            nint.Zero, Win32.OPEN_EXISTING, 0, nint.Zero);
        if (h == nint.Zero || h == new nint(-1)) return;
        try
        {
            var attr = new Win32.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<Win32.HIDD_ATTRIBUTES>() };
            if (Win32.HidD_GetAttributes(h, ref attr))
            {
                vid = attr.VendorID.ToString("X4");
                pid = attr.ProductID.ToString("X4");
            }
            var bufPtr = Marshal.AllocHGlobal(256);
            try
            {
                for (int i = 0; i < 256; i++) Marshal.WriteByte(bufPtr, i, 0);
                if (Win32.HidD_GetManufacturerString(h, bufPtr, 256))
                    manufacturer = Sanitize(ReadNativeString(bufPtr, 256));
                for (int i = 0; i < 256; i++) Marshal.WriteByte(bufPtr, i, 0);
                if (Win32.HidD_GetProductString(h, bufPtr, 256))
                    product = Sanitize(ReadNativeString(bufPtr, 256));
            }
            finally { Marshal.FreeHGlobal(bufPtr); }
        }
        finally { try { Win32.CloseHandle(h); } catch { } }
    }

    private static List<SetupEntry> EnumSetupInterface(Guid ifaceGuid)
    {
        var result = new List<SetupEntry>();
        var guid = ifaceGuid;
        var set = Win32.SetupDiGetClassDevs(ref guid, null, nint.Zero, Win32.DIGCF_PRESENT | Win32.DIGCF_DEVICEINTERFACE);
        if (set == nint.Zero || set == new nint(-1)) return result;
        try
        {
            uint index = 0;
            while (true)
            {
                var ifData = new Win32.SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<Win32.SP_DEVICE_INTERFACE_DATA>() };
                if (!Win32.SetupDiEnumDeviceInterfaces(set, nint.Zero, ref guid, index++, ref ifData))
                    break;
                string? path = GetInterfacePath(set, ref ifData, out var devInst, out var friendly, out var desc);
                if (string.IsNullOrEmpty(path)) continue;
                result.Add(new SetupEntry { Path = path, Friendly = friendly, Desc = desc, DevInst = devInst });
            }
        }
        finally { try { Win32.SetupDiDestroyDeviceInfoList(set); } catch { } }
        return result;
    }

    private static string? GetInterfacePath(nint set, ref Win32.SP_DEVICE_INTERFACE_DATA ifData, out uint devInst, out string friendly, out string desc)
    {
        devInst = 0;
        friendly = "";
        desc = "";
        if (!Win32.SetupDiGetDeviceInterfaceDetail(set, ref ifData, nint.Zero, 0, out uint needed, nint.Zero) && needed == 0)
            return null;
        var detailBuf = Marshal.AllocHGlobal((int)needed);
        var infoBuf = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.SP_DEVINFO_DATA>());
        try
        {
            Marshal.WriteInt32(detailBuf, IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize);
            var info = new Win32.SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<Win32.SP_DEVINFO_DATA>() };
            Marshal.StructureToPtr(info, infoBuf, false);
            if (!Win32.SetupDiGetDeviceInterfaceDetail(set, ref ifData, detailBuf, needed, out _, infoBuf))
                return null;
            var back = Marshal.PtrToStructure<Win32.SP_DEVINFO_DATA>(infoBuf);
            devInst = back.DevInst;
            friendly = GetRegPropWithInfo(set, back, Win32.SPDRP_FRIENDLYNAME);
            desc = GetRegPropWithInfo(set, back, Win32.SPDRP_DEVICEDESC);
            return ReadNativeString(detailBuf + 4, (int)Math.Min(needed, 512));
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuf);
            Marshal.FreeHGlobal(infoBuf);
        }
    }

    private static string GetRegProp(nint set, uint devInst, uint prop)
    {
        return "";
    }

    private static string GetRegPropWithInfo(nint set, Win32.SP_DEVINFO_DATA info, uint prop)
    {
        try
        {
            var mutable = info;
            if (!Win32.SetupDiGetDeviceRegistryProperty(set, ref mutable, prop, out _, null, 0, out uint needed) && needed == 0)
                return "";
            var buf = new byte[needed];
            if (!Win32.SetupDiGetDeviceRegistryProperty(set, ref mutable, prop, out uint regType, buf, needed, out _))
                return "";
            if (regType == 1)
                return Sanitize(Encoding.Unicode.GetString(buf).TrimEnd('\0').Trim());
            return "";
        }
        catch { return ""; }
    }

    private static string? GetDeviceIdString(uint devInst)
    {
        try
        {
            var buf = new char[256];
            if (Win32.CM_Get_Device_ID(devInst, buf, (uint)buf.Length, 0) == 0)
                return new string(buf).TrimEnd('\0');
        }
        catch { }
        return null;
    }

    internal static string ClassifyBus(uint devInst)
    {
        try
        {
            var ids = new List<string>();
            uint cur = devInst;
            for (int i = 0; i < 3; i++)
            {
                var id = GetDeviceIdString(cur);
                if (string.IsNullOrEmpty(id)) break;
                ids.Add(id.ToUpperInvariant());
                if (Win32.CM_Get_Parent(out uint parent, cur, 0) != 0) break;
                if (parent == 0) break;
                cur = parent;
            }
            string all = string.Join(">", ids);
            if (all.Contains("BTHENUM") || all.Contains(@"\BTH") || all.Contains("BLUETOOTH")) return "Bluetooth";
            if (all.Contains(@"USB\")) return "USB";
            if (all.Contains("I2C") || all.Contains("SPI")) return "Internal";
            if (all.Contains(@"ACPI\") || all.Contains("PNP")) return "Internal";
            return "Other";
        }
        catch { return "Other"; }
    }

    internal static string GuessBusFromPath(string path, uint type)
    {
        var u = (path ?? "").ToUpperInvariant();
        if (u.Contains("BTHENUM") || u.Contains("BLUETOOTH")) return "Bluetooth";
        if (u.Contains(@"USB\")) return "USB";
        if (u.Contains("I2C")) return "Internal";
        if (u.Contains("ACPI") || u.Contains("PNP")) return "Internal";
        return "Other";
    }

    internal static string ReadNativeString(nint ptr, int maxBytes = 512)
    {
        if (ptr == nint.Zero) return "";
        try
        {
            int n = Math.Min(maxBytes, 512);
            var bytes = new byte[n];
            Marshal.Copy(ptr, bytes, 0, n);
            string uni = DecodeUnicode(bytes);
            if (LooksLikeDevicePath(uni)) return uni;
            string ansi = DecodeAnsi(bytes);
            if (LooksLikeDevicePath(ansi)) return ansi;
            if (uni.Length >= ansi.Length) return Sanitize(uni);
            return Sanitize(ansi);
        }
        catch { return ""; }
    }

    private static bool LooksLikeDevicePath(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        return s.StartsWith(@"\\?\", StringComparison.Ordinal) || s.StartsWith(@"\\.\", StringComparison.Ordinal)
            || s.Contains("VID_") || s.Contains("HID#") || s.Contains("ACPI") || s.Contains("USB");
    }

    private static string DecodeUnicode(byte[] bytes)
    {
        int len = 0;
        while (len + 1 < bytes.Length && (bytes[len] != 0 || bytes[len + 1] != 0)) len += 2;
        if (len == 0) return "";
        return Sanitize(Encoding.Unicode.GetString(bytes, 0, len));
    }

    private static string DecodeAnsi(byte[] bytes)
    {
        int len = 0;
        while (len < bytes.Length && bytes[len] != 0) len++;
        if (len == 0) return "";
        return Sanitize(Encoding.Latin1.GetString(bytes, 0, len));
    }

    internal static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == '\0') break;
            if (char.IsControl(c) && c != '\t') continue;
            sb.Append(c);
        }
        var t = sb.ToString().Trim();
        return t.Length > 160 ? t[..160] : t;
    }

    private static string GetDeviceName(nint hDevice)
    {
        try
        {
            uint size = 0;
            Win32.GetRawInputDeviceInfo(hDevice, Win32.RIDI_DEVICENAME, nint.Zero, ref size);
            if (size == 0 || size > 1024) return "";
            var buf = Marshal.AllocHGlobal((int)(size * 2 + 8));
            try
            {
                uint r = Win32.GetRawInputDeviceInfo(hDevice, Win32.RIDI_DEVICENAME, buf, ref size);
                if (r == 0 || r > 1024) return "";
                return ReadNativeString(buf, (int)(size * 2 + 8));
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { }
        return "";
    }

    internal static string FriendlyName(string deviceName, uint type)
    {
        if (string.IsNullOrEmpty(deviceName))
            return TypeNameOf(type);
        var short_ = deviceName.Replace(@"\\?\", "");
        int hash = short_.IndexOf('#');
        string tail = hash >= 0 ? short_[(hash + 1)..] : short_;
        if (tail.Length > 56) tail = tail[..56] + "…";
        return $"{TypeNameOf(type)} — {tail}";
    }

    private static string TypeNameOf(uint type) => type switch
    {
        Win32.RIM_TYPEKEYBOARD => S.T("Dev_Keyboard"),
        Win32.RIM_TYPEMOUSE => S.T("Dev_Mouse"),
        Win32.RIM_TYPEHID => S.T("Dev_Hid"),
        _ => S.T("Dev_Unknown"),
    };

    public static bool HasPrecisionTouchpad()
    {
        try
        {
            foreach (var d in Enumerate())
            {
                if (d.Type != Win32.RIM_TYPEHID) continue;
                var hay = $"{d.DeviceName} {d.Manufacturer} {d.Product}".ToUpperInvariant();
                if (hay.Contains("VID_06CB") || hay.Contains("VID_27C6") || hay.Contains("VID_04F3") ||
                    hay.Contains("ELAN") || hay.Contains("SYNAPTICS") || hay.Contains("TOUCHPAD") ||
                    hay.Contains("PTP") || hay.Contains("PRECISION"))
                    return true;
            }
        }
        catch { }
        return Enumerate().Exists(d => d.Type == Win32.RIM_TYPEHID);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sinkWindow?.Dispose();
    }
}
