using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TrayRunner.Tray;

/// <summary>
/// <see cref="ICpuUsageSource"/> 的 macOS 實作:透過 P/Invoke 呼叫 mach
/// <c>host_statistics(HOST_CPU_LOAD_INFO)</c> 讀取整機(已跨核心加總)的累計 CPU tick,
/// 再以前後兩次快照的差分(委派純函式 <see cref="CpuTickDelta"/>)換算整機 CPU 總使用率。
///
/// mach API 只是對系統內建 <c>libSystem.dylib</c> 的 <see cref="DllImportAttribute"/>,
/// 任何平台都能編譯、僅執行期解析,故本檔不需條件編譯常數(對比 Windows 專屬 NuGet 套件
/// 的 <see cref="LhmCpuSource"/> 需 <c>LHM_AVAILABLE</c>);改以 <see cref="App"/> 工廠的
/// <c>OperatingSystem.IsMacOS()</c> 守住不在非 macOS 上實例化,並標註
/// <see cref="SupportedOSPlatformAttribute"/> 供平台分析器檢查。
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacCpuSource : ICpuUsageSource
{
    private const string LibSystem = "libSystem.dylib";

    // host_statistics 的 flavor 與其回傳資料的 integer_t 計數(host_cpu_load_info = 4 個 natural_t)。
    private const int HostCpuLoadInfoFlavor = 3;
    private const uint HostCpuLoadInfoCount = 4;
    private const int KernSuccess = 0;

    private readonly uint _host;
    private CpuTicks? _prev;

    public MacCpuSource()
    {
        // mach_host_self() 取得 host name port。此處刻意只呼叫「一次」並持有,
        // 而非每次取樣都呼叫,因此不會累積 port 參考。
        _host = mach_host_self();
    }

    public double ReadCpuUsage()
    {
        var info = default(HostCpuLoadInfo);
        var count = HostCpuLoadInfoCount;

        var kr = host_statistics(_host, HostCpuLoadInfoFlavor, ref info, ref count);
        if (kr != KernSuccess)
            // mach 呼叫失敗:回傳 NaN,交由輪詢服務略過 / 沿用前值。
            return double.NaN;

        var current = new CpuTicks(info.User, info.System, info.Idle, info.Nice);
        var usage = CpuTickDelta.Compute(_prev, current);
        _prev = current;

        // 第一次取樣 _prev 為 null -> CpuTickDelta 回傳 NaN,被輪詢服務略過;
        // 第二次起才有相隔一週期的差分,產生有效讀數。
        return usage;
    }

    public void Dispose()
    {
        // 任務 2.3 結論:不呼叫 mach_port_deallocate。
        // mach_host_self() 在建構子僅被呼叫一次(非每次取樣),故不會有累積的 port 洩漏;
        // 這唯一一筆 host port 參考在 process 結束時由核心隱式回收。如此可避免為了釋放而
        // 去匯入 mach_task_self_(它是 libSystem 的全域變數而非函式,P/Invoke 取用脆弱)。
    }

    [DllImport(LibSystem)]
    private static extern uint mach_host_self();

    [DllImport(LibSystem)]
    private static extern int host_statistics(
        uint hostPriv, int flavor, ref HostCpuLoadInfo info, ref uint count);

    /// <summary>
    /// 對應 mach <c>host_cpu_load_info</c>:<c>cpu_ticks[CPU_STATE_MAX]</c>,
    /// 順序為 USER(0)、SYSTEM(1)、IDLE(2)、NICE(3),皆為 32-bit 無號 <c>natural_t</c>。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct HostCpuLoadInfo
    {
        public uint User;
        public uint System;
        public uint Idle;
        public uint Nice;
    }
}
