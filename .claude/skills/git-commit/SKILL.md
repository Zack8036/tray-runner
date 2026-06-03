---
name: git-commit
description: |
  協助使用者撰寫符合 Conventional Commits 規範的 git commit 訊息，並執行 commit。
  當使用者想要 commit 程式碼、需要寫 commit message、詢問「怎麼 commit」、說「幫我 commit」、
  「我要 commit」、「提交變更」、「commit 這些修改」、「幫我寫 commit message」，
  或任何與 git commit 相關的操作時，請使用此 skill。
  也適用於使用者已 stage 了一些檔案但不知道如何撰寫訊息的情況。
  請在使用者提到 commit、提交、git commit 時主動觸發此 skill。
---

# Git Commit（依照 Conventional Commits）

協助分析 staged 的變更內容，自動推薦合適的 Conventional Commits 格式，
引導使用者完成 commit message 的撰寫，最後執行 `git commit`。

## 執行流程

### 第一步：確認 staged 狀態

執行以下指令，掌握目前的變更全貌：

```bash
git status
git diff --staged
```

- 如果沒有任何 staged 的變更，告知使用者目前沒有可以 commit 的內容，並提示他們可以用 `git add` 來 stage 檔案
- 如果有 unstaged 的修改，順便提示使用者是否也要一起 stage

### 第二步：分析變更並推薦 type

根據 `git diff --staged` 的內容，判斷最適合的 commit type：

| Type | 使用時機 |
|------|----------|
| `feat` | 新增功能（對使用者有感的新能力） |
| `fix` | 修復 bug |
| `docs` | 只更動文件（README、註解等），不影響程式邏輯 |
| `style` | 格式調整（空白、縮排、分號）、不影響程式邏輯 |
| `refactor` | 重構程式碼，既非新增功能也非修復 bug |
| `perf` | 效能改善 |
| `test` | 新增或修改測試，不影響主程式 |
| `build` | 影響 build 系統或外部相依套件（如 webpack、npm） |
| `ci` | 修改 CI/CD 設定或腳本 |
| `chore` | 其他維護性工作，不修改 src 或測試（如更新 .gitignore） |
| `revert` | 還原先前的 commit |

分析完後，推薦一個或多個你認為最合適的 type，並簡短說明原因。

### 第三步：互動式確認

用一個簡潔的清單向使用者確認以下資訊：

1. **Type**：展示你推薦的選項，讓使用者確認或更改
2. **Scope**（選填）：影響的模組或範疇（例如 `auth`、`api`、`ui`），若不確定可跳過
3. **Breaking Change**：是否有不向下相容的重大變更？（會在 footer 加上 `BREAKING CHANGE:`）
4. **Subject**：一句話描述這次變更（繁體中文台灣用語，專有名詞保留英文，句末不加句點）

如果能從 diff 中明確推斷出 subject，可以直接提供建議，讓使用者確認或修改，而不是讓他們從頭想。

### 第四步：組合並預覽 commit message

依照以下格式組合訊息，並在執行前讓使用者確認：

```
<type>(<scope>): <subject>

[選填的 body — 說明「為什麼」這樣做，不是「做了什麼」]

[選填的 footer，例如]
BREAKING CHANGE: <描述不相容的變更>
Closes #123
```

**範例：**

```
feat(auth): 新增 JWT refresh token 輪換機制

避免 token 遭竊後被重複使用，每次換發時一併作廢舊 token，
縮短 replay attack 的可利用時間窗口。

Closes #42
```

```
fix(api): 處理外部金流服務回傳 null 的情況
```

```
refactor(core): 將驗證邏輯抽離為獨立模組

BREAKING CHANGE: ValidationService 介面已變更，
呼叫端需更新至新的方法簽章。
```

**格式規則提醒：**
- header 行建議不超過 72 個字元（中文字較寬，盡量簡潔）
- subject 使用繁體中文台灣用語，專有名詞（API、JWT、Redis 等）保留英文
- subject 句末不加句號
- body 與 header 之間空一行
- 說明「為什麼」而非「做了什麼」（程式碼本身就說明了做了什麼）
- `BREAKING CHANGE:`、`Closes #N` 等 footer 關鍵字維持英文

### 第五步：執行 commit

使用者確認訊息後，執行：

```bash
git commit -m "$(cat <<'EOF'
<完整的 commit message>
EOF
)"
```

若 commit 成功，顯示 commit hash 與摘要讓使用者知道結果。
若因 pre-commit hook 失敗，說明錯誤原因並協助修正，而不是繞過 hook。

## 互動原則

- 推薦但不強迫：提供建議，讓使用者決定
- 盡量減少來回：若能從 diff 推斷的資訊，直接提供草稿讓使用者確認，而非問一堆問題
- 若變更範圍很大且跨越多個功能，提示使用者考慮拆分成多個 commit
- 語言使用繁體中文台灣用語與使用者溝通，commit message 本身也使用繁體中文，專有名詞保留英文
