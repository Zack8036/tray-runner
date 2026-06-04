# TrayRunner

RunCat-like 系統匣常駐程式，以 Avalonia (.NET 10) 開發，支援 Windows x64 與 macOS Apple Silicon。

程式啟動後不顯示主視窗，僅在系統匣（Windows）或 menu bar（macOS）顯示動畫圖示；右鍵選單提供 **Quit** 退出。

## 執行

```bash
cd src/TrayRunner.Tray
dotnet run
```

## 發佈

**Windows x64**
```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

**macOS Apple Silicon**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained false
```

產出檔案位於 `bin/Release/net10.0/<RID>/publish/`。

## 已知限制

- **macOS dock 仍會顯示 app icon**：純 menu-bar 體驗需在 `Info.plist` 設定 `LSUIElement = true`，本版本尚未實作。
- **固定 100ms 動畫速率**：目前不依 CPU 負載變速，為後續功能預留。
- **無開機自動啟動**：需另外設定 LaunchAgent（macOS）或 Task Scheduler（Windows）。
- **圖片資產為佔位圖**：`Assets/cat_frame_*.png` 目前為彩色方塊，可替換成正式動畫圖片。
