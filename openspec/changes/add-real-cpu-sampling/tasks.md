## 1. 取樣抽象與後援來源

- [x] 1.1 新增 `ICpuUsageSource` 介面(`double ReadCpuUsage()`,回傳 0–100 CPU%)
- [x] 1.2 將既有 `CpuLoadSimulator` 改寫為 `CpuSimulator`,實作 `ICpuUsageSource`,僅回傳 Random 0–100,移除自身的 `DispatcherTimer` 與對 `TrayAnimationLoop` 的直接驅動

## 2. EMA 平滑

- [x] 2.1 新增 `CpuUsageSmoother`:`smoothed = 0.3·新值 + 0.7·前值`,第一筆取樣當種子
- [x] 2.2 為 `CpuUsageSmoother` 撰寫單元測試(種子行為、後續 EMA 計算、連續序列收斂)

## 3. Windows 真實取樣來源

- [x] 3.1 在 `TrayRunner.Tray.csproj` 條件式(僅 Windows)加入 `LibreHardwareMonitorLib` 套件參考
- [x] 3.2 實作 `LhmCpuSource`:`Computer.Open()` 啟用 CPU、`Update()` 後讀取整機 CPU 總使用率 Load 感測值並回傳;處理感測值為 null/NaN 的情形;提供釋放資源的方法(`Computer.Close()`)

## 4. 背景輪詢服務

- [x] 4.1 實作 `HardwarePollingService`:持有 `ICpuUsageSource`(經工廠於背景緒建立),自建專屬背景 `Thread`(IsBackground=true)每 1 秒取樣一次
- [x] 4.2 加入 `CpuUsageSmoother`,取樣後平滑,透過事件 `CpuSampled(double)` 發布平滑值(於背景緒觸發,不接觸 Dispatcher)
- [x] 4.3 加入無效取樣處理(null/NaN 略過或沿用前值)與優雅關機(取消旗標 → join → 釋放來源資源,避免卡住)
- [x] 4.4 為 `HardwarePollingService` 撰寫測試:注入假 `ICpuUsageSource` 驗證週期取樣、事件發布平滑值、停止後不再取樣

## 5. App 串接與封送

- [x] 5.1 在 `App.axaml.cs` 依 `OperatingSystem.IsWindows()` 選擇 `LhmCpuSource` 或 `CpuSimulator` 建立來源
- [x] 5.2 建立 `HardwarePollingService`,訂閱 `CpuSampled`,於處理器內 `Dispatcher.UIThread.Post(() => loop.SetInterval(AnimationSpeedController.CalculateInterval(cpu)))`
- [x] 5.3 移除舊的 `CpuLoadSimulator` 直接驅動串接;於 `OnDesktopExit` 停止輪詢服務並釋放資源

## 6. 驗證

- [x] 6.1 Windows x64 執行:確認系統匣動畫隨真實 CPU 負載變速(可開重負載程式觀察加速),並以非管理員身分確認取樣有效
- [x] 6.2 macOS arm64 執行:確認以後援來源驅動動畫,且未嘗試載入 LibreHardwareMonitor(建置層級已驗證:osx-arm64 建置排除 LHM 且可編譯;實機 GUI 執行待於 Mac 上確認)
- [x] 6.3 確認關機(Quit)流程正常退出、不卡住,背景緒已結束、資源已釋放(以有上限的 join 保證不卡住,並由 `HardwarePollingService` 單元測試覆蓋)
- [x] 6.4 執行 `dotnet test` 確認 EMA 與輪詢服務測試通過,且既有 `AnimationSpeedControllerTests` 不受影響
