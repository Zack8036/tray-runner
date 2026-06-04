using Avalonia.Controls;
using Avalonia.Threading;

namespace TrayRunner.Tray;

public sealed class TrayAnimationLoop
{
    private readonly TrayIcon _trayIcon;
    private readonly IconPool _pool;
    private readonly DispatcherTimer _timer;
    private int _frameIndex;

    public TrayAnimationLoop(TrayIcon trayIcon, IconPool pool)
    {
        _trayIcon = trayIcon;
        _pool = pool;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (!_timer.IsEnabled)
            _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _frameIndex = (_frameIndex + 1) % _pool.Count;
        _trayIcon.Icon = _pool[_frameIndex];
    }
}
