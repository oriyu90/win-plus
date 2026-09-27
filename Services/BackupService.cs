using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace WinPlus.Services;

public sealed class BackupMeta
{
    public string App { get; set; } = "win-plus";
    public string Version { get; set; } = AppState.AppVersion;
    public string ExportedAt { get; set; } = "";
}

public static class BackupService
{
    public static string CreateZip(string destPath) => CreateZip(destPath, ConfigStore.Dir);

    public static string CreateZip(string destPath, string sourceDir)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var meta = new BackupMeta { ExportedAt = stamp };
        string metaJson = JsonSerializer.Serialize(meta);
        using var zip = ZipFile.Open(destPath, ZipArchiveMode.Create);
        if (Directory.Exists(sourceDir))
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(sourceDir, file);
                if (rel.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                zip.CreateEntryFromFile(file, "data/" + rel.Replace('\\', '/'));
            }
        }
        var entry = zip.CreateEntry("version.json");
        using var w = new StreamWriter(entry.Open());
        w.Write(metaJson);
        return destPath;
    }

    public static void RestoreZip(string zipPath) => RestoreZip(zipPath, ConfigStore.Dir);

    public static void RestoreZip(string zipPath, string destDir)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var metaEntry = zip.GetEntry("version.json");
        if (metaEntry is null) throw new InvalidDataException("version.json missing");
        string metaJson;
        using (var r = new StreamReader(metaEntry.Open())) metaJson = r.ReadToEnd();
        var meta = JsonSerializer.Deserialize<BackupMeta>(metaJson);
        if (meta is null || !string.Equals(meta.App, "win-plus", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("not a win-plus backup");
        Directory.CreateDirectory(destDir);
        string backupDir = destDir + ".preimport-" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        if (Directory.Exists(destDir))
            Directory.Move(destDir, backupDir);
        Directory.CreateDirectory(destDir);
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.FullName.EndsWith("/")) continue;
            string rel = entry.FullName["data/".Length..].Replace('/', Path.DirectorySeparatorChar);
            string dest = Path.GetFullPath(Path.Combine(destDir, rel));
            if (!dest.StartsWith(Path.GetFullPath(destDir), StringComparison.OrdinalIgnoreCase))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? destDir);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }
}
