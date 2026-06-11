using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TrayRunner.Tray;

/// <summary>
/// <see cref="IUsageSource"/> 的 macOS 記憶體實作:透過 P/Invoke 呼叫 mach
/// <c>host_statistics64(HOST_VM_INFO64)</c> 取整機虛擬記憶體頁面統計、<c>host_page_size</c>
/// 取頁面大小、<c>sysctlbyname("hw.memsize")</c> 取實體記憶體總量,再委派純函式
/// <see cref="MacMemoryUsage"/> 換算使用率。
///
/// 與 <see cref="MacCpuSource"/> 同理:mach / sysctl 都是對系統內建 <c>libSystem.dylib</c>
/// 的 <see cref="DllImportAttribute"/>,任何平台都能編譯、僅執行期解析,故不需條件編譯常數;
/// 改以 <see cref="App"/> 工廠的 <c>OperatingSystem.IsMacOS()</c> 守住不在非 macOS 上實例化。
/// 記憶體為「當下絕對用量」而非累計差分,故無需保存前值,第一次取樣即為有效讀數。
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacMemorySource : IUsageSource
{
    private const string LibSystem = "libSystem.dylib";

    private const int HostVmInfo64Flavor = 4;
    private const int KernSuccess = 0;

    private readonly uint _host;
    private readonly ulong _pageSize;
    private readonly ulong _totalBytes;

    public MacMemorySource()
    {
        _host = mach_host_self();

        // 頁面大小與實體記憶體總量在執行期固定,建構時取一次即可。
        if (host_page_size(_host, out var pageSize) != KernSuccess || pageSize == 0)
            throw new InvalidOperationException("host_page_size 取得頁面大小失敗。");
        _pageSize = (ulong)pageSize;

        _totalBytes = ReadTotalPhysicalBytes();
        if (_totalBytes == 0)
            throw new InvalidOperationException("sysctl hw.memsize 取得實體記憶體總量失敗。");
    }

    public double ReadUsage()
    {
        var info = default(VmStatistics64);
        // count 以 integer_t(32-bit)為單位,等於結構大小 / sizeof(int)。
        var count = (uint)(Marshal.SizeOf<VmStatistics64>() / sizeof(int));

        var kr = host_statistics64(_host, HostVmInfo64Flavor, ref info, ref count);
        if (kr != KernSuccess)
            // mach 呼叫失敗:回傳 NaN,交由輪詢服務略過 / 沿用前值。
            return double.NaN;

        var stats = new MacVmStats(
            PageSizeBytes: _pageSize,
            TotalBytes: _totalBytes,
            ActivePages: info.ActiveCount,
            WiredPages: info.WireCount,
            CompressedPages: info.CompressorPageCount);

        return MacMemoryUsage.Compute(stats);
    }

    public void Dispose()
    {
        // 與 MacCpuSource 一致:mach_host_self() 僅於建構時呼叫一次,不累積 port 參考,
        // 唯一一筆 host port 於 process 結束時由核心隱式回收,故不主動 deallocate。
    }

    private static ulong ReadTotalPhysicalBytes()
    {
        long value = 0;
        var len = (nuint)sizeof(long);
        var rc = sysctlbyname("hw.memsize", ref value, ref len, IntPtr.Zero, 0);
        if (rc != 0 || value <= 0)
            return 0;
        return (ulong)value;
    }

    [DllImport(LibSystem)]
    private static extern uint mach_host_self();

    [DllImport(LibSystem)]
    private static extern int host_page_size(uint host, out nuint pageSize);

    [DllImport(LibSystem)]
    private static extern int host_statistics64(
        uint hostPriv, int flavor, ref VmStatistics64 info, ref uint count);

    [DllImport(LibSystem, CharSet = CharSet.Ansi)]
    private static extern int sysctlbyname(
        string name, ref long oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);

    /// <summary>
    /// 對應 mach <c>vm_statistics64</c>(HOST_VM_INFO64)。欄位順序與型別必須與原生結構一致,
    /// 否則 marshalling 後的偏移會錯位。<c>natural_t</c> 為 32-bit 無號(<see cref="uint"/>),
    /// 其餘累計統計為 <see cref="ulong"/>。本實作只讀取 active / wire / compressor 三個頁數。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct VmStatistics64
    {
        public uint FreeCount;
        public uint ActiveCount;
        public uint InactiveCount;
        public uint WireCount;
        public ulong ZeroFillCount;
        public ulong Reactivations;
        public ulong Pageins;
        public ulong Pageouts;
        public ulong Faults;
        public ulong CowFaults;
        public ulong Lookups;
        public ulong Hits;
        public ulong Purges;
        public uint PurgeableCount;
        public uint SpeculativeCount;
        public ulong Decompressions;
        public ulong Compressions;
        public ulong Swapins;
        public ulong Swapouts;
        public uint CompressorPageCount;
        public uint ThrottledCount;
        public uint ExternalPageCount;
        public uint InternalPageCount;
        public ulong TotalUncompressedPagesInCompressor;
    }
}
