# 《明日方舟：終末地》直接啟動工具

**中文** | [English](README.en.md)

在 Windows 上直接啟動《明日方舟：終末地》的小工具。透過 `config.json` 指定遊戲位置，不必先開啟 GRYPHLINK 啟動器介面。

已實測可正常啟動遊戲，測試項目見 [驗收紀錄](ACCEPTANCE.md)。

> 本專案是非官方工具，仍需安裝遊戲與官方 GRYPHLINK 啟動器。

## 特色

- **路徑可設定**：同一份 EXE 適用不同安裝位置，不需要修改原始碼。
- **自動尋找啟動器**：從 GRYPHLINK 啟動器的版本目錄尋找 `Games.exe`，也能自行指定。
- **避免重複啟動**：遊戲或本工具已在執行時，不會重複啟動。
- **自動清理**：遊戲結束後，自動結束工具建立的輔助程序。
- 不修改遊戲檔案、不安裝服務、不自動提升權限。

## 系統需求

- Windows 10／11 x64。
- .NET Framework 4.x；建置使用 Windows PowerShell 5.1 與 x64 C# 編譯器。
- 已安裝《明日方舟：終末地》與官方 GRYPHLINK 啟動器。
- 若官方執行檔要求管理員權限，請以系統管理員身分執行工具。

## 快速開始

### 1. 下載

從 [Releases](https://github.com/Willy19928/endfield-direct/releases/latest) 下載 `Endfield-Direct.exe` 和 `config.example.json`，放在同一個資料夾。可用同頁的 `SHA256SUMS` 核對檔案。

### 2. 設定路徑

將 EXE 旁的 `config.example.json` 複製為 `config.json`，填入遊戲位置：

```json
{
  "gamePath": "D:\\Games\\Endfield\\Endfield.exe",
  "launcherPath": ""
}
```

| 欄位 | 用途 |
|---|---|
| `gamePath` | 必填，指定 `Endfield.exe` |
| `launcherPath` | 留空或省略時自動尋找；也可指定官方 `Games.exe` |

路徑可使用 `/` 或 JSON 的 `\\`。相對路徑以 **EXE 所在目錄** 為基準。遊戲搬家後只需修改設定，不必重新編譯。工具不會覆寫設定檔。

### 3. 啟動

執行 `Endfield-Direct.exe`。若出現需要提升權限的訊息，按右鍵選擇「以系統管理員身分執行」。

## 常用命令

| 參數 | 用途 |
|---|---|
| 無參數 | 依設定啟動遊戲 |
| `--check-config` | 檢查設定與檔案位置，不建立遊戲或啟動器程序 |
| `--self-test` | 檢查暫停程序的 Job 清理，不執行遊戲 |

在 PowerShell 中取得檢查結果：

```powershell
$p = Start-Process .\Endfield-Direct.exe -ArgumentList '--check-config' -PassThru -Wait
$p.ExitCode
```

結束代碼：`0` 成功、`1` 發生錯誤、`2` 程序自測未通過、`3` 本工具已在執行。

一般啟動失敗會顯示錯誤視窗；兩種檢查模式失敗時，會嘗試在 EXE 旁寫入 `Endfield-Direct-error.txt`。成功後不會清除先前的錯誤檔，請以這次的結束代碼為準。

## 如何運作

工具建立暫停的官方 `Games.exe`，將它指定為遊戲的父程序。完成 Job 隔離檢查後恢復遊戲，官方父程序則保持暫停，直到遊戲結束後清理。

不修改官方檔案，也不執行官方啟動器的正常介面流程。程序管理細節見 [實作說明](docs/implementation.md)。

## 開發與測試

在專案根目錄執行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

建置會先執行設定測試，再將 EXE、範例設定和 `SHA256SUMS` 放入 `dist/`，不會啟動遊戲。

若要測試程序清理，先準備 `dist/config.json`，再執行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -SelfTest
```

程序自測需要官方啟動器，以及該執行檔要求的權限。測試範圍見 [ACCEPTANCE.md](ACCEPTANCE.md)。

### 專案結構

| 路徑 | 內容 |
|---|---|
| `src/EndfieldDirect.cs` | 遊戲啟動、程序清理與診斷 |
| `src/AppConfig.cs` | 設定讀取與路徑檢查 |
| `src/app.manifest` | 沿用呼叫者權限的 `asInvoker` 設定 |
| `scripts/build.ps1` | 測試、編譯與 SHA-256 產出 |
| `tests/ConfigTests.cs` | 設定回歸測試 |

Git 不會追蹤 `config.json`、錯誤紀錄或編譯產生的檔案。

## 已知限制

- 遊戲更新可能改變啟動來源檢查，更新後的相容性需另行確認。
- 目前固定帶入 `-launcher_lang=zh-tw -launcher_sub_channel=6`，其他參數組合尚未測試。
- 官方啟動器依數值版本目錄選取，未在程式內驗證簽章。

## 授權

本專案採用 GNU 通用公眾授權條款第 3 版（GPL-3.0-only），詳見 [LICENSE](LICENSE)。
