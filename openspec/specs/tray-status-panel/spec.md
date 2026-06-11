# tray-status-panel

## Purpose

定義托盤狀態面板視窗的行為規格，包含喚出機制、視窗生命週期、外觀樣式、定位與拖曳，以及指標卡片的呈現方式。

## Requirements

### Requirement: 透過 NativeMenu 統一喚出狀態面板

系統 SHALL 在現有 `TrayIcon` 的 `NativeMenu` 中提供一個「顯示面板」選單項,使用者點擊該項時 SHALL 顯示狀態面板視窗。喚出路徑 SHALL 在所有平台(Windows / macOS / Linux)使用同一份程式碼,MUST NOT 依賴 `TrayIcon.Clicked` 事件(該事件在 macOS 不觸發、於 Linux 各桌面環境行為不一致)。

#### Scenario: 點擊選單項顯示面板
- **WHEN** 使用者於托盤圖示開啟選單並點擊「顯示面板」項
- **THEN** 狀態面板視窗顯示於畫面上
- **AND** 此行為在 Windows 與 macOS 上一致

#### Scenario: 不依賴 Clicked 事件
- **WHEN** 應用程式於 macOS 執行
- **THEN** 面板仍可透過「顯示面板」選單項喚出,不因 `TrayIcon.Clicked` 不觸發而失效

### Requirement: 單一視窗實例與 toggle 行為

系統 SHALL 以單一面板視窗實例服務整個應用程式生命週期。當面板隱藏時觸發喚出 SHALL 顯示該實例;當面板已顯示時再次觸發 SHALL 隱藏該實例(toggle)。系統 MUST NOT 在每次喚出時重建視窗,以保留面板狀態並避免不必要的資源配置。關閉面板 SHALL 為隱藏而非銷毀,且 MUST NOT 觸發應用程式結束(維持 `OnExplicitShutdown`)。

#### Scenario: 重複喚出切換顯示與隱藏
- **WHEN** 面板目前為隱藏狀態且使用者觸發喚出
- **THEN** 面板顯示
- **WHEN** 面板目前為顯示狀態且使用者再次觸發喚出
- **THEN** 面板隱藏,而非建立第二個視窗

#### Scenario: 關閉面板不結束應用程式
- **WHEN** 面板被關閉(隱藏)
- **THEN** 托盤圖示與動畫持續運作,應用程式不結束

### Requirement: 無邊框透明毛玻璃視窗外觀

面板視窗 SHALL 隱藏作業系統預設標題列與邊框(Avalonia 12 以 `WindowDecorations="None"` 達成;`SystemDecorations` 已於 12 標為過時)、採透明視窗背景(`Background="Transparent"`)、不顯示於工作列(`ShowInTaskbar="False"`)、置頂(`Topmost="True"`)、且不可調整大小(`CanResize="False"`)。視窗 SHALL 透過 `TransparencyLevelHint` 以 `AcrylicBlur, Blur` 的優先順序請求 OS 原生模糊材質,使平台不支援首選材質時可降級。面板內容 SHALL 包覆於一個具圓角(`CornerRadius="12"`)、細邊框與半透明 tint 的 `Border` 中,以呈現 frosted acrylic 毛玻璃質感。此 `Border` MUST NOT 使用會外擴出視窗邊界的 `BoxShadow`:在無邊框透明視窗下,外擴陰影會被視窗邊界裁切而在底部留下突兀暗邊。

#### Scenario: 視窗無系統裝飾且透明
- **WHEN** 面板視窗顯示
- **THEN** 不顯示作業系統標題列與邊框
- **AND** 視窗背景為透明,可透出底層模糊材質
- **AND** 視窗不出現在工作列且置於最上層

#### Scenario: 原生模糊材質可跨平台降級
- **WHEN** 執行平台不支援 `AcrylicBlur`
- **THEN** 視窗降級請求 `Blur` 材質,而非失去透明效果

### Requirement: 面板可拖曳移動且預設出現在狀態列附近

面板視窗 SHALL 在首次顯示時定位到「狀態列附近」的螢幕工作區角落:Windows 托盤位於右下角、macOS 選單列位於右上角。此為近似定位 —— 精準貼齊托盤/選單列圖示需要圖示的螢幕座標,Avalonia 無法提供,仍列為後續。由於無系統標題列,面板 SHALL 支援在其玻璃區域按住左鍵拖曳以移動整個視窗(`BeginMoveDrag`)。使用者一旦手動移動過面板,系統 SHALL 尊重其擺放位置,不在後續顯示時再拉回預設角落。

#### Scenario: 首次顯示定位於狀態列角落
- **WHEN** 面板首次顯示且使用者尚未手動移動
- **THEN** 於 Windows 出現在工作區右下角、於 macOS 出現在工作區右上角(皆貼右側、留邊距)

#### Scenario: 可按住拖曳移動
- **WHEN** 使用者於面板玻璃區域按住左鍵並拖曳
- **THEN** 整個視窗隨指標移動

#### Scenario: 移動後尊重使用者位置
- **WHEN** 使用者拖曳移動面板後將其隱藏,稍後再次顯示
- **THEN** 面板出現在使用者上次擺放的位置,而非預設角落

### Requirement: 面板呈現 CPU 與記憶體指標卡片

面板 SHALL 至少呈現兩張指標卡片:CPU 使用率與記憶體使用率,各以百分比(0–100)顯示即時讀數。卡片數值 SHALL 由背景輪詢服務發布的取樣值更新,且更新 SHALL 於 UI 執行緒套用,MUST NOT 阻塞 UI 執行緒。面板隱藏時系統 MAY 不更新卡片,但再次顯示時 SHALL 反映最新可用讀數。

#### Scenario: 顯示即時 CPU 與記憶體讀數
- **WHEN** 面板顯示且輪詢服務持續發布取樣值
- **THEN** CPU 卡片顯示介於 [0, 100] 的最新 CPU 使用率
- **AND** 記憶體卡片顯示介於 [0, 100] 的最新記憶體使用率

#### Scenario: 數值更新於 UI 執行緒套用
- **WHEN** 背景緒取得新的取樣值
- **THEN** 卡片數值的更新被封送至 UI 執行緒套用
- **AND** 不在背景緒直接存取 UI 物件
