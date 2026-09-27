using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinPlus.Services;

public sealed class MemoImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
}

public sealed class MemoData
{
    public string Text { get; set; } = "";
    public List<MemoImage> Images { get; set; } = new();
}

[JsonSerializable(typeof(MemoData))]
internal partial class MemoJsonContext : JsonSerializerContext { }

public sealed class MemoService
{
    public static string Dir { get; } = Path.Combine(ConfigStore.Dir, "memo");
    public static string DataPath { get; } = Path.Combine(Dir, "memo.json");
    public static string ImageDir { get; } = Path.Combine(Dir, "images");

    public MemoData Data { get; private set; } = new();

    private bool TryLoadFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var d = JsonSerializer.Deserialize(json, MemoJsonContext.Default.MemoData);
            if (d is null) return false;
            d.Images.RemoveAll(i => !File.Exists(Path.Combine(ImageDir, i.FileName)));
            Data = d;
            return true;
        }
        catch { return false; }
    }

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(ImageDir);
            if (File.Exists(DataPath))
            {
                if (TryLoadFile(DataPath)) return;
                try
                {
                    if (File.Exists(DataPath + ".bak") && TryLoadFile(DataPath + ".bak"))
                    {
                        Save();
                        return;
                    }
                }
                catch { }
            }
        }
        catch { Data = new MemoData(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(Data, MemoJsonContext.Default.MemoData);
            var tmp = DataPath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(DataPath))
                File.Copy(DataPath, DataPath + ".bak", overwrite: true);
            File.Move(tmp, DataPath, overwrite: true);
        }
        catch { }
    }

    public string ImagePath(MemoImage img) => Path.Combine(ImageDir, img.FileName);

    public MemoImage? AddImageFile(string sourcePath)
    {
        try
        {
            var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")) return null;
            EnforceQuota();
            var img = new MemoImage { FileName = $"{Guid.NewGuid():N}{ext}" };
            File.Copy(sourcePath, ImagePath(img));
            Data.Images.Add(img);
            Save();
            return img;
        }
        catch { return null; }
    }

    public async Task<MemoImage?> AddImageFromBitmapAsync(Windows.Graphics.Imaging.SoftwareBitmap sb)
    {
        try
        {
            EnforceQuota();
            var img = new MemoImage { FileName = $"{Guid.NewGuid():N}.png" };
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(sb);
            await encoder.FlushAsync();
            stream.Seek(0);
            using var outFile = File.Create(ImagePath(img));
            using var net = stream.AsStreamForRead();
            await net.CopyToAsync(outFile);
            Data.Images.Add(img);
            Save();
            return img;
        }
        catch { return null; }
    }

    public void RemoveImage(MemoImage img)
    {
        try
        {
            Data.Images.Remove(img);
            var p = ImagePath(img);
            if (File.Exists(p)) File.Delete(p);
            Save();
        }
        catch { }
    }

    private void EnforceQuota(long maxBytes = 200L * 1024 * 1024)
    {
        try
        {
            var files = Directory.GetFiles(ImageDir).Select(f => new FileInfo(f)).OrderBy(f => f.CreationTimeUtc).ToList();
            long total = files.Sum(f => f.Length);
            foreach (var f in files)
            {
                if (total <= maxBytes) break;
                total -= f.Length;
                try { f.Delete(); } catch { }
                Data.Images.RemoveAll(i => string.Equals(i.FileName, f.Name, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch { }
    }
}
