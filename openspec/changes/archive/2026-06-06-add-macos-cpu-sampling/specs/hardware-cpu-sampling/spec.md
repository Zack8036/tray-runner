## MODIFIED Requirements

### Requirement: 依平台切換的 CPU 取樣來源

系統 SHALL 定義一個取樣抽象 `ICpuUsageSource`,其回傳介於 0–100 的整機 CPU 總使用率(百分比)。系統 SHALL 在 Windows 平台提供以 LibreHardwareMonitor 為後端的實作,在 macOS 平台提供以 mach API(`host_statistics` 取 `HOST_CPU_LOAD_INFO`,前後兩次 tick 快照差分)讀取整機 CPU 總使用率的實作,並在無真實來源或來源建立失敗時提供以隨機值產生的後援實作。實際採用哪個來源 SHALL 在啟動時依執行平台決定;macOS 來源 SHALL 透過 P/Invoke 呼叫系統內建 `libSystem`,MUST NOT 引入任何平台專屬的第三方套件相依。

#### Scenario: Windows 採用 LibreHardwareMonitor 來源
- **WHEN** 應用程式於 Windows 平台啟動
- **THEN** 取樣來源為以 LibreHardwareMonitor 讀取整機 CPU 總使用率的實作
- **AND** 回傳值介於 [0, 100]

#### Scenario: macOS 採用 mach 差分取樣來源
- **WHEN** 應用程式於 macOS 平台啟動
- **THEN** 取樣來源為以 mach `host_statistics(HOST_CPU_LOAD_INFO)` 前後快照差分計算整機 CPU 總使用率的實作
- **AND** 不引入 LibreHardwareMonitor 等 Windows 專屬套件
- **AND** 取得有效讀數時回傳值介於 [0, 100]

#### Scenario: 來源建立失敗退回後援
- **WHEN** 平台對應的真實取樣來源建立失敗(如 macOS 上 mach 呼叫不可用、或 Windows 上 LibreHardwareMonitor 初始化失敗)
- **THEN** 取樣來源退回後援的隨機值實作,動態變速仍可運作
- **AND** 回傳值介於 [0, 100]

## ADDED Requirements

### Requirement: 差分式取樣的首樣以 NaN 表示無前值

對於需要前後兩次累計計數差分才能算出使用率的取樣來源(如 macOS 的 mach tick 來源),系統 SHALL 在尚無前一次快照(即第一次取樣)、或兩次快照的總 tick 差為 0、或底層讀取失敗時回傳 `double.NaN`,交由背景輪詢服務既有的無效值處理略過該次取樣。差分換算的核心邏輯 SHALL 與 P/Invoke 殼層分離為純函式,使其可獨立於執行平台被單元測試。

#### Scenario: 第一次取樣無前值回傳 NaN
- **WHEN** 差分式取樣來源被第一次呼叫且尚無前一次快照
- **THEN** 回傳 `double.NaN`
- **AND** 背景輪詢服務略過該次取樣,於下一週期取得第二筆快照後產生有效讀數

#### Scenario: 總 tick 差為 0 回傳 NaN
- **WHEN** 前後兩次快照的總 tick(user+system+idle+nice)差為 0
- **THEN** 差分換算回傳 `double.NaN` 而非除以零

#### Scenario: 一般情況依差分公式換算
- **WHEN** 取得相隔一個週期的兩筆有效快照
- **THEN** 使用率為 `Δ(user+system+nice) / Δ(user+system+idle+nice) × 100`
- **AND** 結果介於 [0, 100]
