using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinPlus.Services;

namespace WinPlus;

public partial class App : Application
{
    private static MainWindow? _mainWindow;
    public static Window? Window => _mainWindow;
    public static DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static nint WindowHandle
    {
        get
        {
            try
            {
                var w = _mainWindow;
                return w is null ? nint.Zero : WinRT.Interop.WindowNative.GetWindowHandle(w);
            }
            catch { return nint.Zero; }
        }
    }

    private static Mutex? _mutex;
    private MemoWindow? _memoWindow;
    private RegionSelectWindow? _regionWindow;
    private readonly List<ShotPreviewWindow> _previews = new();

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            try { AppLogger.Error("unhandled", e.Exception); } catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (args.Arguments.Contains("--uninstall-clean")
            || Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--uninstall-clean", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                UninstallService.Cleanup(deleteData: true, deleteShots: false);
                if (UninstallService.IsPackaged())
                    UninstallService.RemoveOwnPackage();
            }
            catch { }
            Environment.Exit(0);
            return;
        }
        try
        {
            _mutex = new Mutex(true, "win-plus_SingleInstance_Mutex_v1", out bool createdNew);
            if (!createdNew)
            {
                Environment.Exit(0);
                return;
            }
        }
        catch (AbandonedMutexException)
        {
            try { _mutex = new Mutex(true, "win-plus_SingleInstance_Mutex_v1", out _); } catch { }
        }
        catch
        {
            Environment.Exit(0);
            return;
        }
        bool silentStart = args.Arguments.Contains("--start-hidden")
            || Environment.GetCommandLineArgs().Any(a => string.Equals(a, "--start-hidden", StringComparison.OrdinalIgnoreCase));
        try
        {
            var activated = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activated?.Kind == ExtendedActivationKind.StartupTask)
                silentStart = true;
        }
        catch { }

        try { AppLogger.Init(); } catch { DumpFatal("logger-init", null); }
        try
        {
            AppState.Current.Init();
            S.ApplyLanguage(AppState.Current.Config.Language);
        }
        catch (Exception ex)
        {
            try { AppLogger.Error("init", ex); } catch { }
            DumpFatal("init", ex);
        }
        try { ApplyTheme(); } catch { }

        DispatcherQueue = DispatcherQueue.GetForCurrentThread();

        var state = AppState.Current;
        state.Tray.OpenSettings += () => RunOnUi(ShowMainWindow);
        state.Tray.OpenMemo += () => RunOnUi(ShowMemo);
        state.Tray.TakeScreenshot += () => RunOnUi(() => _ = TakeScreenshotAsync(null));
        state.Tray.ExitApp += () => RunOnUi(RealExit);
        state.ScreenshotRequested += name => RunOnUi(() => _ = TakeScreenshotAsync(name));
        state.MemoRequested += () => RunOnUi(ShowMemo);
        state.Actions.MemoRequested += () => RunOnUi(ShowMemo);
        state.Actions.ScreenshotRequested += name => RunOnUi(() => _ = TakeScreenshotAsync(name));
        state.Tray.Show(WinPlus.Services.S.T("Tray_Tooltip"));

        if (!silentStart)
            ShowMainWindow();
    }

    public static MainWindow EnsureMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            try { ApplyTheme(); } catch { }
        }
        return _mainWindow;
    }

    public static void RunOnUi(Action action)
    {
        try
        {
            if (DispatcherQueue is null) { action(); return; }
            if (DispatcherQueue.HasThreadAccess) action();
            else DispatcherQueue.TryEnqueue(() => { try { action(); } catch { } });
        }
        catch { }
    }

    private static void DumpFatal(string where, Exception? ex)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Services.AppLogger.FallbackPath) ?? System.IO.Path.GetTempPath();
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(Services.AppLogger.FallbackPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} FATAL {where}: {ex?.ToString() ?? "logger-init-failed"}{Environment.NewLine}");
        }
        catch { }
    }

    public static void ApplyTheme()
    {
        try
        {
            var theme = AppState.Current.Config.Memo.Theme;
            if (Window?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "Dark" => ElementTheme.Dark,
                    "Light" => ElementTheme.Light,
                    _ => ElementTheme.Default,
                };
            }
        }
        catch { }
    }

    private static void ShowMainWindow()
    {
        try
        {
            EnsureMainWindow().ShowFromTray();
        }
        catch { }
    }

    private void ShowMemo()
    {
        try
        {
            if (_memoWindow is null)
            {
                _memoWindow = new MemoWindow();
                _memoWindow.Closed += (_, _) => _memoWindow = null;
            }
            _memoWindow.ShowAndFocus();
        }
        catch { }
    }

    private async Task TakeScreenshotAsync(string? profileName)
    {
        try
        {
            var state = AppState.Current;
            var prof = string.IsNullOrEmpty(profileName)
                ? state.Config.ShotProfiles.FirstOrDefault()
                : state.Config.ShotProfiles.FirstOrDefault(p => p.Name == profileName);
            prof ??= state.Config.ShotProfiles.FirstOrDefault();
            if (prof is null) return;

            ScreenshotService.ShotBounds bounds;
            if (prof.Scope == "Active") bounds = ScreenshotService.ActiveWindow();
            else if (prof.Scope == "Region")
            {
                var region = await PickRegionAsync();
                if (region is null) return;
                bounds = region;
            }
            else bounds = ScreenshotService.VirtualScreen();

            AppLogger.Write($"shot start scope={prof.Scope} folder={prof.Folder}");
            var path = await Task.Run(() => state.Screenshots.CaptureAsync(bounds, prof.Folder, prof.Format, prof.IncludeCursor));
            if (path is null)
            {
                AppLogger.Write("shot failed");
                state.Tray.ShowBalloon("win-plus", S.T("Tray_ShotFailed"));
                return;
            }
            AppLogger.Write($"shot saved {path}");
            if (prof.PreviewSeconds > 0) ShowPreview(path, prof.PreviewSeconds);
            else state.Tray.ShowBalloon("win-plus", S.T("Tray_ShotSaved", path));
        }
        catch { }
    }

    private Task<ScreenshotService.ShotBounds?> PickRegionAsync()
    {
        var tcs = new TaskCompletionSource<ScreenshotService.ShotBounds?>();
        try
        {
            _regionWindow = new RegionSelectWindow(result =>
            {
                tcs.TrySetResult(result);
                _regionWindow = null;
            });
            _regionWindow.Activate();
        }
        catch { tcs.TrySetResult(null); }
        return tcs.Task;
    }

    private void ShowPreview(string path, int seconds)
    {
        try
        {
            var w = new ShotPreviewWindow(path, seconds);
            w.Closed += (_, _) => _previews.Remove(w);
            _previews.Add(w);
            while (_previews.Count > 3)
            {
                try { _previews[0].Close(); } catch { _previews.RemoveAt(0); }
            }
            w.Activate();
        }
        catch { }
    }

    private static void RealExit()
    {
        try
        {
            AppState.Current.RealExitRequested = true;
            AppState.Current.Save();
            AppState.Current.Memo.Save();
            AppState.Current.Tray.Dispose();
            if (Window is not null)
            {
                try { Window.Close(); } catch { }
            }
        }
        catch { }
        Environment.Exit(0);
    }
}
