namespace TrayRunner.Tray;

/// <summary>
/// <see cref="IUsageSource"/> 的後援實作:每次取樣回傳一個 0–100 的隨機值,
/// 由 CPU 與記憶體共用。
///
/// 在無真實取樣來源、或平台對應的真實來源建立失敗時使用,讓面板仍能顯示變動數值、
/// 動態變速仍能運作以供視覺驗證。本類別僅為被輪詢的取樣來源,不自行驅動計時、也不接觸
/// 動畫迴圈——取樣週期與封送一律交由 <see cref="HardwarePollingService"/> 與 App 層負責。
/// </summary>
public sealed class RandomUsageSource : IUsageSource
{
    public double ReadUsage() => Random.Shared.NextDouble() * 100d;

    public void Dispose()
    {
        // 無非受控資源可釋放。
    }
}
