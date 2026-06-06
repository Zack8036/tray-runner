namespace TrayRunner.Tray;

/// <summary>
/// 整機 CPU 總使用率的取樣來源抽象。
///
/// 整機 CPU% 在 .NET 沒有純跨平台 API(Windows 走效能計數器 / LibreHardwareMonitor,
/// macOS 需 P/Invoke <c>host_processor_info</c>),因此以此介面作為依平台切換實作的接縫:
/// Windows 用 <see cref="LhmCpuSource"/>、其他平台用 <see cref="CpuSimulator"/> 後援。
///
/// 實作可能持有非受控資源(如 LibreHardwareMonitor 的底層控制代碼),故繼承
/// <see cref="IDisposable"/>;由背景輪詢服務在停止時負責釋放。
/// </summary>
public interface ICpuUsageSource : IDisposable
{
    /// <summary>
    /// 讀取目前整機 CPU 總使用率,範圍 [0, 100]。
    /// </summary>
    /// <returns>
    /// CPU 使用率百分比;若該次無法取得有效讀數(例如感測值尚未就緒),
    /// 回傳 <see cref="double.NaN"/>,由呼叫端決定略過或沿用前值。
    /// </returns>
    double ReadCpuUsage();
}
