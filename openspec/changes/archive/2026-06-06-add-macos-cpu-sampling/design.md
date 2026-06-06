## Context

`add-real-cpu-sampling` 已建立 `ICpuUsageSource` 接縫:Windows 用 `LhmCpuSource`(`#if LHM_AVAILABLE`),非 Windows 用 `CpuSimulator` 後援;`App.CreateCpuSource()` 以 `OperatingSystem.IsWindows()` 選擇來源,`HardwarePollingService` 在專屬背景緒每 1 秒呼叫 `ReadCpuUsage()`,對 `NaN` 略過、對有效值做 EMA 平滑後封送至 UI 緒變速。

macOS 目前落在 `CpuSimulator` 隨機後援。本次補上 macOS 真實取樣。關鍵約束:

- 整機 CPU% 在 .NET 無純跨平台 API;macOS 需 P/Invoke mach API。
- mach 的 CPU 載入資訊是**自開機以來的累計 tick**,非當下使用率,必須前後兩次快照相減才能算出百分比。
- 既有 spec `tray-animation` 要求單一程式碼基底於 Windows x64 與 macOS arm64 行為一致;`HardwarePollingService`、`CpuUsageSmoother`、`AnimationSpeedController` 維持平台無關不變。

## Goals / Non-Goals

**Goals:**
- 在 macOS (Apple Silicon) 以真實整機 CPU 總使用率驅動動畫速度。
- 透過既有 `ICpuUsageSource` 接縫接入,不更動輪詢、平滑、變速、封送等既有元件。
- 將差分換算抽為純函式,使核心邏輯可跨平台單元測試。
- 取樣建立或讀取失敗時優雅退回 `CpuSimulator`,與 Windows 端對稱。

**Non-Goals:**
- 不做每核心(per-core)使用率或視覺化(僅整機總值)。
- 不讀取溫度 / 風扇 / 頻率等其他感測值。
- 不更動取樣週期(維持 1 秒)、EMA 係數(維持 α=0.3)、或變速映射曲線。
- 不支援 Intel mac 以外的特殊情境調校(P/Invoke 對 x64/arm64 mac 皆適用,但本案僅於 Apple Silicon 實機驗證)。

## Decisions

### 決策 1:採用 `host_statistics(HOST_CPU_LOAD_INFO)` 而非 `host_processor_info` 或 `sysctl`

**選擇**:以 `host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, ref info, ref count)` 讀取已跨核心加總的 `host_cpu_load_info`(固定 4 個 `natural_t`:user / system / idle / nice)。

**理由**:回傳的就是「整機總和」,正中需求;為固定大小 struct,P/Invoke 可直接 `ref` 傳遞、無變長陣列、無需 `vm_deallocate`,表面積最小。

**替代方案**:
- `host_processor_info(PROCESSOR_CPU_LOAD_INFO)` 給每核心明細,須自行加總並 `vm_deallocate` 釋放 kernel 配置的陣列,複雜度高一截,只有將來要做 per-core 視覺化才值得。
- `sysctl` 在 macOS 上無 FreeBSD 式 `kern.cp_time` 整機載入計數,適合查核心數而非算 load,排除為主路。

### 決策 2:差分換算抽成純函式 `CpuTickDelta`,與 P/Invoke 殼層分離

**選擇**:`MacCpuSource.ReadCpuUsage()` 只負責呼叫 mach API 取得當前 tick 快照並保存前值;百分比計算交給純函式,如:
```
usage% = Δ(user+system+nice) / Δ(user+system+nice+idle) × 100
```

**理由**:P/Invoke 殼層只能於 macOS 實機跑,跨平台 CI 測不到;把差分數學抽出後即為純運算,可在任何平台單元測試,延續 `CpuUsageSmoother` 為純邏輯、有測試的風格。`MacCpuSource` 因而只剩薄殼。

**替代方案**:把計算內嵌於 `MacCpuSource`——無法在非 macOS 上測試核心邏輯,放棄。

### 決策 3:差分模型的首樣與無效情況一律回傳 NaN

**選擇**:`MacCpuSource` 內保存上一次 tick 快照。第一次取樣(無前值)、Δtotal 為 0、或 `host_statistics` 回傳非 `KERN_SUCCESS` 時回傳 `double.NaN`。

**理由**:`HardwarePollingService` 第 90 行 `if (double.IsNaN(raw)) continue;` 既有契約原本為「感測值尚未就緒」設計,剛好涵蓋「差分尚無前值」與「讀取失敗」。因此**輪詢服務完全不需更動**,首樣自然被跳過,第二樣起才產生有效讀數。

### 決策 4:不引入條件編譯常數,僅靠工廠平台判斷守護

**選擇**:`MacCpuSource` 無條件參與編譯;`App.CreateCpuSource()` 以 `OperatingSystem.IsMacOS()` 決定是否實例化;類別標註 `[SupportedOSPlatform("macos")]`。

**理由**:mach API 只是對系統內建 `libSystem.dylib` 的 `DllImport`,任何平台都能編譯、僅執行期解析——與 `LhmCpuSource` 需 `LHM_AVAILABLE`(因 LHM 是 Windows 專屬 NuGet 套件,連 build 都不能進 macOS RID)的處境不同。故 csproj 無需任何更動,比 Windows 端更單純。`[SupportedOSPlatform]` 讓平台分析器不對 mac 專屬呼叫示警。

**替代方案**:仿 LHM 加 `MAC_AVAILABLE` 常數——多餘,因為沒有平台專屬套件需要被排除,徒增複雜度。

### 決策 5:取樣建立 / 讀取失敗退回 `CpuSimulator`

**選擇**:`CreateCpuSource()` 的 macOS 分支以 try/catch 包覆 `new MacCpuSource()`,失敗則退回 `new CpuSimulator()`,與既有 Windows 端 LHM 初始化失敗退回 simulator 的邏輯對稱。

**理由**:即使極端情況下 mach 呼叫不可用,動畫仍能變速運作而非整個取樣失效;`CpuSimulator` 因此保留為 macOS 的最終後援而非被移除。

## Risks / Trade-offs

- **mach 型別寬度與 tick 回繞**:`natural_t` / `integer_t` 為 32-bit;tick 為無號累計值,長時間運行可能回繞。→ 緩解:以無號型別保存與相減,回繞時差分自然正確;`CpuTickDelta` 以單元測試覆蓋回繞情境。
- **`mach_host_self()` 的 host port 釋放**:慣例上該 port 為快取的 send right,多數實作不顯式釋放。→ 緩解:實作時確認是否需 `mach_port_deallocate` 避免長時間執行的 port 洩漏,並於程式碼註解記錄結論。
- **讀數與 Activity Monitor 不一致**:差分視窗(1 秒)與取樣對齊方式可能與系統工具略有出入。→ 緩解:於 Apple Silicon 實機目視驗證閒置 / 滿載時趨勢正確且落在 [0,100],非要求數值逐位相符。
- **P/Invoke 殼層無法跨平台測試**:→ 緩解:核心差分邏輯已抽為純函式覆蓋測試,殼層僅留最薄的 mach 呼叫,於 macOS 實機冒煙驗證。

## Open Questions

- 是否需要顯式 `mach_port_deallocate(mach_task_self(), host)` 釋放 host port?(實作時於 macOS 驗證長時間執行無 port 洩漏後定案)
