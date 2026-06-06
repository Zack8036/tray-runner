using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TrayRunner.Tray;

public partial class App : Application
{
    private IconPool? _iconPool;
    private TrayAnimationLoop? _animLoop;
    private CpuLoadSimulator? _cpuSimulator;

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

                // 模擬器:每 3 秒餵入隨機 CPU 負載,動態改變動畫速度以供視覺驗證。
                _cpuSimulator = new CpuLoadSimulator(_animLoop, Console.WriteLine);
                _cpuSimulator.Start();
            }

            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnQuitClicked(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _cpuSimulator?.Stop();
        _animLoop?.Stop();
        _iconPool?.Dispose();
    }
}
