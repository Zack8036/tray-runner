## Why

`add-real-cpu-sampling` 已讓 Windows 以 LibreHardwareMonitor 真實驅動動畫速度,但 macOS 仍退回 `CpuSimulator` 隨機值,只能視覺驗證、不反映真實負載。`ICpuUsageSource` 接縫早已預留 macOS 接點,現在補上 macOS 真實取樣,讓 Apple Silicon 上的奔跑速度也對應到實際 CPU 忙碌程度,完成跨平台真實取樣的最後一塊。

## What Changes

- 新增 macOS 實作 `MacCpuSource`,以 P/Invoke 呼叫 `libSystem` 的 mach API `host_statistics(HOST_CPU_LOAD_INFO)` 讀取整機累計 CPU tick(user / system / idle / nice),並以**前後兩次快照的差分**換算 [0, 100] 的整機 CPU 總使用率。
- 將「兩個 tick 快照 → 使用率%」的差分換算抽成**純函式**(如 `CpuTickDelta.Compute`),與 P/Invoke 殼層分離,使核心邏輯可跨平台單元測試(延續 `CpuUsageSmoother` 為純邏輯的風格)。
- 第一次取樣(尚無前值)、Δtotal 為 0、或 mach 呼叫非 `KERN_SUCCESS` 時回傳 `NaN`;`HardwarePollingService` 既有的 `IsNaN` 略過契約原樣涵蓋此情況,無需更動輪詢服務。
- `App.CreateCpuSource()` 工廠新增 `OperatingSystem.IsMacOS()` 分支回傳 `MacCpuSource`;`CpuSimulator` 由「macOS 主來源」降級為「macOS 取樣建立失敗時的最終後援」,與 Windows 端 LHM 失敗退回 simulator 的邏輯對稱。
- `MacCpuSource` 不需條件編譯常數:mach API 只是對 `libSystem.dylib` 的 `DllImport`,任何平台都能編譯,僅在執行期解析,由工廠的平台判斷守住不在非 macOS 上實例化(對比 LHM 需 `LHM_AVAILABLE` 是因其為 Windows 專屬 NuGet 套件)。標註 `[SupportedOSPlatform("macos")]` 供分析器檢查。

## Capabilities

### New Capabilities
<!-- 無新能力;本次擴充既有 hardware-cpu-sampling 能力的平台來源。 -->

### Modified Capabilities
- `hardware-cpu-sampling`: 「依平台切換的 CPU 取樣來源」需求由「非 Windows 一律用後援模擬器」改為「macOS 使用以 mach `host_statistics` 差分計算的真實取樣來源 `MacCpuSource`,模擬器降為取樣建立失敗時的後援」;並新增「差分式取樣的首樣以 NaN 表示無前值」之行為。
- `tray-animation`: 「跨平台執行支援」需求中 macOS 的取樣來源由「後援模擬器」改為「真實 mach CPU 取樣」,macOS scenario 隨之更新為依真實 CPU 變速。

## Impact

- **新增程式碼**:`MacCpuSource`(P/Invoke 殼層)、`CpuTickDelta`(純差分換算,可測)。
- **修改程式碼**:`App.axaml.cs`(`CreateCpuSource` 新增 macOS 分支)。`ICpuUsageSource`、`HardwarePollingService`、`CpuUsageSmoother`、`AnimationSpeedController`、`TrayAnimationLoop`、`LhmCpuSource`、`CpuSimulator` 均不變。
- **相依**:無新 NuGet 套件;僅新增對系統內建 `libSystem.dylib` 的 P/Invoke。
- **csproj**:無需更動(不新增條件編譯常數或套件)。
- **測試**:`CpuTickDelta` 差分邏輯(首樣 NaN、Δtotal=0、tick 回繞、一般換算)以單元測試覆蓋;P/Invoke 殼層僅能於 macOS 實機驗證。
- **風險**:mach API 的 `natural_t`/`integer_t` 為 32-bit、tick 計數可能回繞(unsigned 相減即正確);`mach_host_self()` 取得的 host port 慣例上不需釋放,但須於實作時確認無 port 洩漏;實機需於 Apple Silicon 上驗證讀數與 Activity Monitor 趨勢一致。
