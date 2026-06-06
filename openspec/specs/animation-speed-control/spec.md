# animation-speed-control

### Requirement: CPU 使用率透過指數曲線映射到影格間隔

系統 SHALL 使用指數(對數線性)曲線 `interval = 300 · (1/15)^(cpu/100)` 將 CPU 使用率映射到動畫影格間隔,其中 `cpu` 是介於 0–100 的百分比,結果為以毫秒計的時間長度。此映射 SHALL 在 0% CPU 時產生 300ms、在 100% CPU 時產生 20ms,且 CPU 每增加相同幅度,間隔皆產生相同倍率的變化。

#### Scenario: 最低負載映射到最慢間隔
- **WHEN** 以 CPU 值 0 呼叫 `CalculateInterval`
- **THEN** 回傳 300ms 的間隔

#### Scenario: 最高負載映射到最快間隔
- **WHEN** 以 CPU 值 100 呼叫 `CalculateInterval`
- **THEN** 回傳 20ms 的間隔

#### Scenario: 中段負載依循指數曲線
- **WHEN** 以 CPU 值 50 呼叫 `CalculateInterval`
- **THEN** 回傳約 77.5ms 的間隔(300 · (1/15)^0.5 = 300/√15),而非線性中點的 160ms

### Requirement: CPU 輸入鉗制於有效範圍

系統 SHALL 在套用映射之前,將 CPU 輸入鉗制(clamp)到 `[0, 100]` 範圍,使超出範圍的取樣值(負值或超過 100 的值)無法產生超出 300ms–20ms 邊界的間隔。

#### Scenario: 超過 100 的值被鉗制
- **WHEN** 以 CPU 值 140 呼叫 `CalculateInterval`
- **THEN** 輸入被視為 100,並回傳 20ms

#### Scenario: 負值被鉗制
- **WHEN** 以 CPU 值 -10 呼叫 `CalculateInterval`
- **THEN** 輸入被視為 0,並回傳 300ms

### Requirement: 動畫間隔於執行期更新

動畫迴圈 SHALL 在運行中接受新的影格間隔並予以套用,使後續的影格切換採用更新後的速度,且不從第 0 格重新開始播放。

#### Scenario: 更新間隔改變播放速度
- **WHEN** 對運行中的動畫迴圈套用新的間隔
- **THEN** 後續的影格切換以新間隔發生
- **AND** 目前的影格索引被保留(動畫不重置回第一格)

### Requirement: 模擬器驅動動態變速以供視覺驗證

系統 SHALL 提供一個 CPU 負載模擬器作為 `ICpuUsageSource` 的後援實作,在非 Windows 平台或無真實取樣來源時產生 0–100 範圍內的隨機 CPU 值。此後援來源 SHALL 與真實來源一樣由背景輪詢服務以固定週期(每 1 秒)取樣,所得值經 EMA 平滑後傳入速度控制器並套用到動畫迴圈,使得在沒有真實 CPU 取樣機制的平台上仍能驅動動態變速。模擬器 SHALL NOT 自行驅動計時或直接更新動畫迴圈,僅作為被輪詢的取樣來源。

#### Scenario: 後援來源驅動動畫速度
- **WHEN** 應用程式以後援模擬器作為取樣來源運行
- **THEN** 背景輪詢服務每約 1 秒向模擬器取得一個介於 [0, 100] 的隨機 CPU 值
- **AND** 該值經 EMA 平滑後,動畫迴圈的間隔被更新為控制器針對平滑值回傳的值
