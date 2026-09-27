using System.Diagnostics;
using System.IO;

namespace WinPlus.Services;

public static class UninstallService
{
    public static bool IsPackaged()
    {
        try
        {
            uint len = 0;
            return Interop.Win32.GetCurrentPackageFullName(ref len, nint.Zero) != 15700;
        }
        catch { return false; }
    }

    public static string OwnPackageName()
    {
        try
        {
            var pkg = Windows.ApplicationModel.Package.Current;
            return pkg?.Id?.Name ?? "yuki-orita.win-plus";
        }
        catch { return "yuki-orita.win-plus"; }
    }

    public static void Cleanup(bool deleteData, bool deleteShots)
    {
        try { AppLogger.Write($"uninstall cleanup data={deleteData} shots={deleteShots}"); } catch { }
        try { _ = StartupManager.ApplyAsync(false); } catch { }
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            key?.DeleteValue("win-plus", throwOnMissingValue: false);
        }
        catch { }
        if (deleteData)
        {
            try
            {
                if (Directory.Exists(ConfigStore.Dir))
                    Directory.Delete(ConfigStore.Dir, recursive: true);
            }
            catch { }
        }
        if (deleteShots)
        {
            try
            {
                var pics = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "win-plus");
                if (Directory.Exists(pics))
                    Directory.Delete(pics, recursive: true);
            }
            catch { }
        }
    }

    public static void RemoveOwnPackage()
    {
        try
        {
            string name = OwnPackageName();
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Start-Sleep -Seconds 3; Get-AppxPackage -Name '" + name + "' | Remove-AppxPackage\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);
        }
        catch { }
    }
}
