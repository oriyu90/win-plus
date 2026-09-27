using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using WinPlus.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace WinPlus;

public sealed partial class MemoWindow : Window
{
    private readonly DispatcherQueueTimer _saveTimer;
    private readonly DispatcherQueueTimer _tickTimer;
    private readonly DispatcherQueueTimer _boundsTimer;
    private DateTime _lastActivity = DateTime.Now;
    private bool _loaded;
    private bool _suppressText;

    private sealed class ImageRow
    {
        public MemoImage Item { get; init; } = null!;
        public BitmapImage Preview { get; init; } = null!;
        public string FullPath { get; init; } = "";
        public string DeleteLabel { get; init; } = "";
    }

    public MemoWindow()
    {
        InitializeComponent();
        Title = $"win-plus {S.T("Nav_Memo")}";
        try
        {
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = true;
            AppWindow.SetPresenter(presenter);
            ApplyBounds(restore: false);
        }
        catch { }
        ApplyTheme();
        _saveTimer = DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveText(); };
        _tickTimer = DispatcherQueue.CreateTimer();
        _tickTimer.Interval = TimeSpan.FromSeconds(1);
        _tickTimer.Tick += (_, _) => Tick();
        _boundsTimer = DispatcherQueue.CreateTimer();
        _boundsTimer.Interval = TimeSpan.FromMilliseconds(800);
        _boundsTimer.Tick += (_, _) => { _boundsTimer.Stop(); SaveBounds(); };
        AppWindow.Changed += (_, e) =>
        {
            try
            {
                if (e.DidPositionChange || e.DidSizeChange)
                {
                    Touch();
                    _boundsTimer.Stop();
                    _boundsTimer.Start();
                }
            }
            catch { }
        };
        MemoBox.TextChanged += (_, _) => { Touch(); if (!_suppressText) { _saveTimer.Stop(); _saveTimer.Start(); } };
        MemoBox.Paste += MemoBox_Paste;
        if (Content is FrameworkElement root)
        {
            root.KeyDown += Root_KeyDown;
            root.PointerMoved += (_, _) => Touch();
            root.PointerPressed += (_, _) => Touch();
        }
        AppWindow.Closing += (s, e) =>
        {
            if (!AppState.Current.RealExitRequested) { e.Cancel = true; s.Hide(); }
        };
    }

    public void ApplyTheme()
    {
        try
        {
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = AppState.Current.Config.Memo.Theme switch
                {
                    "Dark" => ElementTheme.Dark,
                    "Light" => ElementTheme.Light,
                    _ => ElementTheme.Default,
                };
            }
        }
        catch { }
    }

    private void ApplyBounds(bool restore)
    {
        try
        {
            var cfg = AppState.Current.Config.Memo.Bounds;
            var vs = ScreenshotService.VirtualScreen();
            if (restore && cfg is not null && cfg.Custom)
            {
                int w = Math.Clamp(cfg.Width, 280, Math.Max(280, vs.Width));
                int h = Math.Clamp(cfg.Height, 200, Math.Max(200, vs.Height));
                int x = Math.Clamp(cfg.X, vs.X, vs.X + Math.Max(0, vs.Width - 80));
                int y = Math.Clamp(cfg.Y, vs.Y, vs.Y + Math.Max(0, vs.Height - 40));
                AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h));
                AppLogger.Write($"memo bounds restored {x},{y},{w}x{h}");
                return;
            }
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var work = area.WorkArea;
            int dw = Math.Min(460, Math.Max(300, work.Width - 40));
            int dh = Math.Min(420, Math.Max(280, work.Height - 40));
            AppWindow.Resize(new Windows.Graphics.SizeInt32(dw, dh));
        }
        catch { }
    }

    private void SaveBounds()
    {
        try
        {
            var pos = AppWindow.Position;
            var size = AppWindow.Size;
            var b = AppState.Current.Config.Memo.Bounds ??= new WinPlus.Models.MemoWindowBounds();
            b.Custom = true;
            b.X = pos.X;
            b.Y = pos.Y;
            b.Width = size.Width;
            b.Height = size.Height;
            AppState.Current.Save();
        }
        catch { }
    }

    public static void ResetBounds()
    {
        try
        {
            AppState.Current.Config.Memo.Bounds = new WinPlus.Models.MemoWindowBounds();
            AppState.Current.Save();
            AppLogger.Write("memo bounds reset");
        }
        catch { }
    }

    public void ShowAndFocus()
    {
        try
        {
            ApplyTheme();
            MemoBox.PlaceholderText = S.T("MemoWin_TextBoxPh");
            CloseButton.Content = S.T("MemoWin_Close");
            Countdown.Text = S.T("MemoWin_Countdown", AppState.Current.Config.Memo.AutoCloseSeconds);
            if (!_loaded)
            {
                _loaded = true;
                LoadData();
            }
            ApplyBounds(restore: true);
            AppWindow.Show();
            Activate();
            Touch();
            _tickTimer.Start();
            MemoBox.Focus(FocusState.Programmatic);
            MemoBox.SelectionStart = MemoBox.Text.Length;
            MemoBox.SelectionLength = 0;
        }
        catch { }
    }

    private void LoadData()
    {
        var data = AppState.Current.Memo.Data;
        _suppressText = true;
        MemoBox.Text = data.Text ?? "";
        _suppressText = false;
        RefreshImages();
        MemoBox.SelectionStart = MemoBox.Text.Length;
    }

    private void RefreshImages()
    {
        var rows = new List<ImageRow>();
        foreach (var img in AppState.Current.Memo.Data.Images)
        {
            try
            {
                var full = AppState.Current.Memo.ImagePath(img);
                var bmp = new BitmapImage(new Uri(full));
                rows.Add(new ImageRow { Item = img, Preview = bmp, FullPath = full, DeleteLabel = S.T("MemoWin_DeleteImage") });
            }
            catch { }
        }
        ImageList.ItemsSource = rows;
    }

    private void SaveText()
    {
        try
        {
            AppState.Current.Memo.Data.Text = MemoBox.Text;
            AppState.Current.Memo.Save();
        }
        catch { }
    }

    private void Touch() => _lastActivity = DateTime.Now;

    private void Tick()
    {
        try
        {
            int seconds = AppState.Current.Config.Memo.AutoCloseSeconds;
            var remain = seconds - (int)(DateTime.Now - _lastActivity).TotalSeconds;
            if (remain <= 0)
            {
                SaveText();
                _tickTimer.Stop();
                AppWindow.Hide();
                return;
            }
            Countdown.Text = S.T("MemoWin_Countdown", remain);
        }
        catch { }
    }

    private async void MemoBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        e.Handled = true;
        Touch();
        try
        {
            var pkg = Clipboard.GetContent();
            if (pkg.Contains(StandardDataFormats.Text))
            {
                var text = await pkg.GetTextAsync();
                var box = (TextBox)sender;
                int pos = box.SelectionStart;
                box.Text = box.Text[..pos] + text + box.Text[(pos + box.SelectionLength)..];
                box.SelectionStart = pos + text.Length;
                box.SelectionLength = 0;
            }
        }
        catch { }
    }

    private void Root_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        Touch();
        try
        {
            bool ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            if (ctrl && e.Key == Windows.System.VirtualKey.V)
            {
                try { _ = TryPasteImageAsync(Clipboard.GetContent()); } catch { }
            }
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                SaveText();
                _tickTimer.Stop();
                AppWindow.Hide();
            }
        }
        catch { }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            Touch();
            var view = e.DataView;
            if (view.Contains(StandardDataFormats.StorageItems) || view.Contains(StandardDataFormats.Bitmap))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = S.T("MemoWin_DropCaption");
                e.DragUIOverride.IsCaptionVisible = true;
            }
        }
        catch { }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            await TryPasteImageAsync(e.DataView);
        }
        catch { }
        finally { deferral.Complete(); }
    }

    private async Task TryPasteImageAsync(DataPackageView pkg)
    {
        Touch();
        try
        {
            if (pkg.Contains(StandardDataFormats.Bitmap))
            {
                var streamRef = await pkg.GetBitmapAsync();
                using var ras = await streamRef.OpenReadAsync();
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(ras);
                var sb = await decoder.GetSoftwareBitmapAsync();
                var added = await AppState.Current.Memo.AddImageFromBitmapAsync(sb);
                if (added is not null) RefreshImages();
                return;
            }
            if (pkg.Contains(StandardDataFormats.StorageItems))
            {
                var items = await pkg.GetStorageItemsAsync();
                bool any = false;
                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        var added = AppState.Current.Memo.AddImageFile(file.Path);
                        if (added is not null) any = true;
                    }
                }
                if (any) RefreshImages();
                else AppState.Current.Tray.ShowBalloon("win-plus", S.T("MemoWin_ImagesOnly"));
                return;
            }
            AppState.Current.Tray.ShowBalloon("win-plus", S.T("MemoWin_NothingToPaste"));
        }
        catch { }
    }

    private void DeleteImage_Click(object sender, RoutedEventArgs e)
    {
        Touch();
        if ((sender as Button)?.Tag is MemoImage img)
        {
            AppState.Current.Memo.RemoveImage(img);
            RefreshImages();
        }
    }

    private void Image_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        Touch();
        string? path = ((sender as FrameworkElement)?.DataContext as ImageRow)?.FullPath;
        if (path is null || !File.Exists(path))
        {
            foreach (var row in ImageList.ItemsSource as List<ImageRow> ?? new())
            {
                if (File.Exists(row.FullPath)) { path = row.FullPath; break; }
            }
        }
        if (path is null) return;
        string captured = path;
        e.Data.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var d = request.GetDeferral();
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(captured);
                request.SetData(new object[] { file });
            }
            catch { }
            finally { d.Complete(); }
        });
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SaveText();
        _tickTimer.Stop();
        AppWindow.Hide();
    }
}
