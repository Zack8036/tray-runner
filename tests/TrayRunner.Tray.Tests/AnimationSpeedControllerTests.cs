using TrayRunner.Tray;
using Xunit;

namespace TrayRunner.Tray.Tests;

public class AnimationSpeedControllerTests
{
    [Fact]
    public void MinimumLoad_MapsToSlowestInterval()
    {
        var interval = AnimationSpeedController.CalculateInterval(0);
        Assert.Equal(300d, interval.TotalMilliseconds, precision: 6);
    }

    [Fact]
    public void MaximumLoad_MapsToFastestInterval()
    {
        var interval = AnimationSpeedController.CalculateInterval(100);
        Assert.Equal(20d, interval.TotalMilliseconds, precision: 6);
    }

    [Fact]
    public void MidLoad_FollowsExponentialCurve_NotLinearMidpoint()
    {
        var interval = AnimationSpeedController.CalculateInterval(50);

        // 300 · (1/15)^0.5 = 300/√15 ≈ 77.46ms,而非線性中點 160ms
        Assert.Equal(77.46d, interval.TotalMilliseconds, precision: 2);
        Assert.NotEqual(160d, interval.TotalMilliseconds, precision: 0);
    }

    [Theory]
    [InlineData(140, 20)]   // 超過 100 → 鉗制為 100
    [InlineData(-10, 300)]  // 負值 → 鉗制為 0
    public void OutOfRangeInput_IsClamped(double cpu, double expectedMs)
    {
        var interval = AnimationSpeedController.CalculateInterval(cpu);
        Assert.Equal(expectedMs, interval.TotalMilliseconds, precision: 6);
    }

    [Fact]
    public void EachEqualCpuStep_ProducesEqualMultiplicativeChange()
    {
        // 指數映射的性質:相鄰等距 CPU 點的間隔比值固定
        var i0 = AnimationSpeedController.CalculateInterval(0).TotalMilliseconds;
        var i25 = AnimationSpeedController.CalculateInterval(25).TotalMilliseconds;
        var i50 = AnimationSpeedController.CalculateInterval(50).TotalMilliseconds;
        var i75 = AnimationSpeedController.CalculateInterval(75).TotalMilliseconds;

        var r1 = i25 / i0;
        var r2 = i50 / i25;
        var r3 = i75 / i50;

        // 數學上三個比值相等;浮點累積誤差約在第 7 位,故用 precision 5 比較。
        Assert.Equal(r1, r2, precision: 5);
        Assert.Equal(r2, r3, precision: 5);
    }
}
