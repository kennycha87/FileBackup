# File Backup

A Windows desktop file backup application built with .NET 8 WPF (MVVM). Supports full and differential backups with scheduling, restore, and system tray operation.

一個使用 .NET 8 WPF (MVVM) 建構的 Windows 桌面檔案備份應用程式。支援完整備份與差異備份，具備排程、還原及系統匣常駐功能。

---

## How to use / 如何使用
Download this repository, and then go to <your directory>\src\klst-backup\bin, then dobule-click `klst-backup.exe` to start the application

下載本項目到你的電腦, 打開 <你的路徑>\src\klst-backup\bin 並點擊`klst-backup.exe`運行

## Features / 功能

- **Backup jobs / 備份工作** — configure any source and destination folder, name the job, and enable or disable it. / 設定任意來源與目的地資料夾，命名工作並啟用或停用。
- **Flexible schedules / 彈性排程** — Daily, Weekly (pick a weekday), or Monthly (pick a day of the month), each at a configurable time (HH:mm). Jobs run automatically via the built-in scheduler while the app is running. / 每日、每週（選擇星期幾）或每月（選擇日期），每個排程可設定執行時間（HH:mm）。應用程式運行期間，工作會透過內建排程器自動執行。
- **Full and differential backups / 完整與差異備份**
  - **Full / 完整備份** — copies every file from the source directly to the destination. / 將來源的所有檔案直接複製到目的地。
  - **Differential / 差異備份** — copies only files that are new or changed (by size or last-write time) since the last full backup, saving time and bandwidth. If no full backup exists yet, the first run is automatically promoted to a full backup. / 僅複製自上次完整備份以來新增或變更的檔案（依檔案大小或最後寫入時間判斷），節省時間與頻寬。若尚無完整備份，首次執行會自動提升為完整備份。
- **Restore / 還原** — pick a job, a backup set, and a target folder to restore files. / 選擇工作、備份集與目標資料夾即可還原檔案。
- **Run history / 執行紀錄** — every run is recorded (type, status, file count, bytes, duration) and displayed in the History tab. / 每次執行都會記錄（類型、狀態、檔案數、位元組數、耗時），並顯示於「紀錄」分頁。
- **Per-run logs / 每次執行日誌** — written to `%APPDATA%\FileBackup\logs\<yyyyMM>\*.log`. / 寫入至 `%APPDATA%\FileBackup\logs\<yyyyMM>\*.log`。
- **System tray / 系統匣** — closing the window minimizes to the tray; backups continue running in the background. Tray menu: Open / Exit. / 關閉視窗時最小化至系統匣；備份在背景持續運行。系統匣選單：開啟 / 結束。
- **Start with Windows / 開機自動啟動** — optional per-user auto-start (Settings tab) so schedules survive reboots. / 可選的使用者層級自動啟動（設定分頁），讓排程在重新開機後繼續生效。

---

## How It Works / 運作方式

### Backup file layout / 備份檔案佈局

Backup files are written **directly** to the destination path you specify — no extra subfolders are created. The destination contains an up-to-date mirror of your source files.

備份檔案**直接**寫入您指定的目的地路徑 — 不會建立額外的子資料夾。目的地包含來源檔案的最新鏡像。

```
<your-destination / 您的目的地>\
  file1.txt          <- backed up files, preserving source directory structure / 備份的檔案，保留來源的目錄結構
  subfolder\
    file2.txt
  ...
```

### Manifest and backup set tracking / Manifest 與備份集追蹤

Metadata (manifests and backup set history) is stored separately in AppData, keeping your destination clean:

中繼資料（manifest 與備份集歷史）分開存放在 AppData 中，保持目的地整潔：

```
%APPDATA%\FileBackup\sets\<JobId / 工作ID>\
  Full_20260910_020000.json   <- manifest for a full backup / 完整備份的 manifest
  Diff_20260910_140000.json   <- manifest for a differential backup / 差異備份的 manifest
  Diff_20260911_140000.json
  ...
```

Each manifest records the relative path, size, and last-write time of every file captured in that run.

