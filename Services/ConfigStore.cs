using System.IO;
using System.Text.Json;
using WinPlus.Models;

namespace WinPlus.Services;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        TypeInfoResolver = AppJsonContext.Default,
    };

    public static string Dir { get; } = ResolveDir();
    public static string ConfigPath { get; } = Path.Combine(Dir, "settings.json");
    public static string BackupPath { get; } = Path.Combine(Dir, "settings.bak.json");

    private static string ResolveDir()
    {
        try
        {
            uint len = 0;
            if (Interop.Win32.GetCurrentPackageFullName(ref len, nint.Zero) != 15700)
            {
                var local = Windows.Storage.ApplicationData.Current?.LocalFolder?.Path;
                if (!string.IsNullOrEmpty(local))
                    return Path.Combine(local, "win-plus");
            }
        }
        catch { }
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "win-plus");
    }

    public AppConfig Config { get; private set; } = new();

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
                if (cfg is not null)
                {
                    Config = cfg;
                    Migrate();
                    return;
                }
            }
            else if (File.Exists(BackupPath))
            {
                var json = File.ReadAllText(BackupPath);
                var cfg = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
                if (cfg is not null) { Config = cfg; Migrate(); Save(); return; }
            }
        }
        catch { }
        Config = new AppConfig();
        Migrate();
    }

    private void Migrate()
    {
        if (Config.Gestures.Count == 0) Config.Gestures = AppConfig.DefaultGestures();
        if (Config.Shortcuts.Count == 0) Config.Shortcuts.Add(new ShortcutBinding { Name = S.T("Default_ShortcutName") });
        if (Config.ShotProfiles.Count == 0) Config.ShotProfiles.Add(new ScreenshotProfile { Name = S.T("Default_ProfileName") });
        foreach (var s in Config.Shortcuts)
            if (string.IsNullOrWhiteSpace(s.Name)) s.Name = S.T("Default_ShortcutName");
        foreach (var p in Config.ShotProfiles)
            if (string.IsNullOrWhiteSpace(p.Name)) p.Name = S.T("Default_ProfileName");
        Config.Memo ??= new MemoSettings();
        Config.Memo.Bounds ??= new MemoWindowBounds();
        if (Config.Memo.AutoCloseSeconds is < 3 or > 60) Config.Memo.AutoCloseSeconds = 6;
        foreach (var p in Config.ShotProfiles)
        {
            if (p.PreviewSeconds is < 0 or > 30) p.PreviewSeconds = 5;
            if (string.IsNullOrWhiteSpace(p.Folder))
                p.Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "win-plus", p.Name);
        }
        Config.Version = 1;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(Config, AppJsonContext.Default.AppConfig);
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(ConfigPath))
                File.Copy(ConfigPath, BackupPath, overwrite: true);
            File.Move(tmp, ConfigPath, overwrite: true);
        }
        catch { }
    }
}
