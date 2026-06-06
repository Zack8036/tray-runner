using Avalonia.Threading;

namespace TrayRunner.Tray;

/// <summary>
/// 假的 CPU 負載來源:每 3 秒產生一個 0–100 的隨機值,經
/// <see cref="AnimationSpeedController"/> 換算成影格間隔後套用到動畫迴圈,
/// 用來在真實 CPU 取樣機制出現之前以肉眼驗證動態變速效果。
///
/// 計時器運行於 UI 執行緒,因此可直接呼叫 <see cref="TrayAnimationLoop.SetInterval"/>,
/// 無需跨執行緒封送。未來換成真實取樣器時,這裡正是接縫所在。
/// </summary>
public sealed class CpuLoadSimulator
{
    private readonly TrayAnimationLoop _loop;
    private readonly DispatcherTimer _timer;
    private readonly Action<string>? _log;

    public CpuLoadSimulator(TrayAnimationLoop loop, Action<string>? log = null)
    {
        _loop = loop;
        _log = log;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        if (_timer.IsEnabled)
            return;

        OnTick(this, EventArgs.Empty); // 立即套用第一個值,不必等 3 秒
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var cpu = Random.Shared.NextDouble() * 100d;
        var interval = AnimationSpeedController.CalculateInterval(cpu);
        _loop.SetInterval(interval);
        _log?.Invoke($"CPU {cpu:F1}% -> {interval.TotalMilliseconds:F0}ms");
    }
}
