namespace WinPlus.Services;

public static class StartupManager
{
    public static async Task ApplyAsync(bool enable)
    {
        try
        {
            var task = await Windows.ApplicationModel.StartupTask.GetAsync("win-plusStartup");
            if (enable)
                await task.RequestEnableAsync();
            else
                task.Disable();
            return;
        }
        catch { }
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null) return;
            if (enable)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe)) key.SetValue("win-plus", $"\"{exe}\" --start-hidden");
            }
            else key.DeleteValue("win-plus", throwOnMissingValue: false);
        }
        catch { }
    }
}
