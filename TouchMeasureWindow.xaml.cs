using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinPlus.Services;

namespace WinPlus;

public sealed partial class TouchMeasureWindow : Window
{
    private readonly Action<int> _done;
    private bool _finished;
    private readonly DispatcherQueueTimer _timeout;
    private readonly DispatcherQueueTimer _poll;
    private TouchMeasureService? _service;

    public TouchMeasureWindow(Action<int> done)
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
        try
        {
            _service = new TouchMeasureService();
            if (!_service.Available)
            {
                Hint.Text = S.T("Touch_MeasureNoDevice");
                _service.Dispose();
                _service = null;
                var t = DispatcherQueue.CreateTimer();
                t.Interval = TimeSpan.FromSeconds(2.5);
                t.Tick += (_, _) => { t.Stop(); Finish(-2); };
                t.Start();
            }
            else
            {
                Hint.Text = S.T("Touch_MeasureHint");
                _service.Live += count =>
                {
                    App.RunOnUi(() => { Live.Text = S.T("Touch_MeasureLive", count); });
                };
            }
        }
        catch
        {
            Hint.Text = S.T("Touch_MeasureNoDevice");
        }
        Overlay.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape) Finish(-1);
        };
        _poll = DispatcherQueue.CreateTimer();
        _poll.Interval = TimeSpan.FromMilliseconds(200);
        _poll.Tick += (_, _) => Poll();
        _poll.Start();
        _timeout = DispatcherQueue.CreateTimer();
        _timeout.Interval = TimeSpan.FromSeconds(15);
        _timeout.Tick += (_, _) => { _timeout.Stop(); Finish(_service is not null && _service.MaxSeen > 0 ? _service.MaxSeen : -1); };
        _timeout.Start();
        Closed += (_, _) => Finish(-1);
    }

    private void Poll()
    {
        try
        {
            var s = _service;
            if (s is null || s.MaxSeen <= 0) return;
            if ((DateTime.Now - s.LastActive).TotalMilliseconds > 600)
                Finish(s.MaxSeen);
        }
        catch { }
    }

    private void Finish(int count)
    {
        if (_finished) return;
        _finished = true;
        try
        {
            _timeout.Stop();
            _poll.Stop();
            _service?.Dispose();
            _service = null;
            Close();
        }
        catch { }
        _done(count);
    }
}
