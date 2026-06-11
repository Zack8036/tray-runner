namespace TrayRunner.Tray;

/// <summary>
/// 某一瞬間的 macOS 虛擬記憶體頁面統計(取自 mach <c>host_statistics64(HOST_VM_INFO64)</c>),
/// 加上頁面大小與實體記憶體總量(後者取自 <c>sysctl hw.memsize</c>)。各頁數對應
/// <c>vm_statistics64</c> 的同名欄位,單位為「頁」。
/// </summary>
/// <param name="PageSizeBytes">單一頁面的位元組數(<c>host_page_size</c>)。</param>
/// <param name="TotalBytes">實體記憶體總量(位元組,<c>hw.memsize</c>)。</param>
/// <param name="ActivePages">使用中(active)頁數。</param>
/// <param name="WiredPages">鎖定(wired,不可換出)頁數。</param>
/// <param name="CompressedPages">壓縮器佔用(compressor_page_count)頁數。</param>
public readonly record struct MacVmStats(
    ulong PageSizeBytes,
    ulong TotalBytes,
    ulong ActivePages,
    ulong WiredPages,
    ulong CompressedPages);

/// <summary>
/// 把 macOS 頁面統計換算為整機記憶體使用率 [0, 100] 的純函式。
///
/// 「已用」的定義刻意對齊「活動監視器」的「記憶體用量(Memory Used)」近似:
/// <code>used = active + wired + compressed(以頁計,再乘頁面大小換成位元組)</code>
/// 亦即把 free / inactive / purgeable / speculative 視為「可回收 / 可用」而不計入已用。
/// 此定義為本專案定版;若日後於 macOS 實機與活動監視器比對需微調,只需改本函式並更新測試。
///
/// 本類別刻意為純函式(不摸任何 P/Invoke / 平台 API),使換算數學可在任何平台被單元測試;
/// P/Invoke 殼層(<see cref="MacMemorySource"/>)只負責取頁面統計快照並填入 <see cref="MacVmStats"/>。
/// </summary>
public static class MacMemoryUsage
{
    /// <summary>
    /// 由一筆頁面統計快照計算記憶體使用率 [0, 100]。
    /// </summary>
    /// <returns>使用率百分比;當總量為 0(無效讀數)時回傳 <see cref="double.NaN"/>。</returns>
    public static double Compute(MacVmStats stats)
    {
        if (stats.TotalBytes == 0)
            return double.NaN;

        // 以 ulong 累加頁數避免溢位,再乘頁面大小換成位元組。
        ulong usedPages = stats.ActivePages + stats.WiredPages + stats.CompressedPages;
        ulong usedBytes = usedPages * stats.PageSizeBytes;

        var pct = (double)usedBytes / stats.TotalBytes * 100d;

        // 理論上 used 不應超過 total,但頁面統計與 hw.memsize 來源不同、取樣有時間差,
        // 夾限到 [0, 100] 以免面板出現 >100% 的怪數字。
        return Math.Clamp(pct, 0d, 100d);
    }
}
