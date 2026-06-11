## Context

TrayRunner 目前是純托盤 app:`App.axaml` 只宣告一個 `TrayIcon`(含 `NativeMenu` 的 Quit 項),`App.axaml.cs` 在 `OnFrameworkInitializationCompleted` 啟動 `TrayAnimationLoop` 與 `HardwarePollingService`,並把 `ShutdownMode` 設為 `OnExplicitShutdown`。整個程式沒有任何 `Window`。

既有資料層已具備乾淨抽象:`ICpuUsageSource`(回傳 0–100 的 CPU 使用率)+ `HardwarePollingService`(獨立背景緒每秒取樣 → EMA α=0.3 平滑 → 事件發布),平台分歧由 `App.CreateCpuSource()` 工廠處理(Windows LHM、macOS mach、後援 `CpuSimulator`),並以 `LHM_AVAILABLE` 條件編譯讓 `osx-arm64` 發佈完全不牽連 LibreHardwareMonitor。

本變更要在不破壞此架構的前提下,長出第一個視窗(毛玻璃面板)與第二個指標(記憶體)。兩個目標平台為 Windows(`win-x64`)與 macOS(`osx-arm64`),技術棧 Avalonia 12.0.4 / net10.0。

## Goals / Non-Goals

**Goals:**
- 打通「托盤 → 毛玻璃面板 → 即時 CPU + 記憶體」整條路徑,作為多指標擴充的地基。
- 喚出面板的程式碼為**單一路徑**,三平台行為一致,不踩 `TrayIcon.Clicked` 的跨平台地雷。
- 記憶體取樣**完全沿用** CPU 既有的抽象 / 輪詢 / 平滑 / 封送模式與條件編譯策略,降低認知與維護成本。
- 毛玻璃以 OS 原生模糊打底,XAML 設定為主、最少自寫程式。

**Non-Goals:**
- macOS 原生 `NSStatusItem` + `NSPopover`(Stats 同款「點圖示即在圖示正下方彈出」)——需繞過 Avalonia TrayIcon 走 AppKit P/Invoke,列為後續 change。
- GPU / 網路 / 硬碟指標(macOS GPU 需 IOKit/Metal)。
- 面板精準貼齊托盤圖示的螢幕定位(Avalonia 無法取得托盤圖示座標)。
- 點面板外面自動關閉(`Deactivated`)、開啟動畫等體驗細修。
- 真正的 Apple Liquid Glass(折射 / 鏡面高光,需自寫 shader)。

## Decisions

### D1：喚出方式走「NativeMenu 統一」而非 `TrayIcon.Clicked`
驗證結果:`TrayIcon.Clicked` 僅 Win32 與部分 Linux DE 觸發,**macOS 完全不觸發且無官方解法**。若 Windows 走 `Clicked`、macOS 走 NativeMenu,會變成兩條路徑兩種行為。
**選擇**:兩平台都在 `NativeMenu` 加「顯示面板」項,點擊 → toggle 同一視窗。喚出邏輯單一、三平台齊,連 Linux 的 DE 差異一併繞過。
**取捨**:macOS 少了「一點即現」的原生爽感(多一次選單點擊),換取一份程式碼與一致行為;原生體驗留待後續 NSStatusItem change。

### D2：毛玻璃採 OS 原生模糊(`TransparencyLevelHint`)而非 `ExperimentalAcrylicBorder` 自繪
**選擇**:`Window` 設 `SystemDecorations="None"`、`Background="Transparent"`、`TransparencyLevelHint="AcrylicBlur, Blur"`(有序降級),外層再包一個 `CornerRadius=12` + 細邊框 + `BoxShadow` + 半透明 tint 的 `Border`。
**理由**:質感好、平台真模糊、XAML 設定為主、程式碼最少,符合 MVP。
**取捨**:各平台材質不完全一致(Mica 為 Win11 限定,故不放入 hint);若日後要「到處長一樣」,再評估改用 `ExperimentalAcrylicBorder` 自繪。**誠實邊界**:這是 frosted acrylic 近似,非 Apple Liquid Glass。

