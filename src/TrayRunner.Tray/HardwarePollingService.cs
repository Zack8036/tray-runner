namespace TrayRunner.Tray;

/// <summary>
/// 在一條獨立的背景執行緒(非 UI 執行緒、非 thread pool 借用)以固定週期(預設 1 秒)
/// 向 <see cref="IUsageSource"/> 取樣使用率(CPU 或記憶體),經 <see cref="UsageSmoother"/>
/// 做 EMA 平滑後,透過 <see cref="Sampled"/> 事件對外發布。
///
/// 本服務刻意維持「指標中性」與「UI 無關」:它不認識自己取樣的是 CPU 還是記憶體,
/// 事件在背景執行緒上觸發、不認識 Avalonia Dispatcher,跨執行緒封送由訂閱端(App 層)負責。
/// App 對 CPU 與記憶體各建立一個獨立實例,兩者來源彼此獨立,任一來源失敗不影響另一者。
///
/// 取樣來源具執行緒親和性考量(如 LibreHardwareMonitor),故來源的取樣與
/// <see cref="IDisposable.Dispose"/> 一律在同一條背景緒上進行。
/// </summary>
public sealed class HardwarePollingService : IDisposable
{
    private readonly Func<IUsageSource> _sourceFactory;
    private readonly TimeSpan _interval;
    private readonly UsageSmoother _smoother;
    private readonly Action<string>? _log;
    private readonly string _threadName;
    private readonly CancellationTokenSource _cts = new();

    private Thread? _thread;

    /// <summary>每次取樣並平滑後觸發,參數為平滑後的使用率。於背景執行緒上觸發。</summary>
    public event Action<double>? Sampled;

    /// <param name="sourceFactory">
    /// 取樣來源工廠。來源於背景輪詢執行緒上才被建立,使其建立、取樣與釋放
    /// 全程在同一條緒上進行(尊重如 LibreHardwareMonitor 的執行緒親和性)。
    /// </param>
    /// <param name="threadName">背景緒名稱;CPU 與記憶體各跑一個實例時用以區分,便於除錯。</param>
    public HardwarePollingService(
        Func<IUsageSource> sourceFactory,
        TimeSpan? interval = null,
        double alpha = 0.3d,
        Action<string>? log = null,
        string threadName = "HardwarePolling")
    {
        _sourceFactory = sourceFactory;
        _interval = interval ?? TimeSpan.FromSeconds(1);
        _smoother = new UsageSmoother(alpha);
        _log = log;
        _threadName = threadName;
    }

    public void Start()
    {
        if (_thread is not null)
            return;

        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = _threadName,
        };
        _thread.Start();
    }

    private void PollLoop()
    {
        var token = _cts.Token;

        IUsageSource source;
        try
        {
            source = _sourceFactory();
        }
        catch (Exception ex)
        {
            // 來源建立失敗(例如防毒擋掉 LibreHardwareMonitor 驅動):
            // 記錄並結束輪詢,讓動畫維持預設速度,而非讓背景緒未處理例外導致程序崩潰。
            _log?.Invoke($"建立取樣來源失敗,停止輪詢({_threadName}):{ex.Message}");
            return;
        }

        try
        {
            do
            {
                double raw;
                try
                {
                    raw = source.ReadUsage();
                }
                catch (Exception ex)
                {
                    // 取樣來源拋例外:略過本次,維持輪詢不崩潰。
                    _log?.Invoke($"取樣失敗({_threadName}):{ex.Message}");
                    continue;
                }

                // 無效讀數(null 以 NaN 表示):略過,沿用前一次已套用的值。
                if (double.IsNaN(raw))
                    continue;

                var smoothed = _smoother.Add(raw);
                _log?.Invoke($"{_threadName} {raw:F1}% -> EMA {smoothed:F1}%");

                // 訂閱端(含關機時拆除中的 Dispatcher)拋例外不應殺掉背景緒,
                // 否則會變成未處理例外導致程序崩潰而非優雅退出。
                try
                {
                    Sampled?.Invoke(smoothed);
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"取樣事件處理失敗({_threadName}):{ex.Message}");
                }
            }
            // 取樣後等待一個週期;若期間收到取消訊號則立即結束。
            while (!token.WaitHandle.WaitOne(_interval));
        }
        finally
        {
            // 在同一條背景緒上釋放來源資源,尊重執行緒親和性。
            source.Dispose();
        }
    }

    public void Stop()
    {
        if (_thread is null)
            return;

        _cts.Cancel();
        // 等待背景緒結束(含來源釋放);設上限避免關機卡住。
        _thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
