using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TrayRunner.Tray;

/// <summary>
/// <see cref="IUsageSource"/> 的 Windows 記憶體實作:透過 P/Invoke 呼叫 kernel32
/// <c>GlobalMemoryStatusEx</c>,直接取其 <c>dwMemoryLoad</c> 欄位 ——「目前使用中的實體
/// 記憶體百分比(0–100)」,由作業系統算好,無需自行換算。
///
/// 刻意不使用 LibreHardwareMonitor:實體記憶體使用率是 OS 內建可直接取得的資訊,
/// 走 kernel32 比掛 LHM 感測器更簡單,也讓記憶體取樣與 CPU 的 LHM <c>Computer</c>
/// 完全脫鉤(不共用具執行緒親和性的資源、無需 <c>LHM_AVAILABLE</c> 條件編譯)。
///
/// 與 <see cref="MacCpuSource"/> 同理:kernel32 只是對作業系統內建 DLL 的
/// <see cref="DllImportAttribute"/>,任何平台都能編譯、僅執行期解析;改以 <see cref="App"/>
/// 工廠的 <c>OperatingSystem.IsWindows()</c> 守住不在非 Windows 上實例化,並標註
/// <see cref="SupportedOSPlatformAttribute"/> 供平台分析器檢查。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsMemorySource : IUsageSource
{
    public double ReadUsage()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };

        if (!GlobalMemoryStatusEx(ref status))
            // 呼叫失敗:回傳 NaN,交由輪詢服務略過 / 沿用前值。
            return double.NaN;

        // dwMemoryLoad 已是 [0, 100] 的整數百分比。
        return status.MemoryLoad;
    }

    public void Dispose()
    {
        // 無非受控資源可釋放。
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>
    /// 對應 Win32 <c>MEMORYSTATUSEX</c>。呼叫前 <see cref="Length"/> 必須設為本結構大小,
    /// 否則 <c>GlobalMemoryStatusEx</c> 會失敗。本實作只用到 <see cref="MemoryLoad"/>,
    /// 其餘欄位保留以符合原生結構配置。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
