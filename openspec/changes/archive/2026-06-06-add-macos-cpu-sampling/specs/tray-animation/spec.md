## MODIFIED Requirements

### Requirement: 跨平台執行支援

應用程式 SHALL 能以單一程式碼基底發佈並執行於 Windows x64 與 macOS arm64 (Apple Silicon) 兩個目標平台,且兩個平台上 TrayIcon 動畫行為一致。CPU 取樣來源 SHALL 依平台切換:Windows 使用 LibreHardwareMonitor、macOS 使用以 mach `host_statistics(HOST_CPU_LOAD_INFO)` 差分計算的真實取樣來源,兩者皆讀取整機 CPU 總使用率;任一平台真實來源建立失敗時退回後援模擬器。動態變速的播放機制在兩平台上 SHALL 一致。

#### Scenario: Windows x64 執行
- **WHEN** 以 `win-x64` 發佈並於 Windows 11 x64 執行
- **THEN** 系統匣顯示 TrayIcon 且動畫依真實 CPU 取樣經 EMA 平滑後的速率切換 frame

#### Scenario: macOS Apple Silicon 執行
- **WHEN** 以 `osx-arm64` 發佈並於 macOS Apple Silicon 執行
- **THEN** menu bar 顯示 TrayIcon 且動畫依 mach 差分取得的真實 CPU 取樣經相同的 EMA 平滑與變速機制切換 frame
- **AND** 不嘗試載入 Windows 專屬的 LibreHardwareMonitor
