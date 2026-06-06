# tray-animation

### Requirement: 啟動為純狀態列常駐程式

應用程式 SHALL 在啟動時不顯示任何主視窗，只在系統匣 (Windows) 或 menu bar (macOS) 顯示一個 TrayIcon，且在 TrayIcon 顯示期間 process MUST 持續運作不自動退出。

#### Scenario: 啟動後沒有主視窗
- **WHEN** 使用者啟動應用程式
- **THEN** 桌面上不會出現任何應用程式視窗，且系統匣 / menu bar 出現對應的 TrayIcon

#### Scenario: 沒有開啟視窗時程式仍持續運作
- **WHEN** 應用程式啟動完成且沒有任何 Window 物件存在
- **THEN** 應用程式 process 仍持續運作，TrayIcon 仍可被使用者互動

### Requirement: 圖框資源池預先載入

應用程式 SHALL 在啟動階段一次將所有動畫 frame 圖片載入並轉換為 `WindowIcon` 物件，集中保存於一個唯讀集合中；動畫迴圈執行期間 MUST NOT 重新從檔案載入或重新建立 `WindowIcon` 物件。

#### Scenario: 啟動時完成所有 frame 載入
- **WHEN** 應用程式完成初始化
- **THEN** 資源池中包含與 frame 圖片數量相同（本次為 4 張）的 `WindowIcon` 物件，且每個物件對應到正確的 frame 順序

#### Scenario: 動畫期間重複使用同一組物件
- **WHEN** 動畫迴圈執行任意長時間
- **THEN** `TrayIcon.Icon` 只會被指派為資源池中既有的 `WindowIcon` 物件，不會產生新的 `WindowIcon` 實例

### Requirement: 可變速率動畫迴圈

應用程式 SHALL 透過 `DispatcherTimer` 在 UI thread 上以目前設定的間隔切換 `TrayIcon.Icon`，依資源池順序循環播放，使狀態列圖示呈現連續動畫。間隔的初始值為 100 毫秒，且 SHALL 可於執行期更新（由速度控制器決定，參見 `animation-speed-control` 能力）；更新間隔時 MUST 保留目前的 frame 索引，不重置回第一張。

#### Scenario: 依序循環播放 frame
- **WHEN** 動畫迴圈持續執行
- **THEN** `TrayIcon.Icon` 會按照資源池中的順序循環指派，每經過目前間隔切換到下一張，到達最後一張後回到第一張

#### Scenario: 執行期更新間隔不重置動畫
- **WHEN** 動畫迴圈執行中且間隔被更新為新值
- **THEN** 後續切換改以新間隔進行，且目前的 frame 索引被保留，動畫不會跳回第一張

#### Scenario: 切換動作於 UI thread 執行
- **WHEN** Timer tick 觸發
- **THEN** 對 `TrayIcon.Icon` 的指派發生在 Avalonia UI thread 上，不會跨執行緒存取 UI 物件

### Requirement: 透過 TrayIcon 選單退出

TrayIcon SHALL 提供一個包含 "Quit" 項目的右鍵選單，使用者點選後 MUST 觸發應用程式正常結束，並在結束流程中釋放資源池內的 `WindowIcon` 物件。

#### Scenario: 點選 Quit 結束應用程式
- **WHEN** 使用者在 TrayIcon 上開啟右鍵選單並點選 "Quit"
- **THEN** 應用程式停止動畫 Timer、釋放資源池內的圖示資源，process 正常退出

### Requirement: 跨平台執行支援

應用程式 SHALL 能以單一程式碼基底發佈並執行於 Windows x64 與 macOS arm64 (Apple Silicon) 兩個目標平台,且兩個平台上 TrayIcon 動畫行為一致。CPU 取樣來源 SHALL 依平台切換:Windows 使用 LibreHardwareMonitor、macOS 使用以 mach `host_statistics(HOST_CPU_LOAD_INFO)` 差分計算的真實取樣來源,兩者皆讀取整機 CPU 總使用率;任一平台真實來源建立失敗時退回後援模擬器。動態變速的播放機制在兩平台上 SHALL 一致。

#### Scenario: Windows x64 執行
- **WHEN** 以 `win-x64` 發佈並於 Windows 11 x64 執行
- **THEN** 系統匣顯示 TrayIcon 且動畫依真實 CPU 取樣經 EMA 平滑後的速率切換 frame

#### Scenario: macOS Apple Silicon 執行
- **WHEN** 以 `osx-arm64` 發佈並於 macOS Apple Silicon 執行
- **THEN** menu bar 顯示 TrayIcon 且動畫依 mach 差分取得的真實 CPU 取樣經相同的 EMA 平滑與變速機制切換 frame
- **AND** 不嘗試載入 Windows 專屬的 LibreHardwareMonitor
