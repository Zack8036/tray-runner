# hardware-memory-sampling

## Purpose

定義記憶體使用率取樣的跨平台抽象與實作策略，包含平台切換邏輯、輪詢與平滑模式，以及與 CPU 取樣的解耦原則。

## Requirements

### Requirement: 依平台切換的記憶體取樣來源

系統 SHALL 定義一個中性的使用率取樣抽象 `IUsageSource`(`ReadUsage()` 回傳介於 0–100 的使用率),由 CPU 與記憶體共用。記憶體使用率即已用記憶體佔實體記憶體的比例。系統 SHALL 在 Windows 平台提供以 kernel32 `GlobalMemoryStatusEx`(透過 P/Invoke,取其 `dwMemoryLoad` 直接得到 0–100 的實體記憶體使用百分比)為後端的實作,在 macOS 平台提供以 mach API(透過 P/Invoke 呼叫系統內建 `libSystem`,如 `host_statistics64`/`HOST_VM_INFO64` 取頁面統計、`sysctl` 取 `hw.memsize`)換算記憶體使用率的實作,並在無真實來源或來源建立失敗時提供以隨機值產生的後援實作。實際採用哪個來源 SHALL 在啟動時依執行平台決定。記憶體取樣 MUST NOT 引入任何平台專屬的第三方套件相依(Windows 走 kernel32、macOS 走 libSystem,皆為作業系統內建);因此記憶體取樣 MUST NOT 依賴 `LHM_AVAILABLE` 條件編譯,與 CPU 的 LibreHardwareMonitor 來源完全脫鉤。

#### Scenario: Windows 採用 GlobalMemoryStatusEx 來源
- **WHEN** 應用程式於 Windows 平台啟動
- **THEN** 記憶體取樣來源為以 kernel32 `GlobalMemoryStatusEx` 讀取 `dwMemoryLoad` 的實作
- **AND** 不引入 LibreHardwareMonitor(記憶體與 CPU 的 LHM 來源脫鉤)
- **AND** 回傳值介於 [0, 100]

#### Scenario: macOS 採用 mach 取樣來源
- **WHEN** 應用程式於 macOS 平台啟動
- **THEN** 記憶體取樣來源為以 mach / sysctl 讀取記憶體使用率的實作
- **AND** 不引入 LibreHardwareMonitor 等 Windows 專屬套件
- **AND** 取得有效讀數時回傳值介於 [0, 100]

#### Scenario: 來源建立失敗退回後援
- **WHEN** 平台對應的真實取樣來源建立失敗(如 macOS 上 mach 呼叫不可用、或 Windows 上 LibreHardwareMonitor 初始化失敗)
- **THEN** 記憶體取樣來源退回後援的隨機值實作,面板仍可顯示數值
- **AND** 回傳值介於 [0, 100]

### Requirement: 沿用既有輪詢與平滑模式發布記憶體讀數

系統 SHALL 沿用既有 `HardwarePollingService` 的「獨立背景緒週期性取樣 → EMA 指數移動平均平滑 → 透過事件發布」模式發布記憶體使用率,平滑係數與取樣週期 SHALL 與 CPU 取樣一致(α = 0.3、約每 1 秒)。記憶體取樣 SHALL 與 CPU 取樣以**各自獨立的輪詢服務實例**運作(來源彼此獨立,故任一來源失敗不影響另一者),`HardwarePollingService` SHALL 一般化為對中性 `IUsageSource` 取樣、以中性的 `Sampled` 事件發布,使 CPU 與記憶體共用同一服務型別。記憶體取樣的發布 MUST NOT 直接存取 UI 物件,亦 MUST NOT 認識 Avalonia Dispatcher,以維持與前端解耦且可被獨立測試。

#### Scenario: 每秒取樣並發布平滑後讀數
- **WHEN** 輪詢服務啟動後持續運行
- **THEN** 約每 1 秒取得一次記憶體使用率
- **AND** 套用 α = 0.3 的 EMA 平滑後透過事件對外發布

#### Scenario: 取樣與前端解耦
- **WHEN** 記憶體取樣於背景緒進行
- **THEN** 取樣與來源讀取不存取 UI 物件、不認識 Dispatcher
- **AND** 換算核心邏輯可獨立於執行平台被單元測試

#### Scenario: 記憶體取樣不牽連 LHM 條件編譯
- **WHEN** 於任一平台建置(含 `osx-arm64` 發佈)
- **THEN** 記憶體取樣來源不依賴 `LHM_AVAILABLE` 條件編譯即可參與編譯
- **AND** Windows 由 kernel32、macOS 由 libSystem 提供,皆為作業系統內建
