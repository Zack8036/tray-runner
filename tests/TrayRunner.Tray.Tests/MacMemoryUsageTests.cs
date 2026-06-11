using TrayRunner.Tray;
using Xunit;

namespace TrayRunner.Tray.Tests;

public class MacMemoryUsageTests
{
    // 頁面大小固定 4 KiB,總量 1000 頁 = 4,096,000 bytes,方便心算百分比。
    private const ulong PageSize = 4096;
    private const ulong TotalBytes = PageSize * 1000;

    private static MacVmStats Stats(ulong active, ulong wired, ulong compressed)
        => new(PageSizeBytes: PageSize, TotalBytes: TotalBytes,
               ActivePages: active, WiredPages: wired, CompressedPages: compressed);

    [Fact]
    public void ZeroTotal_ReturnsNaN()
    {
        // 無效讀數(總量為 0):回傳 NaN,交由輪詢服務略過。
        var stats = new MacVmStats(PageSizeBytes: PageSize, TotalBytes: 0,
            ActivePages: 100, WiredPages: 100, CompressedPages: 100);

        Assert.True(double.IsNaN(MacMemoryUsage.Compute(stats)));
    }

    [Fact]
    public void TypicalStats_SumsActiveWiredCompressedOverTotal()
    {
        // used = 200+100+50 = 350 頁,佔 1000 頁 -> 35%
        Assert.Equal(35d, MacMemoryUsage.Compute(Stats(active: 200, wired: 100, compressed: 50)), precision: 9);
    }

    [Fact]
    public void NothingUsed_ReturnsZero()
    {
        Assert.Equal(0d, MacMemoryUsage.Compute(Stats(active: 0, wired: 0, compressed: 0)), precision: 9);
    }

    [Fact]
    public void FreeAndInactiveNotCounted_OnlyActiveWiredCompressed()
    {
        // 即使其他頁很多,只有 active+wired+compressed 計入;此處 used=100 頁 -> 10%
        Assert.Equal(10d, MacMemoryUsage.Compute(Stats(active: 60, wired: 30, compressed: 10)), precision: 9);
    }

    [Fact]
    public void UsedExceedingTotal_ClampedToHundred()
    {
        // 頁面統計與 hw.memsize 來源不同步可能使 used 略超過 total:夾限到 100,不出現怪數字。
        var result = MacMemoryUsage.Compute(Stats(active: 800, wired: 300, compressed: 100)); // 1200 頁 > 1000
        Assert.Equal(100d, result, precision: 9);
    }

    [Theory]
    [InlineData(0ul, 0ul, 0ul)]
    [InlineData(500ul, 300ul, 100ul)]
    [InlineData(2000ul, 0ul, 0ul)]
    public void Result_AlwaysWithinZeroToHundred(ulong active, ulong wired, ulong compressed)
    {
        var result = MacMemoryUsage.Compute(Stats(active, wired, compressed));
        Assert.InRange(result, 0d, 100d);
    }
}
