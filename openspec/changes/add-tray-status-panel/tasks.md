## 1. 記憶體取樣資料層

- [x] 1.1 將 `ICpuUsageSource` 一般化為中性 `IUsageSource`(`ReadUsage()`,回傳 0–100),CPU 既有實作隨之改名綁定;`CpuUsageSmoother`→`UsageSmoother`、隨機後援 `CpuSimulator`→`RandomUsageSource`(CPU/記憶體共用)
- [x] 1.2 Windows 記憶體實作:以 kernel32 `GlobalMemoryStatusEx` P/Invoke 取 `dwMemoryLoad`(0–100);不依賴 `LHM_AVAILABLE`,與 CPU 的 LHM 來源脫鉤
- [x] 1.3 macOS 記憶體實作:以 P/Invoke 呼叫 mach `host_statistics64`(`HOST_VM_INFO64`)+ `sysctl`(`hw.memsize`)讀頁面統計;換算核心抽成可單測的純函式,並於註解定版「使用率」公式(`(active+wired+compressed)/total`)
- [x] 1.4 後援:沿用共用的 `RandomUsageSource` 作為記憶體後援(隨機 0–100)
- [x] 1.5 新增與 `App.CreateCpuSource()` 對稱的 `CreateMemorySource()` 工廠(依平台選擇 + 失敗退回後援)
- [x] 1.6 為 macOS 記憶體換算純函式新增單元測試(有效讀數、邊界、無效值)

## 2. 輪詢服務一般化

- [x] 2.1 一般化 `HardwarePollingService` 對中性 `IUsageSource` 取樣、以 `Sampled` 事件發布、執行緒名稱可參數化;App 跑兩個獨立實例(CPU、記憶體),各自 EMA α=0.3、每秒取樣
- [x] 2.2 確認既有 CPU 發布語意與動畫變速行為不變(迴圈邏輯一字不改,僅型別/命名)
- [x] 2.3 更新 / 新增輪詢服務測試,涵蓋記憶體發布與多來源週期
- [x] 2.4 確認 `osx-arm64` 建置不牽連 LibreHardwareMonitor(`LHM_AVAILABLE` 維持有效)

## 3. 毛玻璃面板視窗

- [x] 3.1 新增面板 `Window`(XAML):`WindowDecorations="None"`(Avalonia 12 取代過時的 `SystemDecorations`)、`Background="Transparent"`、`ShowInTaskbar="False"`、`Topmost="True"`、`CanResize="False"`、`TransparencyLevelHint="AcrylicBlur, Blur"`
- [x] 3.2 加上外層玻璃 `Border`:`CornerRadius="12"` + 細邊框 + 半透明 tint(刻意不用會被視窗邊界裁切、在底部留暗邊的 `BoxShadow`)
- [x] 3.3 設計 CPU 與記憶體兩張指標卡片(百分比顯示),版面參考 Stats 卡片式呈現;以固定 `Height` 確保兩張卡片完整不被裁切(`SizeToContent` 在透明視窗下會嚴重低估而切掉記憶體卡)
- [x] 3.4 面板 code-behind:接收 CPU 與記憶體讀數,數值更新封送至 UI 緒(`Dispatcher.UIThread.Post`)
- [x] 3.5 攔截面板關閉為隱藏(不銷毀、不觸發應用程式結束)
- [x] 3.6 面板可拖曳:玻璃區 `PointerPressed` → `BeginMoveDrag`;首次顯示定位於狀態列角落(Windows 右下 / macOS 右上),拖曳後尊重使用者位置

## 4. 喚出與接線

- [x] 4.1 在 `App.axaml` 的 `NativeMenu` 新增「顯示面板」選單項(`Quit` 之上)
- [x] 4.2 在 `App.axaml.cs` 建立單一面板視窗實例,實作「顯示面板」的 toggle(隱藏→`Show()`、顯示→`Hide()`)
- [x] 4.3 將記憶體取樣事件接到面板(沿用既有 CPU 封送至 UI 緒的模式)
- [x] 4.4 於 `OnDesktopExit` 妥善釋放面板與新增的取樣資源

## 5. 驗證

- [x] 5.1 Windows:已實機執行驗證 — 面板正確置中顯示(300×207)、毛玻璃圓角 + CPU/記憶體卡片與進度條渲染正常(PrintWindow 擷圖確認)、CPU 經 LHM、記憶體經 GlobalMemoryStatusEx 皆取得真實讀數、無例外崩潰。註:選單 toggle 喚出/隱藏為 UI 互動,程式邏輯已驗,實際點擊體驗待人工確認
- [~] 5.2 macOS:**無法於 Windows 開發機實機驗證**。已做到:換算純函式 `MacMemoryUsage` 單元測試通過、`osx-arm64` 建置/發佈成功且不含 LHM、P/Invoke 殼層依 mach/sysctl 慣例撰寫。**待 macOS 實機確認**:喚出不依賴 `Clicked` 仍可運作、記憶體數字與活動監視器方向一致
- [x] 5.3 `dotnet build` / 既有測試全綠;確認 `osx-arm64` 發佈不含 LHM
- [x] 5.4 `openspec validate add-tray-status-panel --strict` 通過
