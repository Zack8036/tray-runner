## 1. Spike:驗證執行期間隔行為

- [x] 1.1 確認在運行中的 Avalonia 計時器上指派 `DispatcherTimer.Interval` 是立即生效、下一拍才生效、還是需要 Stop/Start
- [x] 1.2 依 Spike 結果決定 `SetInterval` 的策略(直接指派 vs. 以 Stop/Start 並保留影格索引)

## 2. AnimationSpeedController(純映射)

- [x] 2.1 新增 `AnimationSpeedController`,以 `CalculateInterval(double cpu) → TimeSpan` 透過 `Math.Pow` 實作 `interval = 300 · (1/15)^(cpu/100)`
- [x] 2.2 映射前以 `Math.Clamp(cpu, 0, 100)` 鉗制輸入
- [x] 2.3 新增單元測試:端點(0→300ms、100→20ms)、中點(50→約 77.5ms)、鉗制(140→20ms、-10→300ms)

## 3. 可於執行期更新的動畫迴圈

- [x] 3.1 在 `TrayAnimationLoop` 新增 `SetInterval(TimeSpan)` 路徑,依任務 1.2 的策略更新運行中的計時器
- [x] 3.2 確保間隔變化時保留 `_frameIndex`(不重置回第 0 格)

## 4. CPU 負載模擬器

- [x] 4.1 以第二個 `DispatcherTimer { Interval = 3s }` 新增模擬器
- [x] 4.2 每次觸發時:產生 `Random.Shared.NextDouble() * 100`,經控制器運算,再透過 `SetInterval` 套用
- [x] 4.3 新增可選的除錯記錄(`"CPU 73.2% → 41ms"`)以供視覺對照
- [x] 4.4 將模擬器接入應用程式啟動流程(`App.axaml.cs` / `Program.cs`),使其與動畫迴圈一同運行

## 5. 驗證

- [x] 5.1 執行應用程式,以肉眼確認系統匣動畫每 3 秒隨記錄的 CPU 值同步加速/減速
- [x] 5.2 確認 `dotnet build` 與測試皆通過
