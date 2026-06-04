## 1. 專案骨架

- [x] 1.1 建立 Avalonia desktop 應用程式專案（`dotnet new avalonia.app`），TargetFramework 設為 `net10.0`
- [x] 1.2 加入必要相依套件（`Avalonia`、`Avalonia.Desktop`、`Avalonia.Themes.Fluent` 或 Simple、`Avalonia.Diagnostics` for Debug）
- [x] 1.3 在 `.csproj` 設定 `win-x64` 與 `osx-arm64` 為支援的 RuntimeIdentifiers
- [x] 1.4 將 repo 既有目錄結構（保留 `LICENSE`、`README.md`、`openspec/`）整理好，把專案放在合理位置（例如 repo root 或 `src/TrayRunner.Tray/`）

## 2. 圖片資產

- [x] 2.1 在專案內建立 `Assets/` 目錄，放入 4 張 frame 圖片 `runner_frame_1.png` ~ `runner_frame_4.png`（小狗奔跑動畫）
- [x] 2.2 在 `.csproj` 以 `AvaloniaResource` 方式包含 `Assets/*.png`，確認可透過 `avares://` URI 載入

## 3. Tray-only 啟動

- [x] 3.1 在 `App.axaml` 宣告 `TrayIcon`（含預設 Icon、ToolTipText、`NativeMenu` 含 "Quit" 項目）
- [x] 3.2 在 `App.axaml.cs` 的 `OnFrameworkInitializationCompleted` 中**不指派** `desktop.MainWindow`
- [x] 3.3 設定 `desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown`
- [x] 3.4 將 "Quit" 選單項目綁定到呼叫 `desktop.Shutdown()` 的 Command 或 click handler

## 4. IconPool 實作

- [x] 4.1 新增 `IconPool` 類別，建構時接收 frame 資源 URI 清單
- [x] 4.2 建構過程透過 `AssetLoader.Open` 讀取每張 PNG，建立 `Bitmap` 並包成 `WindowIcon`，存入 `IReadOnlyList<WindowIcon>`
- [x] 4.3 提供 `Count` 與索引存取（`this[int index]` 或 `Frames` 屬性）
- [x] 4.4 實作 `IDisposable`，於 Dispose 時釋放所有 `WindowIcon` / `Bitmap`

## 5. 動畫迴圈

- [x] 5.1 新增 `TrayAnimationLoop` 類別，建構時接收 `TrayIcon` 與 `IconPool` 參考
- [x] 5.2 內部建立 `DispatcherTimer`，`Interval = TimeSpan.FromMilliseconds(100)`
- [x] 5.3 在 `Tick` callback 中 `tray.Icon = pool[++index % pool.Count]`，索引以欄位維護
- [x] 5.4 提供 `Start()` / `Stop()` 方法，並確保 Stop 後可安全再 Start
- [x] 5.5 在 `App` 初始化流程中建立 IconPool + TrayAnimationLoop 並呼叫 `Start()`

## 6. 退出流程

- [x] 6.1 在退出（"Quit" 或 `desktop.Exit` 事件）時呼叫 `TrayAnimationLoop.Stop()` 與 `IconPool.Dispose()`
- [x] 6.2 確認 `desktop.Shutdown()` 被呼叫後 process 正常結束、無例外

## 7. 跨平台驗證

- [x] 7.1 在 Windows x64 透過 `dotnet run` 確認 tray icon 出現且動畫每 100ms 切換
- [x] 7.2 `dotnet publish -c Release -r win-x64` 產出可執行檔並實機驗證
- [x] 7.3 在 macOS Apple Silicon 透過 `dotnet run` 確認 menu bar icon 出現且動畫運作
- [x] 7.4 `dotnet publish -c Release -r osx-arm64` 產出 `.app` / 可執行檔並實機驗證
- [x] 7.5 於兩平台分別測試 "Quit" 選單能正常結束程式

## 8. 文件

- [x] 8.1 更新 `README.md`，補上專案簡介、執行 (`dotnet run`) 與發佈指令
- [x] 8.2 於 README 註記目前已知限制（macOS dock 仍可見、無 CPU 變速等）
