## Why

系統匣動畫目前以固定的 100ms 影格間隔播放,完全無法反映機器狀態。若將動畫速度與 CPU 負載連動,系統匣圖示就會變成一個可以一眼掃過的即時負載指示器——機器越忙,圖示轉得越快。如此一來,這個常駐在系統匣的存在就從裝飾變成了有用的資訊。

## What Changes

- 新增 `AnimationSpeedController`,使用**指數(對數線性)曲線**將 CPU 使用率(0–100)映射到 `DispatcherTimer` 的間隔:`interval = 300 · (1/15)^(cpu/100)`,在 0% CPU 時為 300ms、100% CPU 時縮短為 20ms。
- 此控制器是一個**純計算函式**(`CalculateInterval(double cpu) → TimeSpan`),輸入會先鉗制(clamp)到 `[0, 100]`;它不持有也不修改任何計時器。
- 擴充 `TrayAnimationLoop`,讓影格間隔可在執行期動態更新(對運行中的 `DispatcherTimer` 套用新間隔)。
- 新增一個 **CPU 負載模擬器**,每 3 秒產生一個 0–100 的隨機值,驅動「控制器 → 動畫迴圈」的流程,以便在真實 CPU 取樣機制出現之前,先用肉眼驗證動態變速效果。

## Capabilities

### New Capabilities
- `animation-speed-control`: 透過指數曲線將 CPU 使用率映射到動畫影格間隔,包含輸入鉗制與執行期間隔更新;並附帶一個模擬器負載來源以供視覺驗證。

### Modified Capabilities
<!-- 現有規格中並無在需求層級描述「固定間隔動畫」的條目,因此沒有對應的 delta spec。執行期間隔更新的支援已涵蓋在上述新增能力中。 -->

## Impact

- **程式碼**: `src/TrayRunner.Tray/`——新增 `AnimationSpeedController.cs`;`TrayAnimationLoop.cs` 增加執行期間隔更新的路徑;新增模擬器接線(可能位於 `App.axaml.cs` / `Program.cs`)。
- **執行期行為**: 影格間隔從固定的 100ms 變成動態。
- **Spike / 未知數**: 對運行中的 Avalonia 計時器更新 `DispatcherTimer.Interval` 的行為(立即生效、下一拍才生效、或需要 Stop/Start)必須在依賴它之前先行驗證。
- **相依套件**: 本次變更不引入任何新相依。真實 CPU 取樣明確排除於範圍之外(僅模擬器)。
