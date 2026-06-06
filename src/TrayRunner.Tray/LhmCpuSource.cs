#if LHM_AVAILABLE
using LibreHardwareMonitor.Hardware;

namespace TrayRunner.Tray;

/// <summary>
/// <see cref="ICpuUsageSource"/> 的 Windows 實作:透過 LibreHardwareMonitor 讀取
/// 整機 CPU 總使用率("CPU Total" 的 Load 感測值)。
///
/// LibreHardwareMonitor 為 Windows 專屬,故本檔以 <c>LHM_AVAILABLE</c> 編譯常數包覆,
/// 僅在引入該套件的建置中參與編譯(參見 csproj 條件式相依)。<see cref="Computer"/> 具
/// 執行緒親和性考量,<see cref="Computer.Open"/>/<see cref="Update"/>/<see cref="Dispose"/>
/// 預期由 <see cref="HardwarePollingService"/> 的同一條背景執行緒循序呼叫。
/// </summary>
public sealed class LhmCpuSource : ICpuUsageSource
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private bool _opened;

    public LhmCpuSource()
    {
        _computer = new Computer { IsCpuEnabled = true };
        _computer.Open();
        _opened = true;
    }

    public double ReadCpuUsage()
    {
        if (!_opened)
            return double.NaN;

        _computer.Accept(_visitor);

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Load &&
                    sensor.Name == "CPU Total" &&
                    sensor.Value is float value &&
                    !float.IsNaN(value))
                {
                    return value;
                }
            }
        }

        // 感測值尚未就緒或找不到:回傳 NaN,交由輪詢服務略過 / 沿用前值。
        return double.NaN;
    }

    public void Dispose()
    {
        if (!_opened)
            return;

        _computer.Close();
        _opened = false;
    }

    /// <summary>
    /// LibreHardwareMonitor 標準的更新訪問者:遞迴呼叫各硬體的 <c>Update()</c>,
    /// 使感測值在讀取前先被刷新。
    /// </summary>
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }
}
#endif
