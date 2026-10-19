# 裝置名稱、語言與儲存位置

## 設定遷移

應用程式沿用 `Data\settings.json`。新增欄位如下：

```json
{
  "Language": "zh-TW",
  "DeviceName": "SwooshDrop"
}
```

讀取舊設定時只補入不存在或空白的欄位；`AdapterInstanceId`、`RuntimeDistro`、通知模式、啟動偏好及既有 `SaveFolder` 均予保留。

## AirDrop 裝置名稱資料流

1. 設定頁以 UTF-8 位元組長度與控制字元規則驗證名稱。
2. `settings.json` 儲存驗證後的 `DeviceName`。
3. `WinDropTray` 透過 `ProcessStartInfo` 的獨立參數把名稱交給 `airdrop-lab.sh`。
4. Bash 腳本再次檢查名稱，再以 `--name "$device_name"` 傳給 `windrop.dll`。

所有邊界均使用參數化與雙引號，不把使用者輸入插入 Shell 指令字串。

## 儲存位置

設定頁支援直接輸入、資料夾選擇器、開啟目前資料夾與還原預設。套用時會檢查：

- 絕對本機路徑；
- 磁碟目前可用；
- 目錄存在，或經使用者同意後建立；
- 實際建立暫存檔以驗證寫入權限。

目前明確拒絕 UNC 路徑，因接收服務依賴 WSL `wslpath` 與 `/mnt/<drive>`；在沒有端到端測試前不宣稱支援網路磁碟。

## 在地化

`I18n` 提供 `zh-TW` 與 `en-US`，未知語言回退 `en-US`。靜態 XAML 文字在載入後轉換，動態狀態、通知、核准按鈕、數量及日期由相同服務產生。語言切換後會重新整理目前頁面、傳輸清單與系統匣介面。
