## Why

目前動畫速度由 `CpuLoadSimulator` 每 3 秒丟出的隨機值驅動,只能用來視覺驗證變速機制,並非真正反映系統忙碌程度。變速效果已驗證完美,現在要把假數據換成真實的整機 CPU 總使用率,讓系統匣角色的奔跑速度真正對應到電腦的負載。

## What Changes

- 新增 `ICpuUsageSource` 取樣抽象介面(回傳 `double` CPU%),作為依平台切換實作的接縫。整機 CPU% 在 .NET 沒有純跨平台 API,此介面為必需品。
- 新增 Windows 實作 `LhmCpuSource`,引入 **LibreHardwareMonitorLib**(條件式相依,僅 Windows)讀取整機 CPU 總使用率;架構保留日後擴充溫度、風扇等感測的空間。
- 既有 `CpuLoadSimulator` 改寫為 `ICpuUsageSource` 的後援實作(`CpuSimulator`),在非 Windows 平台或無真實來源時提供隨機值。macOS 本次以此後援運作,真實 macOS 取樣留待後續 change。
- 新增 `HardwarePollingService`:在**專屬背景執行緒**(非 threadpool)每 1 秒取樣一次,透過事件 `CpuSampled(double)` 拋出數值。服務本身 UI 無關,不認識 Dispatcher。
- 新增 `CpuUsageSmoother`:對 1Hz 原始 CPU% 做 **EMA 指數移動平均**(α=0.3,第一筆取樣當種子),吸收尖刺讓速度變化平順。
- App 層負責跨執行緒封送:在事件處理器內以 `Dispatcher.UIThread.Post` 套用速度,確保背景輪詢不影響前端動畫流暢度。`AnimationSpeedController` 維持純函式不變。
- **BREAKING**(對既有 spec 行為):取樣週期由 3 秒改為 1 秒;模擬器由「主要驅動來源」降級為「後援來源」。

## Capabilities

### New Capabilities
- `hardware-cpu-sampling`: 在獨立背景執行緒以固定週期採集整機 CPU 總使用率,依平台切換取樣來源(Windows 用 LibreHardwareMonitor、其他平台用後援模擬器),經 EMA 平滑後透過事件對外發布,並將數值封送至 UI 執行緒。

### Modified Capabilities
- `animation-speed-control`: 速度驅動來源由「每 3 秒隨機模擬器」改為「每 1 秒真實 CPU 取樣經 EMA 平滑後封送至 UI」;模擬器降級為無真實來源時的後援。
- `tray-animation`: 跨平台一致性 scenario 中,macOS 改以後援來源驅動動畫(本次不提供真實 macOS 取樣)。

## Impact

- **新增程式碼**:`ICpuUsageSource`、`LhmCpuSource`、`CpuSimulator`(由 `CpuLoadSimulator` 改寫)、`HardwarePollingService`、`CpuUsageSmoother`。
- **修改程式碼**:`App.axaml.cs`(改以背景服務 + 封送串接,取代直接驅動)。`AnimationSpeedController`、`TrayAnimationLoop` 不變。
- **相依**:新增 `LibreHardwareMonitorLib` NuGet 套件(條件式,僅 Windows)。
- **風險**:LibreHardwareMonitor 會在執行期解壓並載入簽章核心驅動(WinRing0),可能觸發防毒 / SmartScreen;CPU 負載讀取**應**不需驅動 / 系統管理員權限(驅動主要供溫度、電壓等 MSR 讀取),但須於使用者機器實測確認。
- **測試**:`CpuUsageSmoother`(EMA 純邏輯)可單元測試;`HardwarePollingService` 可注入假 `ICpuUsageSource` 驗證取樣週期與事件發布。
