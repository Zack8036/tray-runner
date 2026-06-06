using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TrayRunner.Tray;

public partial class App : Application
{
    private IconPool? _iconPool;
    private TrayAnimationLoop? _animLoop;
    private HardwarePollingService? _polling;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var assemblyName = typeof(App).Assembly.GetName().Name;
            var frameUris = Enumerable.Range(1, 4)
                .Select(i => new Uri($"avares://{assemblyName}/Assets/runner_frame_{i}.png"))
                .ToList();

            _iconPool = new IconPool(frameUris);

            var trayIcons = TrayIcon.GetIcons(this);
            if (trayIcons is { Count: > 0 })
            {
                var tray = trayIcons[0];
                tray.Icon = _iconPool[0];
                _animLoop = new TrayAnimationLoop(tray, _iconPool);
                _animLoop.Start();

                // 背景輪詢:每 1 秒取樣真實 CPU 使用率(Windows 用 LibreHardwareMonitor,
                // 其他平台用後援模擬器),經 EMA 平滑後封送至 UI 緒調整動畫速度。
                _polling = new HardwarePollingService(CreateCpuSource, log: Console.WriteLine);
                _polling.CpuSampled += OnCpuSampled;
                _polling.Start();
            }

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 依執行平台建立取樣來源。Windows 用 LibreHardwareMonitor、macOS 用 mach
    /// <c>host_statistics</c> 差分讀取真實 CPU;任一平台真實來源建立失敗則退回隨機模擬器,
    /// 其他平台亦以模擬器後援。此工廠在輪詢服務的背景緒上被呼叫,使來源的建立、取樣與
    /// 釋放全程位於同一條緒。
    /// </summary>
    private static ICpuUsageSource CreateCpuSource()
    {
#if LHM_AVAILABLE
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return new LhmCpuSource();
            }
            catch (Exception ex)
            {
                // LibreHardwareMonitor 初始化失敗(如防毒擋掉驅動):
                // 退回模擬器,讓動畫仍能變速,而非整個取樣失效。
                Console.WriteLine($"LibreHardwareMonitor 初始化失敗,改用模擬器:{ex.Message}");
            }
        }
#endif
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                return new MacCpuSource();
            }
            catch (Exception ex)
            {
                // mach 取樣建立失敗:退回模擬器,與 Windows 端 LHM 退回邏輯對稱。
                Console.WriteLine($"macOS CPU 取樣建立失敗,改用模擬器:{ex.Message}");
            }
        }

        return new CpuSimulator();
    }

    /// <summary>
    /// 背景緒上的取樣事件處理:封送至 UI 緒,於 UI 緒換算間隔並更新動畫迴圈。
    /// 封送的工作刻意維持輕量(一次換算 + 一次設定間隔),不影響動畫流暢度。
    /// </summary>
    private void OnCpuSampled(double cpuPercent)
    {
        Dispatcher.UIThread.Post(() =>
            _animLoop?.SetInterval(AnimationSpeedController.CalculateInterval(cpuPercent)));
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _polling?.Dispose();
        _animLoop?.Stop();
        _iconPool?.Dispose();
    }
}
