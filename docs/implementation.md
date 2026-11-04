# 程序管理與設定

## 啟動流程

1. 取得 `Local\EndfieldDirect` Mutex，避免同一 Windows 工作階段重複執行。
2. 讀取 EXE 旁的 `config.json`，解析路徑並檢查目標檔案。
3. 尋找官方啟動器；一般啟動模式另檢查是否已有 `Endfield` 程序。
4. 建立設定 `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` 的未命名 Job。
5. 以 `CREATE_SUSPENDED` 建立官方 `Games.exe`。
6. 設定 `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS`，以 `STARTUPINFOEX` 建立暫停的遊戲程序。
7. 遊戲建立後，才將官方父程序加入清理 Job。
8. 用 `IsProcessInJob` 確認遊戲不屬於這個 Job，再恢復遊戲主執行緒。
9. 等待遊戲結束，清理官方父程序及控制代碼。

遊戲先建立、父程序後加入 Job，是為了避免遊戲在建立時繼承清理範圍。官方父程序主執行緒始終不恢復。

## 資源生命週期

`Child` 使用 `SafeHandle` 持有程序與執行緒控制代碼。初始化失敗時，仍暫停的自有程序會在釋放時嘗試終止；已恢復的遊戲不由該釋放邏輯主動終止。

`ResumeThread` 必須回報先前暫停計數為 1。屬性清單在 `finally` 中刪除並釋放記憶體。

Job 最後一個控制代碼關閉時，系統終止已加入其中的官方父程序。從父程序建立到加入 Job 之間仍有短暫時間窗；若工具在此期間遭強制終止，可能留下暫停程序。

## 設定讀取

`AppConfig` 使用 .NET Framework 內建的 `JavaScriptSerializer`，只接受 `gamePath` 和 `launcherPath`。遊戲路徑為必填字串；啟動器路徑可省略或留空。

相對路徑以 EXE 目錄解析，不依賴目前的工作目錄。拒絕 `C:foo` 等磁碟相對路徑及根相對路徑，避免位置解釋不一致。檔名必須分別為 `Endfield.exe`、`Games.exe`，且檔案必須存在。

自動尋找啟動器時，從 Program Files 下的 GRYPHLINK 啟動器目錄選取包含 `Games.exe` 的最高數值版本目錄；沒有合適版本目錄時使用根目錄候選。

## 權限與錯誤

Application Manifest 使用 `asInvoker`，沿用呼叫者權限。建立程序失敗時，錯誤包含目標路徑及 Windows 訊息；錯誤 740 額外提示以系統管理員身分執行。

一般模式使用錯誤視窗。診斷模式以結束代碼回報，並嘗試寫入 `Endfield-Direct-error.txt`；目錄不可寫時可能無法留下診斷檔。

## 測試設計

設定測試在獨立暫存目錄建立不執行的假檔案，驗證 JSON、欄位、Unicode 路徑、空格與工作目錄獨立性。

`--check-config` 檢查設定及目標檔案，不建立外部程序。

`--self-test` 以工具自身作為暫停子程序，不恢復執行緒。關閉 Job 後，確認官方父程序結束而子程序仍存在，最後清理子程序。此測試涵蓋 Job 清理，不驗證遊戲 SDK、實際 PPID 或登入結果。

## 實作限制

- Mutex 限同一工作階段；既有遊戲依程序名稱判斷。
- 不讀 SDK 紀錄，也不將遊戲結束代碼轉送給呼叫者。
- 不驗證官方檔案簽章，也不查詢官方啟用版本設定。
- 一般模式回傳 0 不代表遊戲沒有異常結束或已完成登入。
