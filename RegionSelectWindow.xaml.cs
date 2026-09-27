using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinPlus.Services;
using Windows.Foundation;

namespace WinPlus;

public sealed partial class RegionSelectWindow : Window
{
    private readonly Action<ScreenshotService.ShotBounds?> _done;
    private Point _start;
    private bool _dragging;
    private bool _finished;

    public RegionSelectWindow(Action<ScreenshotService.ShotBounds?> done)
    {
        InitializeComponent();
        _done = done;
        Title = "win-plus";
        try
        {
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            AppWindow.SetPresenter(presenter);
            var vs = ScreenshotService.VirtualScreen();
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(vs.X, vs.Y, Math.Max(320, vs.Width), Math.Max(200, vs.Height)));
        }
        catch
        {
            try { AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen); } catch { }
        }
        Overlay.PointerPressed += OnPressed;
        Overlay.PointerMoved += OnMoved;
        Overlay.PointerReleased += OnReleased;
        Hint.Text = WinPlus.Services.S.T("Region_Hint");
        Overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape) Finish(null);
        };
        Closed += (_, _) => Finish(null);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        _start = e.GetCurrentPoint(SelCanvas).Position;
        _dragging = true;
        SelRect.Visibility = Visibility.Visible;
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(SelRect, _start.X);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(SelRect, _start.Y);
        SelRect.Width = 0;
        SelRect.Height = 0;
        Overlay.CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetCurrentPoint(SelCanvas).Position;
        double x = Math.Min(_start.X, p.X), y = Math.Min(_start.Y, p.Y);
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(SelRect, x);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(SelRect, y);
        SelRect.Width = Math.Abs(p.X - _start.X);
        SelRect.Height = Math.Abs(p.Y - _start.Y);
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        var p2 = e.GetCurrentPoint(SelCanvas).Position;
        double scale = 1.0;
        try { scale = Content.XamlRoot?.RasterizationScale ?? 1.0; } catch { }
        int x = (int)(Math.Min(_start.X, p2.X) * scale);
        int y = (int)(Math.Min(_start.Y, p2.Y) * scale);
        int w = (int)(Math.Abs(p2.X - _start.X) * scale);
        int h = (int)(Math.Abs(p2.Y - _start.Y) * scale);
        if (w < 4 || h < 4) { Finish(null); return; }
        var vs = ScreenshotService.VirtualScreen();
        Finish(new ScreenshotService.ShotBounds(vs.X + x, vs.Y + y, w, h));
    }

    private void Finish(ScreenshotService.ShotBounds? bounds)
    {
        if (_finished) return;
        _finished = true;
        try
        {
            Closed -= (_, _) => Finish(null);
            Close();
        }
        catch { }
        _done(bounds);
    }
}
