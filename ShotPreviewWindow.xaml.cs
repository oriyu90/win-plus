using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace WinPlus;

public sealed partial class ShotPreviewWindow : Window
{
    private readonly string _path;
    private readonly DispatcherQueueTimer _timer;
    private int _remain;

    public ShotPreviewWindow(string path, int seconds)
    {
        InitializeComponent();
        _path = path;
        _remain = Math.Clamp(seconds, 1, 30);
        Title = "win-plus";
        OpenButton.Content = WinPlus.Services.S.T("Preview_Open");
        FolderButton.Content = WinPlus.Services.S.T("Preview_Folder");
        CloseButton.Content = WinPlus.Services.S.T("Preview_Close");
        try
        {
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            AppWindow.SetPresenter(presenter);
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            int w = Math.Min(400, Math.Max(280, work.Width - 20));
            int h = Math.Min(340, Math.Max(240, work.Height - 20));
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
            AppWindow.Move(new Windows.Graphics.PointInt32(
                work.X + Math.Max(0, work.Width - w - 20),
                work.Y + Math.Max(0, work.Height - h - 20)));
        }
        catch { }
        Preview.Source = new BitmapImage(new Uri(path));
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) =>
        {
            _remain--;
            if (_remain <= 0) { _timer.Stop(); try { Close(); } catch { } return; }
            Countdown.Text = WinPlus.Services.S.T("Preview_Countdown", _remain);
        };
        Countdown.Text = WinPlus.Services.S.T("Preview_Countdown", _remain);
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void Preview_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var d = request.GetDeferral();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(_path);
                request.SetData(new object[] { file });
            }
            catch { }
            finally { d.Complete(); }
        });
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = _path, UseShellExecute = true }); } catch { }
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{_path}\"", UseShellExecute = true }); } catch { }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        try { Close(); } catch { }
    }
}