### D3：單一視窗實例 + toggle,關閉=隱藏
**選擇**:應用程式持有單一面板 `Window` 實例;喚出時若隱藏則 `Show()`、若顯示則 `Hide()`;面板關閉攔截為隱藏,不銷毀、不影響 `OnExplicitShutdown`。
**理由**:省資源、保留狀態、與既有「托盤常駐」生命週期相容。
**取捨**:視窗物件常駐記憶體(極小),換取喚出即時與狀態保留。

### D4:記憶體取樣鏡像 CPU 的抽象,但與 LHM 脫鉤、跑獨立實例(已定版)
**關鍵洞察**:決定 A(兩實例)vs B(一條緒雙來源)的真正約束不是執行緒數(兩條 1Hz 睡眠緒成本可忽略),而是 Windows LHM `Computer` 的執行緒親和性。若 Windows 記憶體也讀同一個 LHM `Computer`,親和性會逼成 B 並得重寫已驗證的 `PollLoop`(動畫變速的回歸風險)。
**化解**:Windows 記憶體**不走 LHM**,改用 kernel32 `GlobalMemoryStatusEx` 的 `dwMemoryLoad`(直接 0–100)。記憶體與 CPU 因此完全獨立 →
**選擇 Option A**:把 `ICpuUsageSource` 一般化為中性 `IUsageSource`(`ReadUsage()`)、事件改名 `Sampled`、執行緒名可參數化;App 跑**兩個獨立 `HardwarePollingService` 實例**(CPU、記憶體)。既有 CPU 迴圈邏輯一字不改(僅型別/命名),失敗隔離免費,既有測試機械式更名。記憶體取樣不需 `LHM_AVAILABLE`(Windows kernel32 / macOS libSystem 皆 OS 內建)。
**取捨**:多一條睡眠背景緒(可忽略),換取零回歸風險與更簡單的程式碼。

### D5：UI 更新一律封送 UI 緒
沿用既有 `Dispatcher.UIThread.Post` 模式:背景緒取樣事件 → 封送 → 於 UI 緒更新卡片數值,封送工作維持輕量,避免阻塞並維持動畫流暢。

## Risks / Trade-offs

- **[macOS 記憶體「使用率」定義模糊]** mach 的 free/active/inactive/wired/compressed 分類使「已用比例」有多種算法,易與「活動監視器」數字對不上 → 明確定義為某一公式(如 `(total − free − purgeable) / total` 或對齊活動監視器的 App+Wired+Compressed),寫進實作註解並以純函式單元測試。
- **[一般化輪詢服務動到既有 CPU 路徑]** 重構可能回歸既有動畫變速行為 → 倚賴既有測試 + 保持事件介面相容,CPU 發布語意不變。
- **[原生模糊在某些環境失效]** 部分 Linux/遠端桌面/舊 Windows 無合成器時模糊降級為半透明 → 接受降級(hint 已含 `Blur` 後援),tint Border 仍保證可讀性。
- **[`SystemDecorations=None` 失去拖曳/關閉]** 無系統邊框後視窗無法用標題列移動 → MVP 接受固定位置;若需拖曳再於 Border 加 `PointerPressed` + `BeginMoveDrag`。
- **[面板顯示位置]** 無法貼齊托盤,可能出現在螢幕中央或角落 → MVP 接受預設/工作區角落定位,精準貼齊列為後續。

## Open Questions

- ~~輪詢服務:一般化承載多來源,或新增第二個實例?~~ **已定版(D4)**:Option A,兩個獨立實例 + 中性 `IUsageSource`;Windows 記憶體改走 `GlobalMemoryStatusEx` 以與 LHM 脫鉤。
- macOS 記憶體使用率公式:**暫定** `(active+wired+compressed)/total`,以純函式 + 單元測試固定;待 macOS 實機與活動監視器比對後可微調。
- 面板初始顯示位置:螢幕角落、游標附近、或畫面中央?(MVP 先採視窗預設/置中,精準貼齊托盤列為後續)
