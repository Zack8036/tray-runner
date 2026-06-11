using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace TrayRunner.Tray;

/// <summary>
/// 托盤下拉狀態面板:無系統裝飾、透明背景、OS 原生模糊的毛玻璃視窗,顯示 CPU 與記憶體
/// 兩張即時指標卡片。由 <see cref="App"/> 持有單一實例並以顯示/隱藏 toggle(關閉攔截為隱藏,
/// 不銷毀、不結束應用程式)。數值更新方法預期於 UI 執行緒被呼叫(由 App 封送)。
/// </summary>
public partial class StatusPanelWindow : Window
{
    /// <summary>
    /// 由 App 在結束時設為 true,使 <see cref="OnClosing"/> 放行真正的關閉(銷毀);
    /// 平時為 false,關閉一律被攔截為隱藏。
    /// </summary>
    public bool AllowClose { get; set; }

    // 使用者是否曾手動拖曳過視窗。一旦拖過就尊重其位置,不再於下次顯示時拉回托盤角落。
    private bool _userMoved;

    // 載入 XAML 後以 FindControl 解析具名控制項並持有,避免依賴 x:Name 欄位產生器
    // (手寫 InitializeComponent 時該欄位接線不會自動產生,直接存取會是 null)。
    private readonly TextBlock _cpuPercent;
    private readonly ProgressBar _cpuBar;
    private readonly TextBlock _memPercent;
    private readonly ProgressBar _memBar;

    public StatusPanelWindow()
    {
        AvaloniaXamlLoader.Load(this);

        _cpuPercent = this.FindControl<TextBlock>("CpuPercentText")!;
        _cpuBar = this.FindControl<ProgressBar>("CpuBar")!;
        _memPercent = this.FindControl<TextBlock>("MemPercentText")!;
        _memBar = this.FindControl<ProgressBar>("MemBar")!;
    }

    /// <summary>以平滑後的 CPU 使用率([0,100])更新卡片。需於 UI 執行緒呼叫。</summary>
    public void UpdateCpu(double percent) => Apply(_cpuPercent, _cpuBar, percent);

    /// <summary>以平滑後的記憶體使用率([0,100])更新卡片。需於 UI 執行緒呼叫。</summary>
    public void UpdateMemory(double percent) => Apply(_memPercent, _memBar, percent);

    private static void Apply(TextBlock text, ProgressBar bar, double percent)
    {
        if (double.IsNaN(percent))
            return;

        var clamped = Math.Clamp(percent, 0d, 100d);
        text.Text = $"{clamped:F0}%";
        bar.Value = clamped;
    }

    /// <summary>
    /// 在玻璃面板任一非互動處按住左鍵即可拖曳整個視窗(無系統標題列,改由此提供搬移)。
    /// 拖過之後標記 <see cref="_userMoved"/>,使後續顯示尊重使用者擺放的位置。
    /// </summary>
    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _userMoved = true;
        BeginMoveDrag(e);
    }

    /// <summary>
    /// 視窗首次開啟時,延後到版面量測完成後定位到「狀態列附近」的螢幕角落:
    /// Windows 托盤在右下、macOS 選單列在右上。這是近似定位(精準貼齊托盤圖示需要
    /// 圖示螢幕座標,Avalonia 無法提供,仍列為後續)。OnOpened 當下 Bounds 尚為 0,
    /// 故以 Dispatcher 延後一拍,待實際尺寸就緒再算角落。
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(PositionNearStatusArea, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(RoundCornersOnMac, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// macOS 限定:把視窗自身的 NSView 圖層裁成與 <c>RootBorder</c> 相同的圓角,讓 OS 模糊層
    /// (NSVisualEffectView)不再於失焦時從四角露出矩形底版。圖層需待視窗顯示後才存在,
    /// 故與定位一樣以 Dispatcher 延後;Border 半徑 12 對齊裁切半徑。
    /// </summary>
    private void RoundCornersOnMac()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        if (TryGetPlatformHandle()?.Handle is { } handle)
            MacWindowCorner.Round(handle, 12);
    }

    private void PositionNearStatusArea()
    {
        if (_userMoved)
            return;

        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;          // 實體像素,已扣掉工作列
        var scale = screen.Scaling;
        // Bounds 為 DIP,換算成實體像素以對齊 WorkingArea。
        var w = (int)(Bounds.Width * scale);
        var h = (int)(Bounds.Height * scale);
        var margin = (int)(12 * scale);

        // 右側對齊兩平台一致;垂直方向依狀態列位置:macOS 貼上緣、Windows 貼下緣。
        var x = area.X + area.Width - w - margin;
        var y = OperatingSystem.IsMacOS()
            ? area.Y + margin
            : area.Y + area.Height - h - margin;

        Position = new PixelPoint(x, y);
    }

    /// <summary>
    /// 攔截關閉:取消預設的銷毀,改為隱藏。如此單一實例得以保留、再次喚出即顯示,
    /// 且因 <c>ShutdownMode.OnExplicitShutdown</c> 也不會因關窗而結束應用程式。
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