每個 manifest 記錄該次執行所擷取之每個檔案的相對路徑、大小與最後寫入時間。

### Differential backup logic / 差異備份邏輯

1. The engine loads the manifest of the most recent `Full_*` set from AppData. / 引擎從 AppData 載入最近的 `Full_*` 集的 manifest。
2. It compares every source file against the baseline (by size and last-write time). / 將每個來源檔案與基準進行比對（依大小與最後寫入時間）。
3. Only new or changed files are copied to the destination. / 僅將新增或變更的檔案複製到目的地。
4. A new manifest is saved to AppData recording the current state. / 新的 manifest 儲存至 AppData，記錄當前狀態。

Deleted source files are not removed from the destination (safe by design).

已刪除的來源檔案不會從目的地移除（安全設計）。

---

## Build and Run / 建置與執行

### Prerequisites / 必要條件

- **.NET 8 SDK** — install via [winget](https://learn.microsoft.com/en-us/windows/package-manager/winget/): / 可透過 [winget](https://learn.microsoft.com/zh-tw/windows/package-manager/winget/) 安裝：
  ```
  winget install Microsoft.DotNet.SDK.8
  ```
- **Windows** — required for WPF and system tray features. / WPF 與系統匣功能需要 Windows 作業系統。

### Commands / 指令

```powershell
# Build / 建置
dotnet build klst-backup.sln -c Release

# Run / 執行
dotnet run --project src\klst-backup

# Test / 測試
dotnet test tests\klst-backup.Tests
```

The compiled executable is at `src\klst-backup\bin\Release\net8.0-windows\klst-backup.exe`.

編譯後的執行檔位於 `src\klst-backup\bin\Release\net8.0-windows\klst-backup.exe`。

---

## Project Structure / 專案結構

```
src\klst-backup\
  Models\         Data models: BackupJob, BackupManifest, FileEntry, results, enums
                  資料模型：BackupJob、BackupManifest、FileEntry、結果物件、列舉
  Services\       BackupEngine, ManifestStore, SchedulerService, SchedulerMath,
                  ConfigService, LogService, StartupService, AppIcon
  ViewModels\     MainViewModel, JobItemViewModel, JobEditViewModel, converters
                  MainViewModel、JobItemViewModel、JobEditViewModel、轉換器
  Views\          MainWindow (Jobs / History / Restore / Settings tabs), JobEditDialog
                  MainWindow（工作 / 紀錄 / 還原 / 設定分頁）、JobEditDialog
  App.xaml        Application entry point and resources / 應用程式進入點與資源

tests\klst-backup.Tests\
  BackupEngineTests.cs     Full, differential, restore, and cancellation tests
                           完整、差異、還原與取消測試
  ManifestStoreTests.cs    Manifest save/load and set enumeration tests
                           Manifest 存取與備份集列舉測試
  SchedulerMathTests.cs    Schedule calculation tests / 排程計算測試
```

---

## Notes and Limitations / 注意事項與限制

- **No compression or encryption / 無壓縮或加密** — backup sets are plain file copies for simplicity and easy manual access. / 備份集為純檔案複製，設計簡單且可手動存取。
- **Locked or unauthorized files / 鎖定或未經授權的檔案** are skipped with a warning (visible in the run log and history). / 會跳過並記錄警告（可見於執行日誌與紀錄）。
- **Long paths / 長路徑** (> 240 characters / 超過 240 字元) are handled automatically via the Windows extended path prefix (`\\?\`). / 會自動透過 Windows 擴充路徑前綴（`\\?\`）處理。
- **In-app scheduler / 應用程式內排程器** — backups only run while the application is running. This is by design; the app does not register Windows Task Scheduler entries. / 備份僅在應用程式運行期間執行。此為設計選擇，應用程式不會註冊 Windows 工作排程器項目。
- **Restore / 還原** copies the current state of the destination to the target folder. Point-in-time restore to a specific historical backup set is not supported. / 會將目的地的當前狀態複製到目標資料夾。不支援還原至特定歷史備份集的時間點還原。

---

## License / 授權

MIT
