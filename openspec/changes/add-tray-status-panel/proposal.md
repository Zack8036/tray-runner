## Why

目前 TrayRunner 是純托盤 app:只有一個會依 CPU 負載變速的 runner 動畫圖示,使用者無法看到任何實際數據。要把它從「動畫玩具」推進成「狀態工具」,需要一個能呈現即時系統指標的下拉面板,且兩大目標平台(Windows / macOS)都要能用。本變更先以最小可行範圍(CPU + 記憶體)打通整條「托盤 → 毛玻璃面板 → 即時指標」的路徑,作為後續擴充多指標的地基。

## What Changes

- 新增一個跨平台**毛玻璃下拉狀態面板**(Avalonia `Window`):無系統標題列/邊框、透明背景、OS 原生模糊材質、圓角 + 細邊框 + 微陰影,視覺上近似 macOS Control Center / Stats。
- 面板 MVP 顯示**兩張指標卡片:CPU 使用率、記憶體使用率**。
- 在現有 `TrayIcon` 的 `NativeMenu` 新增「顯示面板」選單項;Windows 與 macOS **統一**透過此選單項喚出面板(刻意不依賴 `TrayIcon.Clicked`,因該事件在 macOS 不觸發、Linux 各桌面環境不一致),使三平台喚出路徑為單一程式碼路徑。
- 面板採**單一視窗實例 + toggle**(顯示/隱藏同一實例),保留狀態、省資源,非每次重建。
- **新增記憶體取樣來源**,沿用既有 `ICpuUsageSource` / `HardwarePollingService` 的抽象與「背景緒輪詢 → EMA 平滑 → 封送 UI 緒」模式:Windows 用 LibreHardwareMonitor、macOS 用 mach;維持 `osx-arm64` 不牽連 LHM 的條件編譯策略(`LHM_AVAILABLE`)。
- 釐清誠實邊界:此面板為 **frosted acrylic 近似**,並非 Apple 真正的 Liquid Glass(折射 / 鏡面高光需自寫 shader),不在本範圍。

## Capabilities

### New Capabilities
- `tray-status-panel`: 托盤下拉狀態面板的喚出方式(NativeMenu 統一路徑)、視窗外觀(無邊框 / 透明 / 原生模糊 / 圓角玻璃)、單一實例 toggle 生命週期,以及面板上 CPU + 記憶體卡片的呈現契約。
- `hardware-memory-sampling`: 依平台切換的記憶體使用率取樣來源(Windows LibreHardwareMonitor、macOS mach、後援隨機值),並沿用既有背景輪詢 / EMA 平滑 / 封送 UI 緒的發布模式。

### Modified Capabilities
<!-- 既有 spec 的「需求」本身不改變:CPU 取樣與動畫變速行為維持原狀。記憶體以平行的新 capability 表達,面板為全新 capability。故此處留空。 -->

## Impact

- **新增程式碼**:面板 `Window`(XAML + code-behind)、記憶體取樣來源實作(LHM / mach / 後援)、面板與輪詢服務的接線。
- **既有程式碼**:`App.axaml`(`NativeMenu` 新增「顯示面板」項)、`App.axaml.cs`(建立 / toggle 面板、接收記憶體取樣);輪詢服務可能需一般化以同時發布記憶體讀數。
- **相依 / 建置**:沿用現有 Avalonia 12.0.4 / net10.0 與 `LHM_AVAILABLE` 條件編譯;不新增第三方套件(macOS 走 P/Invoke)。
- **out of scope(列為後續 change)**:
  1. macOS 原生 `NSStatusItem` + `NSPopover`(Stats 同款「點圖示即在圖示正下方彈出」,需繞過 Avalonia TrayIcon 走 AppKit P/Invoke);
  2. GPU / 網路 / 硬碟指標(尤其 macOS GPU 需 IOKit / Metal);
  3. 面板**精準貼齊托盤/選單列圖示**的定位(Avalonia 拿不到圖示螢幕座標)—— 註:近似定位(出現在狀態列所在的螢幕角落)與拖曳移動已納入本變更;

  4. 點面板外面自動關閉(`Deactivated`)的細部行為。
