using System.IO;
using System.Text;

namespace WinPlus.Services;

public static class AppLogger
{
    private static readonly object _lock = new();
    private static string _path = "";

    public static string FallbackPath { get; } = Path.Combine(
        Path.GetTempPath(), "win-plus-logs", "app.log");

    public static void Init()
    {
        var dirs = new[] { Path.Combine(ConfigStore.Dir, "logs"), Path.GetDirectoryName(FallbackPath) ?? Path.GetTempPath() };
        foreach (var dir in dirs)
        {
            try
            {
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "app.log");
                try
                {
                    var fi = new FileInfo(_path);
                    if (fi.Exists && fi.Length > 200 * 1024)
                    {
                        var bak = Path.Combine(dir, "app.prev.log");
                        File.Copy(_path, bak, overwrite: true);
                        File.WriteAllText(_path, "");
                    }
                }
                catch { }
                Write("app started");
                return;
            }
            catch { }
        }
    }

    public static void Write(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_path)) return;
            lock (_lock)
            {
                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch { }
    }

    public static void Error(string where, Exception ex)
    {
        Write($"ERROR {where}: {ex.GetType().Name}: {ex.Message}");
    }
}
