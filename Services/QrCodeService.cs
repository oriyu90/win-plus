using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using ZXing;
using ZXing.Common;

namespace WinPlus.Services;

/// <summary>
/// QR payload decoder for screenshots. Runs fully offline on a background
/// thread; only http/https URLs are ever returned (see <see cref="TrySanitizeUrl"/>).
/// All failures yield an empty result — never throws to callers.
/// </summary>
public static class QrCodeService
{
    public const int MaxLinks = 10;
    public const int MaxUrlLength = 2048;
    private const int MaxDecodeDimension = 1600;
    private const long MaxFileBytes = 100 * 1024 * 1024;

    public sealed record DecodeResult(IReadOnlyList<string> Links, int Blocked);

    public static async Task<DecodeResult> DecodeFromFileAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return Empty();
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) return Empty();
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxFileBytes) return Empty();
            return await Task.Run(() => Decode(path)).ConfigureAwait(false);
        }
        catch { return Empty(); }
    }

    private static DecodeResult Empty() =>
        new DecodeResult(Array.Empty<string>(), 0);

    private static DecodeResult Decode(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var ras = file.AsRandomAccessStream();
            var getDecoder = BitmapDecoder.CreateAsync(ras).AsTask();
            getDecoder.Wait();
            var decoder = getDecoder.Result;

            uint w = decoder.PixelWidth, h = decoder.PixelHeight;
            if (w == 0 || h == 0 || w > 30000 || h > 30000) return Empty();

            double scale = Math.Min(1.0, (double)MaxDecodeDimension / Math.Max(w, h));
            var transform = new BitmapTransform();
            if (scale < 1.0)
            {
                transform.ScaledWidth = (uint)Math.Max(1, w * scale);
                transform.ScaledHeight = (uint)Math.Max(1, h * scale);
                transform.InterpolationMode = BitmapInterpolationMode.Linear;
            }

            var getPixels = decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
                transform, ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask();
            getPixels.Wait();
            byte[] pixels = getPixels.Result.DetachPixelData();
            uint sw = scale < 1.0 ? transform.ScaledWidth : w;
            uint sh = scale < 1.0 ? transform.ScaledHeight : h;
            if (pixels.Length < sw * sh * 4) return Empty();

            var luminance = new RGBLuminanceSource(pixels, (int)sw, (int)sh, RGBLuminanceSource.BitmapFormat.BGR32);
            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
                    TryHarder = false,
                    TryInverted = true,
                },
            };
            Result[]? results;
            try { results = reader.DecodeMultiple(luminance); }
            catch { return Empty(); }
            if (results is null || results.Length == 0) return Empty();

            var links = new List<string>(results.Length);
            int blocked = 0;
            foreach (var r in results)
            {
                string? text = r?.Text;
                if (string.IsNullOrWhiteSpace(text)) { blocked++; continue; }
                if (TrySanitizeUrl(text, out string url))
                {
                    if (!links.Contains(url, StringComparer.Ordinal))
                        links.Add(url);
                    if (links.Count >= MaxLinks) break;
                }
                else blocked++;
            }
            return new DecodeResult(links, blocked);
        }
        catch { return Empty(); }
    }

    /// <summary>
    /// Accepts only absolute http/https URLs. Rejects javascript:, file:,
    /// data:, UNC, username-embedded and over-long payloads.
    /// </summary>
    public static bool TrySanitizeUrl(string? raw, out string url)
    {
        url = "";
        try
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string s = raw.Trim();
            if (s.Length == 0 || s.Length > MaxUrlLength) return false;
            foreach (char c in s)
                if (char.IsControl(c)) return false;
            if (s.Contains(' ') || s.Contains('\u3000')) return false;
            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)) return false;
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrEmpty(uri.Host)) return false;
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
            url = uri.AbsoluteUri;
            return url.Length <= MaxUrlLength;
        }
        catch { url = ""; return false; }
    }
}
