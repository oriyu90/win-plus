using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using WinPlus.Services.Interop;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WinPlus.Services;

public sealed class ScreenshotService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(nint hdc, nint hbmp, uint uStartScan, uint cScanLines, nint lpvBits, ref BITMAPINFOHEADER lpbi, uint uUsage);

    public record ShotBounds(int X, int Y, int Width, int Height);

    public static ShotBounds VirtualScreen() => new(
        Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN),
        Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN),
        Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN),
        Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));

    public static ShotBounds ActiveWindow()
    {
        var fg = Win32.GetForegroundWindow();
        if (fg != nint.Zero && Win32.GetWindowRect(fg, out var r) && r.Right > r.Left && r.Bottom > r.Top)
            return new ShotBounds(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return VirtualScreen();
    }

    public async Task<string?> CaptureAsync(ShotBounds bounds, string folder, string format, bool includeCursor)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;
        nint screenDc = nint.Zero, memDc = nint.Zero, hbmp = nint.Zero, old = nint.Zero;
        try
        {
            Directory.CreateDirectory(folder);
            screenDc = Win32.GetDC(nint.Zero);
            memDc = Win32.CreateCompatibleDC(screenDc);
            hbmp = Win32.CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);
            if (hbmp == nint.Zero) return null;
            old = Win32.SelectObject(memDc, hbmp);
            if (!Win32.BitBlt(memDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.X, bounds.Y,
                    Win32.SRCCOPY | Win32.CAPTUREBLT))
                return null;
            if (includeCursor) DrawCursor(memDc, bounds);
            Win32.SelectObject(memDc, old);
            old = nint.Zero;
            var pixels = ReadPixels(memDc, hbmp, bounds.Width, bounds.Height);
            if (pixels is null) return null;
            string ext = format.Equals("JPG", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
            string name = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.{ext}";
            string path = Path.Combine(folder, name);
            await SaveAsync(pixels, bounds.Width, bounds.Height, path, ext);
            return path;
        }
        catch { return null; }
        finally
        {
            if (old != nint.Zero && memDc != nint.Zero) Win32.SelectObject(memDc, old);
            if (hbmp != nint.Zero) Win32.DeleteObject(hbmp);
            if (memDc != nint.Zero) Win32.DeleteDC(memDc);
            if (screenDc != nint.Zero) Win32.ReleaseDC(nint.Zero, screenDc);
        }
    }

    private static void DrawCursor(nint memDc, ShotBounds bounds)
    {
        try
        {
            var ci = new Win32.CURSORINFO { cbSize = (uint)Marshal.SizeOf<Win32.CURSORINFO>() };
            if (Win32.GetCursorInfo(ref ci) && ci.hCursor != nint.Zero)
                Win32.DrawIcon(memDc, ci.x - bounds.X, ci.y - bounds.Y, ci.hCursor);
        }
        catch { }
    }

    private static byte[]? ReadPixels(nint memDc, nint hbmp, int w, int h)
    {
        var bi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };
        var pixels = new byte[w * h * 4];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            int lines = GetDIBits(memDc, hbmp, 0, (uint)h, handle.AddrOfPinnedObject(), ref bi, 0);
            return lines == 0 ? null : pixels;
        }
        finally { handle.Free(); }
    }

    private static async Task SaveAsync(byte[] pixels, int w, int h, string path, string ext)
    {
        var sb = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        Guid encoderId = ext == "jpg" ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId;
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetSoftwareBitmap(sb);
        if (ext == "jpg")
        {
            var props = new BitmapPropertySet
            {
                ["ImageQuality"] = new BitmapTypedValue(0.9, Windows.Foundation.PropertyType.Single),
            };
            await encoder.BitmapProperties.SetPropertiesAsync(props);
        }
        await encoder.FlushAsync();
        stream.Seek(0);
        using var file = File.Create(path);
        using var net = stream.AsStreamForRead();
        await net.CopyToAsync(file);
    }
}
