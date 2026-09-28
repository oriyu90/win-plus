using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinPlus.Services;
using WinRT.Interop;

namespace WinPlus;

public sealed partial class MainWindow : Window
{
    private bool _positioned;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"win-plus {WinPlus.Services.S.T("General_Title")}";
        ExtendsContentIntoTitleBar = true;
        Activated += OnFirstActivated;
        AppWindow.Closing += OnClosing;
        Closed += OnWindowClosed;
        RootFrame.Navigate(typeof(MainPage));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_positioned) return;
        _positioned = true;
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var dpi = GetDpiForWindow(hwnd);
            double scale = dpi == 0 ? 1.0 : dpi / 96.0;
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            int w = Math.Min((int)(1000 * scale), Math.Max(360, work.Width - 40));
            int h = Math.Min((int)(720 * scale), Math.Max(320, work.Height - 40));
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
            int x = work.X + Math.Max(0, (work.Width - w) / 2);
            int y = work.Y + Math.Max(0, (work.Height - h) / 3);
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch { }
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!AppState.Current.RealExitRequested && AppState.Current.Config.MinimizeToTray)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (AppState.Current.RealExitRequested) return;
        if (!AppState.Current.Config.MinimizeToTray)
        {
            try
            {
                AppState.Current.RealExitRequested = true;
                AppState.Current.Save();
                AppState.Current.Memo.Save();
                AppState.Current.Tray.Dispose();
            }
            catch { }
            Environment.Exit(0);
        }
    }

    public void ShowFromTray()
    {
        try
        {
            AppWindow.Show();
            Activate();
        }
        catch { }
    }
}
