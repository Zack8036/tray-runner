## Context

現有架構中,`CpuLoadSimulator` 在 UI 執行緒以 `DispatcherTimer` 每 3 秒產生隨機 CPU 值,直接呼叫 `AnimationSpeedController.CalculateInterval` 並更新 `TrayAnimationLoop`。`CpuLoadSimulator` 內既有註解已標記此處為「未來換成真實取樣器的接縫」。

本次要把假數據換成真實整機 CPU 總使用率。關鍵約束:

- 既有 spec `tray-animation` 要求同一程式碼基底同時執行於 Windows x64 與 macOS arm64,行為一致。
- LibreHardwareMonitorLib 為 Windows 專屬(底層依賴 WinRing0 核心驅動)。
- 整機 CPU% 在 .NET 沒有純跨平台 API(Windows 走 `GetSystemTimes`/PerformanceCounter,macOS 需 P/Invoke `host_processor_info`)。
- `AnimationSpeedController` 為純函式,流暢度由 UI 執行緒的 `DispatcherTimer` 維持。

## Goals / Non-Goals

**Goals:**
- 在 Windows 上以真實整機 CPU 總使用率驅動動畫速度。
- 取樣在獨立背景執行緒進行,每 1 秒一次,且不影響前端動畫流暢度。
- 以平台無關的介面隔離取樣來源,讓 Windows / 後援 / 日後 macOS 實作可互換。
- 對 1Hz 取樣做 EMA 平滑,讓速度變化自然而非神經質跳動。
- 保留日後在 Windows 端擴充溫度、風扇等感測的空間。

**Non-Goals:**
- 不在本次實作真實 macOS CPU 取樣(以後援模擬器替代,留待後續 change)。
- 不顯示溫度 / 風扇 / 電壓等感測值(僅保留架構擴充空間)。
- 不更動 `AnimationSpeedController` 的映射曲線與 `TrayAnimationLoop` 的播放機制。
- 不讓 α 或取樣週期可由使用者設定(本次寫死)。

## Decisions

### 決策 1:以 `ICpuUsageSource` 介面隔離取樣來源

**選擇**:定義 `interface ICpuUsageSource { double ReadCpuUsage(); }`,Windows 用 `LhmCpuSource`、其他平台用 `CpuSimulator`,啟動時以 `OperatingSystem.IsWindows()` 選擇。

**理由**:整機 CPU% 本就無純跨平台 API,依平台切換的抽象是必需品而非為了配合 LHM 才加。這也讓既有模擬器降級為「其中一種實作」,自然成為非 Windows 後援,並為日後 macOS 真實取樣預留接點。

**替代方案**:直接在服務內 `#if WINDOWS` 分支——會把平台邏輯散落、難以單元測試,且模擬器無法重用為後援。

### 決策 2:LibreHardwareMonitor 作為 Windows 後端(而非 GetSystemTimes)

**選擇**:Windows 端採用 LibreHardwareMonitorLib。

**理由**:使用者規劃日後顯示 CPU 溫度等感測值,而那正是 LHM(核心驅動讀 MSR)存在的理由;`GetSystemTimes` 只能拿到 CPU%。既然要背這個相依,放在介面後面、可隨時替換,風險可控。

**替代方案**:`GetSystemTimes`(無驅動、無管理員權限、足跡輕),若未來不做感測會是更好的選擇——故保留在介面後面,日後若放棄感測可低成本切回。

### 決策 3:專屬背景 `Thread` 而非 thread pool / PeriodicTimer 續行

**選擇**:`HardwarePollingService` 自建一條 `Thread { IsBackground = true }`,內含取樣迴圈 + 取消旗標。

**理由**:符合「獨立背景執行緒」需求;且 LHM 的 `Computer.Open()` / `Update()` / `Close()` 有執行緒親和性考量,固定待在同一條緒最乾淨,避免每次取樣借用不同 thread pool 執行緒。

**替代方案**:`PeriodicTimer` + `Task` 較現代,但續行跑在 thread pool 上,不保證同一條緒,且本案不需要 async I/O。

### 決策 4:封送由 App 層負責,服務維持 UI 無關

**選擇**:`HardwarePollingService` 只 `raise CpuSampled(double)`;App 的處理器內 `Dispatcher.UIThread.Post(() => loop.SetInterval(AnimationSpeedController.CalculateInterval(cpu)))`。

**理由**:維持服務可測試、與 Avalonia 解耦,延續既有「接縫在 App 層」的設計。Post 進 UI 緒的工作僅一次換算 + 一次間隔設定,1Hz 下成本可忽略,流暢度不受威脅。

### 決策 5:EMA 平滑放在取樣與發布之間,α=0.3

**選擇**:獨立 `CpuUsageSmoother`,`smoothed = 0.3·新值 + 0.7·前值`,第一筆當種子;服務發布平滑後的值。

**理由**:1Hz 原始 CPU% 尖刺嚴重,直接驅動會讓角色抽搐。EMA 只需記住一個數字、一行算式,且為純邏輯易於單元測試。α=0.3 在 1s 週期下兼顧反應與平順;種子化避免開頭假性低負載。`AnimationSpeedController` 完全不受影響。

### 決策 6:LHM 相依條件式引入,僅 Windows

**選擇**:`LibreHardwareMonitorLib` 的 `PackageReference` 以條件限定 Windows,且僅在 `OperatingSystem.IsWindows()` 時實例化 `LhmCpuSource`。

**理由**:避免 macOS 建置 / 執行時牽連 Windows 專屬組件。具體條件式寫法(依 RID 或 TFM 條件)於實作時驗證。

## Risks / Trade-offs

- **LHM 載入核心驅動觸發防毒 / SmartScreen** → LibreHardwareMonitor 會在執行期解壓並載入簽章核心驅動(WinRing0)。緩解:於提案與 README 註明;若僅讀 CPU 負載未來可評估改用 `GetSystemTimes` 免驅動。
- **CPU 負載讀取是否需管理員權限未經實測** → 驅動主要供溫度 / 電壓等 MSR 讀取,CPU 負載**應**不需驅動 / 管理員權限,但須於使用者機器實測確認。緩解:實作後在目標機器以非管理員身分驗證取樣有效。
- **macOS 行為與 Windows 不完全一致(用後援隨機值)** → 本次刻意取捨,spec 已明確記載 macOS 以後援來源驅動。緩解:後續 change 補上 `host_processor_info` 真實取樣。
- **關機時背景執行緒卡住** → 緩解:取消旗標 + 有上限的 join 等待 + `Computer.Close()`,確保正常退出。
- **α=0.3 平滑感未經實機調校** → 緩解:先寫死,實機觀感不佳再調整;數值集中於單一常數易於修改。
