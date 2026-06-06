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

    /// <summary>
    /// 於執行期更新影格間隔。Avalonia 的 <see cref="DispatcherTimer.Interval"/> setter
    /// 在計時器運行中會立即重排,因此直接指派即可;<c>_frameIndex</c> 不受影響,
    /// 動畫不會重置回第一格。
    /// </summary>
    public void SetInterval(TimeSpan interval)
    {
        _timer.Interval = interval;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _frameIndex = (_frameIndex + 1) % _pool.Count;
        _trayIcon.Icon = _pool[_frameIndex];
    }
}
