namespace TrayRunner.Tray;

/// <summary>
/// 某一瞬間自開機以來的整機 CPU 累計 tick 計數,分四個狀態桶。
/// 對應 macOS mach <c>host_cpu_load_info</c> 的 <c>cpu_ticks[CPU_STATE_*]</c>,
/// 型別為 32-bit 無號(<c>natural_t</c>),長時間運行可能回繞。
/// </summary>
public readonly record struct CpuTicks(uint User, uint System, uint Idle, uint Nice);

/// <summary>
/// 把「相隔一個取樣週期的兩筆累計 tick 快照」換算為該期間的整機 CPU 總使用率。
///
/// mach 給的是自開機以來單調遞增的累計值而非當下使用率,故必須前後相減:
/// <code>usage% = Δ(user+system+nice) / Δ(user+system+idle+nice) × 100</code>
///
/// 本類別刻意為純函式(不摸任何 P/Invoke / 平台 API),使取樣的核心數學可在
/// 任何平台被單元測試;P/Invoke 殼層(<see cref="MacCpuSource"/>)只負責取快照並保存前值。
/// </summary>
public static class CpuTickDelta
{
    /// <summary>
    /// 由前後兩筆快照計算使用率 [0, 100]。
    /// </summary>
    /// <param name="previous">
    /// 前一次快照;若為 <c>null</c>(即第一次取樣尚無前值)回傳 <see cref="double.NaN"/>,
    /// 交由輪詢服務略過該次。
    /// </param>
    /// <param name="current">本次快照。</param>
    /// <returns>使用率百分比;無前值或總 tick 差為 0 時回傳 <see cref="double.NaN"/>。</returns>
    public static double Compute(CpuTicks? previous, CpuTicks current)
    {
        if (previous is not { } prev)
            return double.NaN;

        // 以無號相減取差;32-bit 計數回繞時,unchecked 無號減法的環繞語意
        // 仍會得到正確的「這段期間增加了多少 tick」。再以 ulong 累加避免溢位。
        ulong dUser = unchecked(current.User - prev.User);
        ulong dSystem = unchecked(current.System - prev.System);
        ulong dIdle = unchecked(current.Idle - prev.Idle);
        ulong dNice = unchecked(current.Nice - prev.Nice);

        ulong busy = dUser + dSystem + dNice;
        ulong total = busy + dIdle;

        // 兩次快照之間毫無 tick 推進(理論上不應發生):避免除以零,視為無效讀數。
        if (total == 0)
            return double.NaN;

        return (double)busy / total * 100d;
    }
}
