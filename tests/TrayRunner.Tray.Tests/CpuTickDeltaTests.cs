using TrayRunner.Tray;
using Xunit;

namespace TrayRunner.Tray.Tests;

public class CpuTickDeltaTests
{
    [Fact]
    public void NoPreviousSnapshot_ReturnsNaN()
    {
        // 第一次取樣尚無前值:回傳 NaN,交由輪詢服務略過。
        var result = CpuTickDelta.Compute(previous: null, current: new CpuTicks(100, 50, 800, 10));

        Assert.True(double.IsNaN(result));
    }

    [Fact]
    public void ZeroTotalDelta_ReturnsNaN()
    {
        // 兩次快照完全相同 -> 總 tick 差為 0:避免除以零,視為無效。
        var snap = new CpuTicks(100, 50, 800, 10);

        Assert.True(double.IsNaN(CpuTickDelta.Compute(snap, snap)));
    }

    [Fact]
    public void TypicalDelta_ComputesBusyOverTotal()
    {
        var prev = new CpuTicks(User: 100, System: 50, Idle: 800, Nice: 10);
        // Δ: user+25, system+25, idle+150, nice+0 -> busy=50, total=200 -> 25%
        var current = new CpuTicks(User: 125, System: 75, Idle: 950, Nice: 10);

        Assert.Equal(25d, CpuTickDelta.Compute(prev, current), precision: 9);
    }

    [Fact]
    public void FullyBusyDelta_ReturnsHundred()
    {
        var prev = new CpuTicks(0, 0, 0, 0);
        // 全部增量都在 busy 桶、idle 無推進 -> 100%
        var current = new CpuTicks(User: 60, System: 30, Idle: 0, Nice: 10);

        Assert.Equal(100d, CpuTickDelta.Compute(prev, current), precision: 9);
    }

    [Fact]
    public void FullyIdleDelta_ReturnsZero()
    {
        var prev = new CpuTicks(10, 10, 10, 10);
        // 增量全在 idle -> 0%
        var current = new CpuTicks(User: 10, System: 10, Idle: 110, Nice: 10);

        Assert.Equal(0d, CpuTickDelta.Compute(prev, current), precision: 9);
    }

    [Fact]
    public void CounterWrapAround_StillComputesCorrectDelta()
    {
        // user 由 uint.MaxValue-10 回繞到 39 -> 實際增量 50;idle +150。
        // busy = 50,total = 200 -> 25%。
        var prev = new CpuTicks(User: uint.MaxValue - 10, System: 50, Idle: 800, Nice: 10);
        var current = new CpuTicks(User: 39, System: 50, Idle: 950, Nice: 10);

        var result = CpuTickDelta.Compute(prev, current);

        Assert.Equal(25d, result, precision: 9);
    }

    [Theory]
    [InlineData(125u, 75u, 950u, 10u)]
    [InlineData(1000u, 1000u, 1000u, 1000u)]
    [InlineData(5u, 0u, 1u, 0u)]
    public void Result_AlwaysWithinZeroToHundred(uint u, uint s, uint i, uint n)
    {
        var prev = new CpuTicks(100, 50, 800, 10);
        var current = new CpuTicks(100 + u, 50 + s, 800 + i, 10 + n);

        var result = CpuTickDelta.Compute(prev, current);

        Assert.InRange(result, 0d, 100d);
    }
}
