# hardware-cpu-sampling

### Requirement: 依平台切換的 CPU 取樣來源

系統 SHALL 定義一個取樣抽象 `ICpuUsageSource`,其回傳介於 0–100 的整機 CPU 總使用率(百分比)。系統 SHALL 在 Windows 平台提供以 LibreHardwareMonitor 為後端的實作,並在非 Windows 平台或無真實來源時提供以隨機值產生的後援實作。實際採用哪個來源 SHALL 在啟動時依執行平台決定。

#### Scenario: Windows 採用 LibreHardwareMonitor 來源
- **WHEN** 應用程式於 Windows 平台啟動
- **THEN** 取樣來源為以 LibreHardwareMonitor 讀取整機 CPU 總使用率的實作
- **AND** 回傳值介於 [0, 100]

#### Scenario: 非 Windows 平台採用後援來源
- **WHEN** 應用程式於非 Windows 平台(如 macOS)啟動
- **THEN** 取樣來源為後援的隨機值實作,且不嘗試載入 LibreHardwareMonitor
- **AND** 回傳值介於 [0, 100]

### Requirement: 背景執行緒週期性取樣並發布

系統 SHALL 提供 `HardwarePollingService`,於一條獨立的背景執行緒(非 UI 執行緒、非 thread pool 借用)每 1 秒呼叫取樣來源一次,並透過事件對外發布取得的 CPU 使用率。此服務 MUST NOT 直接存取 UI 物件,亦 MUST NOT 認識 Avalonia Dispatcher,以維持與前端解耦且可被獨立測試。

#### Scenario: 每秒取樣並觸發事件
- **WHEN** 輪詢服務啟動後持續運行
- **THEN** 每隔約 1 秒呼叫取樣來源一次
- **AND** 每次取樣後觸發一個帶有該 CPU 使用率數值的事件

#### Scenario: 取樣不在 UI 執行緒進行
- **WHEN** 輪詢服務執行取樣
- **THEN** 取樣與來源的讀取動作發生在專屬背景執行緒上,不佔用 UI 執行緒

### Requirement: 取樣值經 EMA 指數移動平均平滑

系統 SHALL 在發布取樣值之前,對原始 CPU 使用率套用指數移動平均(EMA)平滑:`smoothed = α · 新取樣 + (1 − α) · 前一次 smoothed`,其中平滑係數 α SHALL 為 0.3。第一筆取樣 SHALL 直接作為 EMA 的初始值(種子),使動畫不會在啟動時從假性低負載慢慢爬升。

#### Scenario: 第一筆取樣作為種子
- **WHEN** 服務取得第一筆原始 CPU 取樣值
- **THEN** 該值直接作為平滑後的輸出,不與任何先前值混合

#### Scenario: 後續取樣依 EMA 公式平滑
- **WHEN** 服務取得第二筆以後的原始取樣值
- **THEN** 平滑後的值為 `0.3 · 新取樣 + 0.7 · 前一次平滑值`
- **AND** 對外發布的是平滑後的值,而非原始取樣值

### Requirement: 取樣值封送至 UI 執行緒後套用速度

系統 SHALL 在背景取樣事件與動畫迴圈之間進行跨執行緒封送:事件處理 SHALL 透過 Avalonia Dispatcher 將套用速度的動作排程到 UI 執行緒,於 UI 執行緒上呼叫速度控制器換算間隔並更新動畫迴圈。封送到 UI 執行緒的工作 MUST 維持輕量(僅換算與設定間隔),以確保背景輪詢不影響前端動畫的流暢度。

#### Scenario: 速度更新發生在 UI 執行緒
- **WHEN** 背景取樣事件被觸發
- **THEN** 換算間隔與更新動畫迴圈的動作被排程並執行於 UI 執行緒上
- **AND** 不在 UI 執行緒上執行取樣或來源讀取等耗時工作

### Requirement: 取樣錯誤與關機的穩健處理

系統 SHALL 在取樣來源回傳無效值(null 或 NaN)時略過該次取樣或沿用前一次有效值,而非讓背景執行緒崩潰。應用程式關閉時,系統 SHALL 通知背景執行緒停止、等待其結束(join)、釋放取樣來源所持有的資源(如 LibreHardwareMonitor 的底層控制代碼),且關機流程 MUST NOT 因等待背景執行緒而卡住。

#### Scenario: 無效取樣值不中斷輪詢
- **WHEN** 取樣來源在某次取樣回傳 null 或 NaN
- **THEN** 該次取樣被略過或沿用前一次有效值
- **AND** 背景輪詢於下一週期繼續正常運作

#### Scenario: 關機時優雅停止背景執行緒
- **WHEN** 應用程式開始關閉流程
- **THEN** 背景執行緒收到停止通知並結束,取樣來源資源被釋放
- **AND** 應用程式正常退出,不因背景執行緒而卡住
