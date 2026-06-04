## Why

目前 repo 還沒有任何應用程式碼，需要先建立一個最小可動的 RunCat-like 跨平台狀態列常駐程式骨架，作為後續 CPU 偵測、變速動畫、多主題等功能的基礎。先把「會動的小狗在 tray 跑」做出來，可以最快驗證 Avalonia TrayIcon 在 Windows x64 與 macOS Apple Silicon 兩個目標平台都行得通，並把資源預載 / Dispatcher 動畫迴圈這兩個核心機制的雛形定下來。

## What Changes

- 新增一個 Avalonia (.NET) 桌面應用程式專案，作為整個 system monitor 的進入點。
- 程式啟動時**不顯示主視窗**，只在系統匣 / menu bar 顯示 TrayIcon；shutdown 採顯式退出 (`OnExplicitShutdown`)。
- 在啟動階段把 4 張 frame 圖片 (`runner_frame_1.png` ~ `runner_frame_4.png`) 預載成 `WindowIcon`，存成唯讀清單，避免動畫迴圈中重複配置帶 native resource 的物件。
- 使用 `DispatcherTimer` 以固定 100ms 的間隔依序切換 `TrayIcon.Icon`，產生角色奔跑動畫。
- TrayIcon 右鍵選單僅提供 "Quit" 用以關閉程式。
- 目標執行平台：Windows x64 與 macOS arm64 (Apple Silicon)。

## Capabilities

### New Capabilities
- `tray-animation`: 狀態列常駐圖示、預載的圖框資源池、固定速率動畫迴圈，以及最小退出選單。

### Modified Capabilities
<!-- 無 -->

## Impact

- **新增程式碼**：Avalonia 應用程式專案（`App.axaml` / `App.axaml.cs` / `Program.cs`）、IconPool、動畫迴圈、TrayIcon 設定、4 張 frame 圖片資產。
- **新增相依套件**：`Avalonia`、`Avalonia.Desktop`、`Avalonia.Themes.Fluent`（或 Simple）。
- **建置目標**：`net10.0`，RID 至少涵蓋 `win-x64`、`osx-arm64`。
- **本次不含**：CPU 取樣 / 變速、設定視窗、開機自動啟動、macOS `LSUIElement`（dock 隱藏）、簽章 / 公證、CI 打包流程。
