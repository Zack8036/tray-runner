using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace TrayRunner.Tray;

public partial class App : Application
{
    private IconPool? _iconPool;
    private TrayAnimationLoop? _animLoop;

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
        _animLoop?.Stop();
        _iconPool?.Dispose();
    }
}
