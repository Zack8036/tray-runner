namespace TrayRunner.Tray;

/// <summary>
/// 中性的「使用率」取樣來源抽象,範圍 [0, 100],由 CPU 與記憶體共用。
///
/// 整機 CPU%/記憶體% 在 .NET 都沒有純跨平台 API(Windows 走效能計數器 / LibreHardwareMonitor /
/// <c>GlobalMemoryStatusEx</c>,macOS 需 P/Invoke mach),因此以此介面作為依平台切換實作的接縫。
/// CPU 來源見 <see cref="LhmCpuSource"/> / <see cref="MacCpuSource"/>,記憶體來源見
/// <see cref="WindowsMemorySource"/> / <see cref="MacMemorySource"/>,任一平台皆以
/// <see cref="RandomUsageSource"/> 後援。
///
/// 實作可能持有非受控資源(如 LibreHardwareMonitor 的底層控制代碼),故繼承
/// <see cref="IDisposable"/>;由背景輪詢服務在停止時負責釋放。
/// </summary>
public interface IUsageSource : IDisposable
{
    /// <summary>
    /// 讀取目前的使用率,範圍 [0, 100]。
    /// </summary>
    /// <returns>
    /// 使用率百分比;若該次無法取得有效讀數(例如感測值尚未就緒),
    /// 回傳 <see cref="double.NaN"/>,由呼叫端決定略過或沿用前值。
    /// </returns>
    double ReadUsage();
}
