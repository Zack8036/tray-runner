namespace TrayRunner.Tray;

/// <summary>
/// 將 CPU 使用率(0–100)映射到動畫影格間隔。
///
/// 採用指數(對數線性)曲線:<c>interval = 300 · (1/15)^(cpu/100)</c>,
/// 在 0% CPU 時為 300ms、100% CPU 時為 20ms。選擇指數而非線性是因為
/// 人眼感知的是影格率(間隔的倒數),指數曲線讓 CPU 每增加相同幅度
/// 都帶來相同倍率的速度變化,整個範圍都有回饋,而非把加速擠在高負載區。
///
/// 本類別為純計算,不持有狀態、也不接觸任何計時器。
/// </summary>
public static class AnimationSpeedController
{
    /// <summary>0% CPU(最低負載)對應的最慢間隔。</summary>
    public static readonly TimeSpan SlowestInterval = TimeSpan.FromMilliseconds(300);

    /// <summary>100% CPU(最高負載)對應的最快間隔。</summary>
    public static readonly TimeSpan FastestInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// 將 CPU 使用率映射到影格間隔。輸入會先鉗制到 [0, 100]。
    /// </summary>
    /// <param name="cpuPercent">CPU 使用率百分比;超出 [0, 100] 的值會被鉗制。</param>
    /// <returns>動畫影格間隔,介於 <see cref="FastestInterval"/> 與 <see cref="SlowestInterval"/> 之間。</returns>
    public static TimeSpan CalculateInterval(double cpuPercent)
    {
        var cpu = Math.Clamp(cpuPercent, 0d, 100d);

        // interval = 300 · (20/300)^(cpu/100) = 300 · (1/15)^(cpu/100)
        var ratio = FastestInterval.TotalMilliseconds / SlowestInterval.TotalMilliseconds;
        var milliseconds = SlowestInterval.TotalMilliseconds * Math.Pow(ratio, cpu / 100d);

        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
