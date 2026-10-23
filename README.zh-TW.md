# SwooshDrop 繁體中文貢獻分支

這個分支以官方 SwooshDrop `v0.3.6` 為基礎，保留原始品牌、圖示、內部識別與 AirDrop 接收架構，加入可供上游審查的通用功能：

- `zh-TW`／`en-US` 介面切換，缺少翻譯時回退英文。
- 可自訂 iPhone 原生 AirDrop 清單中顯示的接收端名稱。
- 可輸入、瀏覽、驗證、開啟及還原接收檔案資料夾。
- 主頁即時顯示傳送者、目前檔名、檔案序號、百分比與已接收容量。
- Windows 通知與應用程式核准視窗使用目前選定的語言。
- 舊版 `settings.json` 會保留原有網卡、WSL 與使用者偏好，再補入安全預設值。

## 安全與相容性

裝置名稱會以獨立程序參數傳給 WSL 接收服務，不進行 Shell 字串拼接；空白名稱、控制字元及超過 63 UTF-8 位元組的名稱會被拒絕。`ざこ～♡` 已納入單元測試。

接收資料夾目前限 WSL2 可透過 `/mnt/<drive>` 存取的本機磁碟路徑。接收服務運作中若變更裝置名稱或資料夾，應用程式會先儲存設定，再詢問是否立即重新啟用接收；提示會明確警告目前進行中的傳輸將被中止。選擇「否」則不中斷接收，變更在下次開始接收時生效。

進度百分比以目前正在解壓並寫入的單一檔案為基準。AirDrop 的串流封包不會事先提供整批壓縮內容的總長度，因此多檔傳輸另外顯示「第幾個／共幾個」，不虛構整批百分比。

## 建置與測試

```powershell
.\Test-AppFeatures.ps1
.\Test-WinDropSetup.ps1
.\Build-WinDrop.ps1 -BackendDirectory '<包含 windrop.dll 的目錄>'
```

精簡安裝器需要準備 runtime payload；corresponding-source archive 會獨立放在同一個 GitHub Release，不再嵌入 EXE。請參閱 [`docs/DISTRIBUTION.md`](docs/DISTRIBUTION.md)。

## 尚未承諾的功能

- iPhone AirDrop 清單中的自訂圖片或頭像。
- Windows 傳送檔案到 iPhone。
- 非本機路徑、UNC 或網路磁碟作為接收位置。

上述項目需要獨立的協定與硬體驗證，不會以未驗證的 UI 選項呈現。
