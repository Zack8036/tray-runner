## MODIFIED Requirements

### Requirement: 跨平台執行支援

應用程式 SHALL 能以單一程式碼基底發佈並執行於 Windows x64 與 macOS arm64 (Apple Silicon) 兩個目標平台,且兩個平台上 TrayIcon 動畫行為一致。CPU 取樣來源 SHALL 依平台切換:Windows 使用 LibreHardwareMonitor 讀取真實整機 CPU 總使用率;macOS 本次使用後援模擬器作為取樣來源(真實 macOS 取樣留待後續 change)。動態變速的播放機制在兩平台上 SHALL 一致。

#### Scenario: Windows x64 執行
- **WHEN** 以 `win-x64` 發佈並於 Windows 11 x64 執行
- **THEN** 系統匣顯示 TrayIcon 且動畫依真實 CPU 取樣經 EMA 平滑後的速率切換 frame

#### Scenario: macOS Apple Silicon 執行
- **WHEN** 以 `osx-arm64` 發佈並於 macOS Apple Silicon 執行
- **THEN** menu bar 顯示 TrayIcon 且動畫以後援模擬器來源驅動,依相同的變速與平滑機制切換 frame
- **AND** 不嘗試載入 Windows 專屬的 LibreHardwareMonitor
