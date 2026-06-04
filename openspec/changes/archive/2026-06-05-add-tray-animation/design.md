## Context

Repo 目前只包含 `LICENSE`、`README.md` 與 OpenSpec 設定，尚無任何應用程式碼。本 change 是整個 system monitor 應用的「第一個可執行版本」，目標是建立 Avalonia 桌面專案骨架，並驗證 RunCat-like 的核心動畫機制（TrayIcon 動畫 + 預載資源池 + DispatcherTimer 迴圈）在 Windows x64 與 macOS arm64 兩個平台都能跑。後續的 CPU 取樣、變速、設定視窗等功能會疊在這個骨架上。

主要限制：
- 必須跨平台（Win x64、macOS Apple Silicon）。
- 必須避免動畫迴圈中重複配置帶 native handle 的物件（`WindowIcon` / `Bitmap`）。
- 此階段刻意維持「最小可動」，不引入未來功能需要的抽象。

## Goals / Non-Goals

**Goals:**
- 建立可啟動、純 tray-only、無主視窗的 Avalonia 應用程式骨架。
- 落實 frame 資源池：啟動載入一次，後續只重複使用。
- 以 `DispatcherTimer` 在 UI thread 驅動 100ms 固定速率動畫。
- 提供 "Quit" 選單作為唯一的退出入口。
- 同一份程式碼可發佈到 `win-x64` 與 `osx-arm64`。

**Non-Goals:**
- CPU 取樣、依負載變速。
- 多動畫主題、可選圖集。
- 設定視窗 / 偏好持久化。
- 開機自動啟動。
- macOS `LSUIElement`（dock 隱藏）、簽章 / 公證、CI 打包。
- 國際化、無障礙、深淺色主題適配。

## Decisions

### Decision 1: 採用 `ClassicDesktopStyleApplicationLifetime` + `OnExplicitShutdown`
- **選擇**：在 `App.OnFrameworkInitializationCompleted` 中**不指派** `desktop.MainWindow`，並將 `desktop.ShutdownMode` 設為 `ShutdownMode.OnExplicitShutdown`；退出由 "Quit" 選單明確呼叫 `desktop.Shutdown()`。
- **替代方案**：建立隱藏的 invisible host window。
- **理由**：避免多餘的 Window 物件與跨平台行為差異。Avalonia 預設 `OnLastWindowClose` 在沒有任何 window 時會立刻退出，是 tray-only app 的最常見地雷，這裡用 `OnExplicitShutdown` 直接根治。

### Decision 2: TrayIcon 在 `App.axaml` 以 `TrayIcon.Icons` 宣告
- **選擇**：在 `App.axaml` 中以 XAML 宣告一個 `TrayIcon`（含預設 icon 與右鍵 `NativeMenu`），程式碼中透過 `TrayIcon.GetIcons(Application.Current)` 取得參考。
- **替代方案**：完全用 code-behind 動態建立 TrayIcon。
- **理由**：符合 Avalonia 慣例，選單結構宣告式更清楚；code-behind 仍然可以取回參考以改變 `Icon`。

### Decision 3: IconPool 為唯讀 `IReadOnlyList<WindowIcon>`，單執行緒、非執行緒安全
- **選擇**：啟動時把 `runner_frame_1.png` ~ `runner_frame_4.png`（透過 `avares://` URI + `AssetLoader`）讀成 `Bitmap`，再包成 `WindowIcon`，集中放入唯讀清單；不加鎖。
- **替代方案**：通用 Object Pool（含 rent / return 語意）、Lazy 載入。
- **理由**：本場景所有讀取都在 UI thread（DispatcherTimer 在 UI thread fire），rent/return 語意完全用不到；唯讀清單表達意圖最直接，也避免過度工程。Lazy 載入會把第一輪動畫的延遲推到 runtime，且仍需處理「載入是否完成」狀態，得不償失。

### Decision 4: 動畫使用 `DispatcherTimer`，禁止 `System.Timers.Timer`
- **選擇**：以 `DispatcherTimer` 設定 `Interval = TimeSpan.FromMilliseconds(100)`，在 `Tick` 中 `tray.Icon = pool[++index % pool.Count]`。
- **替代方案**：`System.Timers.Timer` + `Dispatcher.UIThread.Post`。
- **理由**：`DispatcherTimer` callback 本來就在 UI thread，省掉 marshalling；改 `TrayIcon.Icon` 必須在 UI thread，用對 timer 直接避免 race。

### Decision 5: 圖片資產採用 `avares://` 嵌入式資源
- **選擇**：將 4 張 PNG 放在專案 `Assets/` 下，於 `.csproj` 以 `AvaloniaResource` 包含，啟動時用 `AssetLoader.Open(new Uri("avares://.../Assets/runner_frame_N.png"))` 讀取。
- **替代方案**：放於發佈目錄旁邊的檔案系統路徑、嵌入 `EmbeddedResource`。
- **理由**：`avares://` 跨平台一致、不需要處理執行檔所在目錄的相對路徑差異，也免去 macOS `.app` bundle 結構的相容問題。

### Decision 6: 目標框架 `net10.0`，RID = `win-x64` / `osx-arm64`
- **選擇**：`<TargetFramework>net10.0</TargetFramework>`，發佈時以 `dotnet publish -r win-x64` 或 `-r osx-arm64`。
- **替代方案**：`net8.0`（現行較舊 LTS）、multi-targeting。
- **理由**：.NET 10 為現行 LTS（2025-11 釋出，支援至 2028-11），相比 .NET 8（剩餘支援不到半年）有明顯更長的支援窗口；AOT / trimming 改進對常駐型 tray app 的啟動時間與記憶體佔用直接受益；Avalonia 11.x 已完整支援。Repo 全新、無相容舊框架的需求，沒有選擇 .NET 8 的理由。

## Risks / Trade-offs

- [Risk] macOS 即使設為 tray-only，dock 仍會出現 app icon → Mitigation：明確列為 Non-Goal；之後若要做純 menu-bar 體驗，再加 `Info.plist` 的 `LSUIElement = true`。
- [Risk] menu bar 圖示在 macOS retina 螢幕模糊 → Mitigation：準備夠大的來源 PNG（建議至少 32×32 或更大），讓系統縮小；本階段不做 @1x/@2x 分流。
- [Risk] `WindowIcon` 內部包 native handle，若未在退出時釋放，重啟反覆累積可能耗 handle → Mitigation：在 Quit 流程明確 dispose 池內物件；現階段不額外做 finalizer 防護。
- [Risk] 固定 100ms 在系統高負載下可能 tick 延遲、動畫卡頓 → Mitigation：MVP 不補償；後續若做 CPU-driven 變速，自然會重新檢視 timer 行為。
- [Trade-off] 不做通用 Object Pool 介面，未來若 frame 數量或來源變動需小幅重構 → 接受，避免現在過度設計。
