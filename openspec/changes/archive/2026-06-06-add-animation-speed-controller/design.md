## Context

這個系統匣應用程式採用 Avalonia(並非 WPF——不過 `Avalonia.Threading.DispatcherTimer` 與 WPF 的 API 相當一致),透過 `TrayAnimationLoop` 來播放系統匣圖示動畫,其內部持有一個寫死 100ms 間隔的 `DispatcherTimer`(`src/TrayRunner.Tray/TrayAnimationLoop.cs`)。我們希望動畫速度能反映 CPU 負載:負載越高、轉得越快。本次變更新增映射邏輯與一個模擬器負載來源;真實的 CPU 取樣則延後處理。

## Goals / Non-Goals

**Goals:**
- 一個純粹、可測試的函式,將 CPU 0–100 映射到影格間隔。
- 在整個 CPU 範圍內都有感知上平滑的速度變化。
- 在現有動畫迴圈上可於執行期更新間隔。
- 一個每 3 秒驅動整條流程的模擬器,以供視覺驗收。

**Non-Goals:**
- 真實 CPU 取樣(跨平台效能計數器)——留待後續的獨立變更。
- 可設定的端點 / 使用者設定 UI。
- 連續速度變化之間的平滑/緩動處理(目前接受階梯式跳變)。

## Decisions

### Decision: 採用指數(對數線性)映射,而非線性

使用 `interval = 300 · (1/15)^(cpu/100)`。

**理由:** 人類對速度的感知是對數的(韋伯—費希納定律)。肉眼看到的量是影格率(`fps = 1000/interval`),而它是我們所設定值的**倒數**。對間隔做線性內插,會把幾乎所有可感知的加速擠到 CPU 的最高 25% 區間,使 0–50% 區段在視覺上毫無變化。指數曲線讓 CPU 每 +10% 就把速度乘上固定的約 1.31 倍(1.31¹⁰ ≈ 15),在整個範圍內都提供回饋,同時仍精準命中端點(0 時 300ms、100 時 20ms)。

**考慮過的替代方案:**
- **對間隔做線性內插**(`300 − 2.8·cpu`):最簡單,但低/中 CPU 區段有死區。已否決。
- **對影格率做線性內插**(`fps = 3.33 + 46.67·cpu/100`):感知上不錯且容易解釋(「CPU 翻倍 → 速度快一倍」),但在兩端的平滑度略遜於指數。保留作為公式日後需要面向使用者時的備案。

### Decision: 控制器是純函式,不持有計時器

`AnimationSpeedController.CalculateInterval(double cpu) → TimeSpan`,並先套用 `Math.Clamp(cpu, 0, 100)`。它不持有計時器、也不保留狀態。

**理由:** 純映射極易做單元測試(端點、中點、鉗制),也讓模擬器與未來的真實取樣器共用完全相同的邏輯。計時器留在 `TrayAnimationLoop`——它是唯一接觸 Avalonia UI 狀態的元件。

### Decision: TrayAnimationLoop 對外提供執行期間隔更新

新增一個方法(例如 `SetInterval(TimeSpan)`),在運行中指派 `_timer.Interval`,並保留 `_frameIndex`。

**理由:** 速度變化時動畫不應重置。把指派動作留在迴圈內部,可將所有「在 UI 執行緒上修改計時器」的行為集中於同一個類別。

### Decision: 模擬器使用第二個 DispatcherTimer(3 秒)

另一個 `DispatcherTimer { Interval = 3s }` 在 UI 執行緒上觸發,呼叫 `Random.Shared.NextDouble() * 100`,經由控制器運算,再透過 `SetInterval` 套用結果。可選擇性記錄 `"CPU 73.2% → 41ms"` 以供肉眼對照。

**理由:** 在 UI 執行緒上運行表示本階段不需要任何跨執行緒封送。模擬器刻意設計成未來真實取樣器的輕量替身,並落在同一個接縫上。

## Risks / Trade-offs

- **~~在運行中的 Avalonia 計時器上更新 `DispatcherTimer.Interval` 可能不會立即生效~~**(已由 Spike 解決)→ Spike 結論:Avalonia 12.0.4 的 `Interval` setter 在 `IsEnabled` 時會重算 `DueTimeInMs = now + interval` 並呼叫 `RescheduleTimers()`,**立即生效**,且不重置計時。`_frameIndex` 是本類別自有欄位、計時器不觸碰,天然保留。**策略定案:`SetInterval` 直接指派,無需 Stop/Start。**
- **未來真實的 CPU 取樣會在背景執行緒運行**,但計時器位於 UI 執行緒 → 目前全部都在 UI 執行緒,故暫不處理;`SetInterval` 這個接縫正是未來用 `Dispatcher.UIThread.Post(...)` 包裹呼叫的位置。
- **每 3 秒一次的階梯式速度變化可能看起來突兀** → 對驗證而言可接受;緩動處理是明確的 Non-Goal。

## Open Questions

- 端點固定為 300ms / 20ms——目前確認採用;僅在視覺測試顯示「以該圖示影格數而言 20ms 太快、無法分辨個別影格」時才重新檢視。
