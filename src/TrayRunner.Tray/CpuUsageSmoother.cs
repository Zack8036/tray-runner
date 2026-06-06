namespace TrayRunner.Tray;

/// <summary>
/// 對 1Hz 原始 CPU 取樣套用指數移動平均(EMA)平滑:
/// <c>smoothed = α · 新取樣 + (1 − α) · 前一次 smoothed</c>。
///
/// 真實 CPU 使用率每秒尖刺嚴重,若直接驅動動畫會讓角色神經質地暴衝又龜速。
/// EMA 讓速度朝新取樣平順地移動一小步,只需記住一個數字(前一次平滑值)。
/// 第一筆取樣直接作為種子,避免啟動時從假性低負載慢慢爬升。
///
/// 本類別為純邏輯、不具執行緒安全保證;預期由單一背景輪詢執行緒循序呼叫。
/// </summary>
public sealed class CpuUsageSmoother
{
    private readonly double _alpha;
    private double _smoothed;
    private bool _seeded;

    /// <param name="alpha">
    /// 平滑係數,介於 (0, 1]。越大反應越快但越跳,越小越平順但越鈍;預設 0.3。
    /// </param>
    public CpuUsageSmoother(double alpha = 0.3d)
    {
        if (alpha is <= 0d or > 1d)
            throw new ArgumentOutOfRangeException(nameof(alpha), alpha, "alpha 必須介於 (0, 1]。");

        _alpha = alpha;
    }

    /// <summary>
    /// 餵入一筆原始取樣並回傳平滑後的值。第一筆直接成為種子。
    /// </summary>
    public double Add(double sample)
    {
        if (!_seeded)
        {
            _smoothed = sample;
            _seeded = true;
        }
        else
        {
            _smoothed = _alpha * sample + (1d - _alpha) * _smoothed;
        }

        return _smoothed;
    }
}
