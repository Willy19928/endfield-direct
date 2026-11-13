# 驗收紀錄

## 1.4.1

- EXE 檔案版本與 Manifest 版本均為 1.4.1.0。
- 17 項設定測試、設定檢查與程序自測均通過。

## 實際使用

- 已實測可正常啟動遊戲。

## 設定與建置

- 17 項設定回歸測試通過，涵蓋缺少設定、錯誤 JSON、欄位型別、未知欄位、檔案路徑、Unicode、空格及不同工作目錄。
- x64 C# 建置通過，使用 `/warn:4 /warnaserror+`。
- 成品內嵌 Manifest 為 `asInvoker`、`uiAccess=false`。

## 診斷與程序管理

- `--check-config` 通過，結束代碼為 0。
- `--self-test` 通過，結束代碼為 0；驗證關閉 Job 後父程序結束、暫停子程序仍存活。

## 重現方式

執行 `scripts/build.ps1` 跑設定測試並建置。準備 `dist/config.json` 後，執行 `--check-config` 檢查設定，或使用 `scripts/build.ps1 -SelfTest` 執行程序自測。

## 未涵蓋

- 其他啟動參數組合，以及遊戲更新後的相容性。
- 工具在啟動途中遭強制關閉的各種情況。
