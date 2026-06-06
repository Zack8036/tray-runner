## 1. 差分換算純函式

- [x] 1.1 新增 `CpuTickDelta`(純函式)接收前後兩筆 tick 快照(user/system/idle/nice),回傳 [0,100] 使用率或 `double.NaN`;以無號型別保存與相減以正確處理回繞
- [x] 1.2 為 `CpuTickDelta` 補單元測試:首樣無前值回傳 NaN、Δtotal=0 回傳 NaN、tick 回繞、一般差分換算結果落在 [0,100]

## 2. macOS P/Invoke 取樣來源

- [x] 2.1 新增 `MacCpuSource : ICpuUsageSource`,標註 `[SupportedOSPlatform("macos")]`,以 `[DllImport("libSystem.dylib")]` 宣告 `mach_host_self`、`host_statistics`,並定義 `host_cpu_load_info` struct 與 `HOST_CPU_LOAD_INFO`/count/`KERN_SUCCESS` 常數
- [x] 2.2 在 `ReadCpuUsage()` 中讀取當前 tick 快照、保存前值、委派 `CpuTickDelta` 換算;非 `KERN_SUCCESS`、首樣、Δtotal=0 皆回傳 NaN
- [x] 2.3 確認 `mach_host_self()` 取得的 host port 是否需 `mach_port_deallocate` 釋放,於 `Dispose()` / 程式碼註解中記錄結論並避免 port 洩漏

## 3. 工廠接線

- [x] 3.1 於 `App.CreateCpuSource()` 新增 `OperatingSystem.IsMacOS()` 分支,以 try/catch 回傳 `MacCpuSource`,失敗則退回 `CpuSimulator`(與 Windows 端 LHM 退回邏輯對稱)
- [x] 3.2 確認 `HardwarePollingService`、`CpuUsageSmoother`、`AnimationSpeedController`、csproj 均不需更動(NaN 略過契約原樣涵蓋首樣)

## 4. 驗證

- [x] 4.1 跨平台建置:確認 macOS(osx-arm64,無 LHM)與 Windows / 無 RID 開發建置皆編譯通過,單元測試全綠
- [x] 4.2 macOS Apple Silicon 實機冒煙:menu bar 動畫依真實 CPU 變速,閒置 / 滿載時讀數趨勢與 Activity Monitor 一致且落在 [0,100],長時間執行無 port 洩漏

## 5. 收尾

- [x] 5.1 `openspec validate add-macos-cpu-sampling --strict` 通過,並於實作完成後同步主 specs
