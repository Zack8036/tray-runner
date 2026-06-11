using System.Collections.Concurrent;
using TrayRunner.Tray;
using Xunit;

namespace TrayRunner.Tray.Tests;

public class HardwarePollingServiceTests
{
    /// <summary>
    /// 假取樣來源:依序回傳給定的值,耗盡後回傳 NaN(會被服務略過),
    /// 使輸出序列可預期。記錄是否被 Dispose。
    /// </summary>
    private sealed class FakeUsageSource : IUsageSource
    {
        private readonly ConcurrentQueue<double> _values;
        public bool Disposed { get; private set; }

        public FakeUsageSource(params double[] values)
            => _values = new ConcurrentQueue<double>(values);

        public double ReadUsage()
            => _values.TryDequeue(out var v) ? v : double.NaN;

        public void Dispose() => Disposed = true;
    }

    private static List<double> CollectSamples(
        HardwarePollingService service, int expectedCount, int timeoutMs = 2000)
    {
        var collected = new BlockingCollection<double>();
        service.Sampled += v => collected.Add(v);
        service.Start();

        var result = new List<double>();
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            while (result.Count < expectedCount)
                result.Add(collected.Take(cts.Token));
        }
        catch (OperationCanceledException)
        {
            // 逾時:回傳已收集到的,讓斷言呈現實際數量。
        }

        return result;
    }

    [Fact]
    public void PublishesSmoothedValues_NotRawSamples()
    {
        var source = new FakeUsageSource(0d, 100d, 100d);
        using var service = new HardwarePollingService(
            () => source,
            interval: TimeSpan.FromMilliseconds(10),
            alpha: 0.3d);

        var samples = CollectSamples(service, expectedCount: 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(0d, samples[0], precision: 9);   // 第一筆為種子
        Assert.Equal(30d, samples[1], precision: 9);  // 0.3·100 + 0.7·0
        Assert.Equal(51d, samples[2], precision: 9);  // 0.3·100 + 0.7·30
    }

    [Fact]
    public void SamplesPeriodically_RaisingMultipleEvents()
    {
        var source = new FakeUsageSource(50d, 50d, 50d, 50d, 50d);
        using var service = new HardwarePollingService(
            () => source,
            interval: TimeSpan.FromMilliseconds(10),
            alpha: 0.3d);

        var samples = CollectSamples(service, expectedCount: 5);

        Assert.Equal(5, samples.Count);
    }

    [Fact]
    public void SkipsNaNSamples_WithoutRaisingEvent()
    {
        var source = new FakeUsageSource(double.NaN, 50d);
        using var service = new HardwarePollingService(
            () => source,
            interval: TimeSpan.FromMilliseconds(10),
            alpha: 0.3d);

        var samples = CollectSamples(service, expectedCount: 1);

        // NaN 被略過,第一個發布的事件是 50(作為種子),而非 NaN。
        Assert.Single(samples);
        Assert.Equal(50d, samples[0], precision: 9);
    }

    [Fact]
    public void SourceFactoryThrows_DoesNotCrash_NoEventsRaised()
    {
        using var service = new HardwarePollingService(
            () => throw new InvalidOperationException("驅動被擋"),
            interval: TimeSpan.FromMilliseconds(10),
            alpha: 0.3d);

        var raised = 0;
        service.Sampled += _ => Interlocked.Increment(ref raised);

        // 工廠拋例外不應讓背景緒未處理例外而崩潰。
        service.Start();
        Thread.Sleep(100);

        Assert.Equal(0, Volatile.Read(ref raised));
        // 即使來源從未建立,Stop/Dispose 仍應安全。
        service.Stop();
    }

    [Fact]
    public void TwoIndependentInstances_PublishTheirOwnSourcesSeparately()
    {
        // App 對 CPU 與記憶體各跑一個獨立實例;此處驗證兩者來源彼此獨立、各自發布,
        // 不會互相串擾(一個來源耗盡回傳 NaN 被略過,不影響另一個)。
        var cpuSource = new FakeUsageSource(10d, 10d, 10d);
        var memSource = new FakeUsageSource(80d, 80d, 80d);

        using var cpu = new HardwarePollingService(
            () => cpuSource, interval: TimeSpan.FromMilliseconds(10), alpha: 0.3d, threadName: "CpuPolling");
        using var mem = new HardwarePollingService(
            () => memSource, interval: TimeSpan.FromMilliseconds(10), alpha: 0.3d, threadName: "MemoryPolling");

        var cpuSamples = new BlockingCollection<double>();
        var memSamples = new BlockingCollection<double>();
        cpu.Sampled += v => cpuSamples.Add(v);
        mem.Sampled += v => memSamples.Add(v);

        cpu.Start();
        mem.Start();

        // 每個服務的第一筆為種子,直接等於其來源值。
        using var cts = new CancellationTokenSource(2000);
        var firstCpu = cpuSamples.Take(cts.Token);
        var firstMem = memSamples.Take(cts.Token);

        Assert.Equal(10d, firstCpu, precision: 9);
        Assert.Equal(80d, firstMem, precision: 9);
    }

    [Fact]
    public void Stop_HaltsSampling_AndDisposesSource()
    {
        var source = new FakeUsageSource(Enumerable.Repeat(50d, 1000).ToArray());
        var service = new HardwarePollingService(
            () => source, interval: TimeSpan.FromMilliseconds(10), alpha: 0.3d);

        var seen = 0;
        service.Sampled += _ => Interlocked.Increment(ref seen);
        service.Start();

        // 等到確實開始取樣。
        var spin = SpinWait.SpinUntil(() => Volatile.Read(ref seen) > 0, 1000);
        Assert.True(spin, "服務未在時限內開始取樣");

        service.Stop();
        var countAtStop = Volatile.Read(ref seen);

        // 停止後再等一段時間,事件數不應再增加。
        Thread.Sleep(100);
        Assert.Equal(countAtStop, Volatile.Read(ref seen));
        Assert.True(source.Disposed, "停止後來源應已被釋放");

        service.Dispose();
    }
}
