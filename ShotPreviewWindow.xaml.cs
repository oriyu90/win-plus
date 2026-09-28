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
    private sealed record QrLink(string Url, string Display);

    private readonly string _path;
    private readonly DispatcherQueueTimer _timer;
    private readonly DispatcherQueue _ui;
    private int _remain;

    public ShotPreviewWindow(string path, int seconds)
    {
        InitializeComponent();
        _path = path;
        _remain = Math.Clamp(seconds, 1, 30);
        _ui = DispatcherQueue.GetForCurrentThread();
        Title = "win-plus";
        OpenButton.Content = WinPlus.Services.S.T("Preview_Open");
        FolderButton.Content = WinPlus.Services.S.T("Preview_Folder");
        CloseButton.Content = WinPlus.Services.S.T("Preview_Close");
        QrSection.Visibility = Visibility.Collapsed;
        QrBlockedNote.Visibility = Visibility.Collapsed;
        try
        {
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            AppWindow.SetPresenter(presenter);
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            int w = Math.Min(400, Math.Max(280, work.Width - 20));
            int h = Math.Min(380, Math.Max(240, work.Height - 20));
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
            AppWindow.Move(new Windows.Graphics.PointInt32(
                work.X + Math.Max(0, work.Width - w - 20),
                work.Y + Math.Max(0, work.Height - h - 20)));
        }
        catch { }
        try { Preview.Source = new BitmapImage(new Uri(path)); } catch { }
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
        _ = LoadQrAsync();
    }

    private async Task LoadQrAsync()
    {
        WinPlus.Services.QrCodeService.DecodeResult result;
        try { result = await WinPlus.Services.QrCodeService.DecodeFromFileAsync(_path).ConfigureAwait(false); }
        catch { return; }
        if (result.Links.Count == 0 && result.Blocked == 0) return;
        try
        {
            _ui.TryEnqueue(() =>
            {
                try { ShowQrLinks(result); } catch { }
            });
        }
        catch { }
    }

    private void ShowQrLinks(WinPlus.Services.QrCodeService.DecodeResult result)
    {
        if (result.Links.Count == 0 && result.Blocked == 0) return;
        var items = new List<QrLink>(result.Links.Count);
        foreach (var url in result.Links)
            items.Add(new QrLink(url, TrimDisplay(url)));
        QrList.ItemsSource = items;
        QrHeader.Text = WinPlus.Services.S.T("Qr_Title", items.Count);
        QrSection.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (result.Blocked > 0 && items.Count > 0)
        {
            QrBlockedNote.Text = WinPlus.Services.S.T("Qr_BlockedNote", result.Blocked);
            QrBlockedNote.Visibility = Visibility.Visible;
        }
        else if (result.Blocked > 0)
        {
            // Only unsafe payloads found: show the section with the note so the
            // user knows something was hidden, but no clickable link.
            QrHeader.Text = WinPlus.Services.S.T("Qr_Title", 0);
            QrBlockedNote.Text = WinPlus.Services.S.T("Qr_BlockedNote", result.Blocked);
            QrBlockedNote.Visibility = Visibility.Visible;
            QrSection.Visibility = Visibility.Visible;
        }
        if (QrSection.Visibility == Visibility.Visible) GrowForQr(items.Count);
    }

    private static string TrimDisplay(string url)
    {
        const int max = 72;
        return url.Length > max ? url[..max] + "…" : url;
    }

    private void GrowForQr(int count)
    {
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            var size = AppWindow.Size;
            int extra = Math.Min(220, 28 + Math.Min(count, 10) * 36 + 20);
            int h = Math.Min(size.Height + extra, Math.Max(240, work.Height - 20));
            int w = Math.Min(420, Math.Max(size.Width, 280));
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
            AppWindow.Move(new Windows.Graphics.PointInt32(
                work.X + Math.Max(0, work.Width - w - 20),
                work.Y + Math.Max(0, work.Height - h - 20)));
        }
        catch { }
    }

    private void QrLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? raw = (sender as HyperlinkButton)?.Tag as string;
            if (!WinPlus.Services.QrCodeService.TrySanitizeUrl(raw, out string url)) return;
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
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
