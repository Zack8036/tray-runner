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
    private HardwarePollingService? _cpuPolling;
    private HardwarePollingService? _memPolling;
    private StatusPanelWindow? _panel;

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

                // 單一面板實例:啟動時即建立但不顯示,使背景取樣能持續更新卡片,
                // 「顯示面板」選單項只負責 toggle 其顯示/隱藏(關閉攔截為隱藏,不銷毀)。
                _panel = new StatusPanelWindow();

                // CPU 輪詢:每 1 秒取樣真實 CPU 使用率(Windows 用 LibreHardwareMonitor、
                // macOS 用 mach,其他平台用隨機後援),經 EMA 平滑後封送至 UI 緒:
                // 既調整動畫速度,也更新面板 CPU 卡片。
                _cpuPolling = new HardwarePollingService(
                    CreateCpuSource, log: Console.WriteLine, threadName: "CpuPolling");
                _cpuPolling.Sampled += OnCpuSampled;
                _cpuPolling.Start();

                // 記憶體輪詢:與 CPU 各自獨立的實例(Windows 用 GlobalMemoryStatusEx、
                // macOS 用 mach/sysctl,其他平台用隨機後援),平滑後封送至 UI 緒更新面板記憶體卡片。
                _memPolling = new HardwarePollingService(
                    CreateMemorySource, log: Console.WriteLine, threadName: "MemoryPolling");
                _memPolling.Sampled += OnMemorySampled;
                _memPolling.Start();
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
    private static IUsageSource CreateCpuSource()
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

        return new RandomUsageSource();
    }

    /// <summary>
    /// 依執行平台建立記憶體取樣來源。Windows 用 kernel32 <c>GlobalMemoryStatusEx</c>、
    /// macOS 用 mach <c>host_statistics64</c>;任一平台真實來源建立失敗則退回隨機後援,
    /// 其他平台亦以後援。與 <see cref="CreateCpuSource"/> 對稱,且不牽涉 LibreHardwareMonitor,
    /// 故無 <c>LHM_AVAILABLE</c> 條件編譯。此工廠在記憶體輪詢服務的背景緒上被呼叫。
    /// </summary>
    private static IUsageSource CreateMemorySource()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return new WindowsMemorySource();
            }
            catch (Exception ex)
            {
                // GlobalMemoryStatusEx 失敗極罕見;退回隨機後援讓面板仍能顯示數值。
                Console.WriteLine($"Windows 記憶體取樣建立失敗,改用後援:{ex.Message}");
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            try
            {
                return new MacMemorySource();
            }
            catch (Exception ex)
            {
                // mach / sysctl 取樣建立失敗:退回後援,與 Windows 端退回邏輯對稱。
                Console.WriteLine($"macOS 記憶體取樣建立失敗,改用後援:{ex.Message}");
            }
        }

        return new RandomUsageSource();
    }

    /// <summary>
    /// 背景緒上的 CPU 取樣事件處理:封送至 UI 緒,於 UI 緒換算間隔並更新動畫迴圈,
    /// 同時更新面板的 CPU 卡片。封送的工作刻意維持輕量,不影響動畫流暢度。
    /// </summary>
    private void OnCpuSampled(double cpuPercent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _animLoop?.SetInterval(AnimationSpeedController.CalculateInterval(cpuPercent));
            _panel?.UpdateCpu(cpuPercent);
        });
    }

    /// <summary>
    /// 背景緒上的記憶體取樣事件處理:封送至 UI 緒更新面板的記憶體卡片。
    /// </summary>
    private void OnMemorySampled(double memPercent)
    {
        Dispatcher.UIThread.Post(() => _panel?.UpdateMemory(memPercent));
    }

    /// <summary>
    /// 「顯示面板」選單項:toggle 單一面板實例。已顯示則隱藏,否則顯示並帶到前景。
    /// 兩平台共用此單一路徑(刻意不依賴 macOS 不觸發的 <c>TrayIcon.Clicked</c>)。
    /// </summary>
    private void OnShowPanelClicked(object? sender, EventArgs e)
    {
        if (_panel is null)
            return;

        if (_panel.IsVisible)
        {
            _panel.Hide();
        }
        else
        {
            _panel.Show();
            _panel.Activate();
        }
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        // 結束前先解除面板的關閉攔截:面板平時把關閉轉為隱藏(AllowClose=false),
        // 若 Shutdown 過程中面板仍攔截自身關閉,理論上可能阻塞關機。此處顯式放行,
        // 使 Quit 不依賴 Avalonia 各版本對「關閉取消 vs 強制關機」的處理差異。
        if (_panel is not null)
            _panel.AllowClose = true;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _cpuPolling?.Dispose();
        _memPolling?.Dispose();
        _animLoop?.Stop();
        _iconPool?.Dispose();

        // 面板攔截關閉為隱藏,故結束時需顯式關閉:先解除攔截再 Close,避免無法銷毀。
        if (_panel is not null)
        {
            _panel.AllowClose = true;
            _panel.Close();
            _panel = null;
        }
    }
}
