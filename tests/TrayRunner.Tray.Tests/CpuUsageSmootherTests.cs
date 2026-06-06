using TrayRunner.Tray;
using Xunit;

namespace TrayRunner.Tray.Tests;

public class CpuUsageSmootherTests
{
    [Fact]
    public void FirstSample_IsUsedAsSeed_NotMixed()
    {
        var smoother = new CpuUsageSmoother(alpha: 0.3d);

        // 第一筆直接成為輸出,不與任何先前值(預設 0)混合。
        Assert.Equal(80d, smoother.Add(80d), precision: 9);
    }

    [Fact]
    public void SubsequentSample_FollowsEmaFormula()
    {
        var smoother = new CpuUsageSmoother(alpha: 0.3d);
        smoother.Add(0d); // 種子 = 0

        // 0.3 · 100 + 0.7 · 0 = 30
        Assert.Equal(30d, smoother.Add(100d), precision: 9);
        // 0.3 · 100 + 0.7 · 30 = 51
        Assert.Equal(51d, smoother.Add(100d), precision: 9);
    }

    [Fact]
    public void ConstantInput_ConvergesTowardThatValue()
    {
        var smoother = new CpuUsageSmoother(alpha: 0.3d);
        smoother.Add(0d); // 種子 = 0

        double last = 0d;
        for (var i = 0; i < 100; i++)
            last = smoother.Add(50d);

        // 持續餵入 50,平滑值應收斂到接近 50。
        Assert.Equal(50d, last, precision: 6);
    }

    [Fact]
    public void Spike_IsDampened_NotPassedThrough()
    {
        var smoother = new CpuUsageSmoother(alpha: 0.3d);
        smoother.Add(10d); // 種子穩定在低檔

        var afterSpike = smoother.Add(90d);

        // 單一尖刺被吸收成「往上推一把」,而非直接跳到 90。
        Assert.True(afterSpike < 90d);
        Assert.True(afterSpike > 10d);
        Assert.Equal(34d, afterSpike, precision: 9); // 0.3·90 + 0.7·10
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1.5d)]
    [InlineData(-0.1d)]
    public void InvalidAlpha_Throws(double alpha)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CpuUsageSmoother(alpha));
    }
}
