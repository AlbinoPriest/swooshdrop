using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public class Preferences
{
    public string Language { get; set; }
    public string DeviceName { get; set; }
    public string SaveFolder { get; set; }
    public bool ReceiveOnLaunch { get; set; }
    public bool PauseAtSignIn { get; set; }
    public bool Notifications { get; set; }
    public string NotificationMode { get; set; }
    public bool OpenLinksOnReceive { get; set; }
    public string RuntimeDistro { get; set; }
    public string AdapterInstanceId { get; set; }

    public Preferences()
    {
        Language = SettingsPolicy.DefaultLanguage();
        DeviceName = "SwooshDrop";
        SaveFolder = SettingsPolicy.DefaultSaveFolder();
        ReceiveOnLaunch = true;
        Notifications = true;
        OpenLinksOnReceive = true;
        RuntimeDistro = "WinDropRuntime";
    }
}

public static class SettingsPolicy
{
    public const int MaxDeviceNameUtf8Bytes = 63;

    public static string DefaultLanguage()
    {
        string name = CultureInfo.CurrentUICulture.Name;
        return name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "en-US";
    }

    public static string NormalizeLanguage(string language)
    {
        return string.Equals(language, "zh-TW", StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "en-US";
    }

    public static string DefaultSaveFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SwooshDrop");
    }

    public static bool Normalize(Preferences settings)
    {
        bool changed = false;
        string language = string.IsNullOrWhiteSpace(settings.Language) ? DefaultLanguage() : NormalizeLanguage(settings.Language);
        if (!string.Equals(settings.Language, language, StringComparison.Ordinal)) { settings.Language = language; changed = true; }
        if (string.IsNullOrWhiteSpace(settings.DeviceName)) { settings.DeviceName = "SwooshDrop"; changed = true; }
        if (string.IsNullOrWhiteSpace(settings.SaveFolder)) { settings.SaveFolder = DefaultSaveFolder(); changed = true; }
        if (string.IsNullOrWhiteSpace(settings.RuntimeDistro)) { settings.RuntimeDistro = "WinDropRuntime"; changed = true; }
        return changed;
    }

    public static bool TryNormalizeDeviceName(string value, out string normalized, out string error)
    {
        normalized = (value ?? "").Trim();
        error = null;
        if (normalized.Length == 0) { error = "Device name cannot be empty."; return false; }
        foreach (char character in normalized)
            if (char.IsControl(character)) { error = "Device name cannot contain control characters."; return false; }
        if (Encoding.UTF8.GetByteCount(normalized) > MaxDeviceNameUtf8Bytes)
        {
            error = "Device name must be 63 UTF-8 bytes or fewer.";
            return false;
        }
        return true;
    }
}

public static class I18n
{
    static string language = "en-US";
    static readonly Dictionary<string, string> Zh = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Received", "已接收" },
        { "Settings", "設定" },
        { "About", "關於" },
        { "Choose SwooshDrop on your iPhone.", "請在 iPhone 上選擇 SwooshDrop。" },
        { "Choose {0} on your iPhone.", "請在 iPhone 上選擇 {0}。" },
        { "Choose {0} in your iPhone’s AirDrop menu.", "請在 iPhone 的 AirDrop 選單中選擇 {0}。" },
        { "Send it. Got it.", "傳送，即刻收到。" },
        { "A direct route from your iPhone to this PC.", "從 iPhone 直接傳到這台電腦。" },
        { "Make it yours.", "依你的方式設定。" },
        { "Choose how SwooshDrop runs and where files go.", "設定 SwooshDrop 的運作方式與檔案位置。" },
        { "About SwooshDrop.", "關於 SwooshDrop。" },
        { "Project information and licenses.", "專案資訊與授權。" },
        { "Open folder ↗", "開啟資料夾 ↗" },
        { "Ready when you are", "準備就緒" },
        { "Connect your adapter and start receiving.", "連接網卡後即可開始接收。" },
        { "Start receiving", "開始接收" },
        { "Stop receiving", "停止接收" },
        { "Working…", "處理中…" },
        { "RECENTLY LANDED", "最近接收" },
        { "Incoming AirDrop", "收到 AirDrop 請求" },
        { "Decline", "拒絕" },
        { "Accept", "接受" },
        { "The next drop lands here.", "下一個檔案會出現在這裡。" },
        { "Choose SwooshDrop in your iPhone’s AirDrop menu.", "請在 iPhone 的 AirDrop 選單中選擇 SwooshDrop。" },
        { "Open", "開啟" },
        { "Show in folder", "在資料夾中顯示" },
        { "General", "一般" },
        { "Display language", "介面語言" },
        { "AirDrop device name", "AirDrop 裝置名稱" },
        { "Shown in the AirDrop menu on nearby iPhones.", "顯示於附近 iPhone 的 AirDrop 選單。" },
        { "Save name", "儲存名稱" },
        { "Restore default", "還原預設" },
        { "Name changes take effect after stopping and starting receiving.", "名稱變更會在停止並重新開始接收後生效。" },
        { "Startup", "啟動" },
        { "Open SwooshDrop in the tray when I sign in", "登入 Windows 時在系統匣開啟 SwooshDrop" },
        { "Keep receiving paused at sign-in (uses less memory)", "登入時暫停接收（節省記憶體）" },
        { "Start receiving from the tray when needed. SwooshDrop won't appear in AirDrop while paused.", "需要時再從系統匣開始接收；暫停時不會出現在 AirDrop 中。" },
        { "Start receiving on a fresh manual launch", "手動啟動時自動開始接收" },
        { "Incoming transfers", "傳入項目" },
        { "Choose how you approve files and links sent from your iPhone.", "選擇如何核准從 iPhone 傳來的檔案與連結。" },
        { "Windows notifications", "Windows 通知" },
        { "Use the Windows notification center", "使用 Windows 通知中心" },
        { "SwooshDrop popup", "SwooshDrop 彈出視窗" },
        { "Approve in a matching app window", "在應用程式視窗中核准" },
        { "Open accepted web links in my browser", "使用瀏覽器開啟已接受的網頁連結" },
        { "Every transfer still needs your approval.", "每次傳輸仍需由你核准。" },
        { "Files & adapter", "檔案與網卡" },
        { "Save received files to", "接收檔案儲存位置" },
        { "Choose folder…", "選擇資料夾…" },
        { "Apply folder", "套用位置" },
        { "Open folder", "開啟資料夾" },
        { "Adapter setup…", "網卡設定…" },
        { "Folder changes take effect after stopping and starting receiving.", "位置變更會在停止並重新開始接收後生效。" },
        { "Diagnostics", "連線診斷" },
        { "Live receiver events, useful when troubleshooting.", "即時接收事件，可用於疑難排解。" },
        { "Activity", "活動" },
        { "Radio", "無線電" },
        { "Bridge", "橋接" },
        { "Copy", "複製" },
        { "Open logs ↗", "開啟日誌 ↗" },
        { "Waiting for activity…", "等待活動…" },
        { "Receive photos, files, and web links from the native AirDrop menu on your iPhone.", "從 iPhone 原生 AirDrop 選單接收照片、檔案與網頁連結。" },
        { "Unofficial community project", "非官方社群專案" },
        { "Developed independently by AlbinoPriest. Not affiliated with, sponsored by, or endorsed by Apple, Microsoft, or the upstream WinDrop and OWL maintainers.", "由 AlbinoPriest 獨立開發，與 Apple、Microsoft 或上游 WinDrop 與 OWL 維護者沒有從屬、贊助或背書關係。" },
        { "AirDrop protocol adapted from MIT-licensed WinDrop by Uvejs Gjelaj. AWDL provided by OWL under GPL-3.0. Original copyright and license notices are retained; see Licenses below.", "AirDrop 協定改編自 Uvejs Gjelaj 以 MIT 授權的 WinDrop；AWDL 由 GPL-3.0 授權的 OWL 提供。原始著作權與授權聲明均予保留，詳見下方授權。" },
        { "Hardware beta", "硬體測試版" },
        { "Uses a dedicated compatible USB adapter and a separate Linux runtime installed by SwooshDrop Setup. Discovery can take time. Experimental adapters and fresh Windows installations still need testing. Contacts Only is not supported.", "使用相容的專用 USB 網卡，以及由 SwooshDrop 安裝程式建立的獨立 Linux 執行環境。裝置探索可能需要一些時間；實驗性網卡與全新 Windows 安裝仍需測試。目前不支援「僅限聯絡人」。" },
        { "Open diagnostics", "開啟診斷" },
        { "Licenses", "授權" },
        { "Files keep their original quality. Closing this window keeps receiving in the tray.", "檔案會保留原始品質；關閉視窗後仍會在系統匣持續接收。" },
        { "LAST RECEIVED", "上次接收" },
        { "No transfers yet", "尚無傳輸紀錄" },
        { "Adapter", "網卡" },
        { "Runtime", "執行環境" },
        { "Checking…", "檢查中…" },
        { "Stopped", "已停止" },
        { "Open SwooshDrop", "開啟 SwooshDrop" },
        { "Received files", "接收的檔案" },
        { "Close", "關閉" },
        { "SWOOSHDROP / INCOMING", "SWOOSHDROP / 傳入" },
        { "Your iPhone wants to share", "你的 iPhone 想要分享" },
        { "Your iPhone", "你的 iPhone" },
        { "Only accept a transfer you expect.", "請只接受你預期收到的傳輸。" },
        { "WEB LINK", "網頁連結" },
        { "PHOTO", "照片" },
        { "FILE", "檔案" },
        { "Open received folder", "開啟接收資料夾" },
        { "Quit", "結束" },
        { "Receiving paused", "接收已暫停" },
        { "Started with Windows without waking WSL. Press Start receiving when you want to AirDrop.", "已隨 Windows 啟動但未喚醒 WSL；需要 AirDrop 時請按「開始接收」。" },
        { "Getting ready…", "正在準備…" },
        { "Connecting your dedicated adapter.", "正在連接專用 Wi-Fi 網卡。" },
        { "Connect your adapter", "請連接網卡" },
        { "Couldn't start receiving", "無法開始接收" },
        { "Plug in your selected USB adapter. SwooshDrop will try again.", "請插入所選的 USB Wi-Fi 網卡，SwooshDrop 會再次嘗試。" },
        { "Open Settings → Adapter setup to check this PC, or About → Open diagnostics.", "請開啟「設定 → 網卡設定」檢查電腦，或前往「關於 → 開啟診斷」。" },
        { "Stopping…", "正在停止…" },
        { "Returning the adapter to Windows.", "正在將網卡交還 Windows。" },
        { "Receiving is off", "接收已關閉" },
        { "Start receiving whenever you're ready.", "準備好時即可開始接收。" },
        { "Still stopping", "仍在停止中" },
        { "Please try Stop again.", "請再按一次停止。" },
        { "Ready to receive", "等待接收" },
        { "Discovery may take a moment.", "裝置探索可能需要一點時間。" },
        { "Connection paused", "連線已暫停" },
        { "Click Start to reconnect, or wait for an automatic retry.", "按下開始以重新連線，或等待自動重試。" },
        { "Receiving your drop…", "正在接收…" },
        { "Saving the original files to your received folder.", "正在將原始檔案儲存至接收資料夾。" },
        { "Receiving from {0}", "正在接收來自 {0} 的檔案" },
        { "Preparing transfer…", "正在準備傳輸…" },
        { "File {0} of {1} · {2}% · {3} / {4}", "第 {0}/{1} 個檔案 · {2}% · {3} / {4}" },
        { "Transfer complete", "傳輸完成" },
        { "Ready for your next drop", "等待下一次傳輸" },
        { "Link received", "已收到連結" },
        { "AirDrop received", "已收到 AirDrop" },
        { "Link saved", "連結已儲存" },
        { "Windows couldn't open your browser. Use Open in the gallery to try again.", "Windows 無法開啟瀏覽器，請在紀錄中按「開啟」重試。" },
        { "No events recorded yet.", "尚未記錄任何事件。" },
        { "This file was moved or deleted. You can still see its transfer details here.", "此檔案已移動或刪除，但仍可在這裡查看傳輸資訊。" },
        { "Windows couldn't open this item.", "Windows 無法開啟此項目。" },
        { "Device name cannot be empty.", "裝置名稱不可空白。" },
        { "Device name cannot contain control characters.", "裝置名稱不可包含控制字元。" },
        { "Device name must be 63 UTF-8 bytes or fewer.", "裝置名稱不可超過 63 個 UTF-8 位元組。" },
        { "Device name saved.", "裝置名稱已儲存。" },
        { "Restart receiving to advertise the new name.", "請停止並重新開始接收，以廣播新名稱。" },
        { "Save folder updated.", "儲存位置已更新。" },
        { "New transfers will use this folder after receiving restarts.", "重新開始接收後，新傳輸會使用此資料夾。" },
        { "Restart receiving now?", "要立即重新啟用接收嗎？" },
        { "Applying this change now will stop any transfer currently in progress. Choose No to apply it the next time receiving starts.", "立即套用這項變更會中止目前進行中的傳輸。選擇「否」可在下次開始接收時套用。" },
        { "Change saved. Restart receiving to apply it.", "變更已儲存；請重新啟用接收以套用。" },
        { "The new setting will be used the next time receiving starts.", "新設定會在下次開始接收時套用。" },
        { "Receiving restarted. The new setting is active.", "接收已重新啟用，新設定現已生效。" },
        { "Could not restart receiving. Use the main Start button to try again.", "無法重新啟用接收，請使用主畫面的「開始接收」重試。" },
        { "The selected folder is not available.", "所選資料夾目前無法使用。" },
        { "SwooshDrop needs a local drive folder that WSL2 can access.", "SwooshDrop 需要 WSL2 可存取的本機磁碟資料夾。" },
        { "SwooshDrop cannot write to this folder.", "SwooshDrop 無法寫入此資料夾。" },
        { "Do you want SwooshDrop to create it?", "要讓 SwooshDrop 建立此資料夾嗎？" },
        { "Folder does not exist", "資料夾不存在" },
        { "AirDrop device", "AirDrop 裝置" },
        { "Folder", "資料夾" },
        { "Language", "語言" },
        { "Choose where SwooshDrop saves received files", "選擇 SwooshDrop 接收檔案的儲存位置" },
        { "Connected", "已連線" },
        { "Not selected", "尚未選擇" },
        { "Released to Windows", "已交還 Windows" },
        { "Starting…", "正在啟動…" },
        { "WSL2 running", "WSL2 執行中" },
        { "{0} item", "{0} 個項目" },
        { "{0} items", "{0} 個項目" },
        { "{0} received item", "已接收 {0} 個項目" },
        { "{0} received items", "已接收 {0} 個項目" },
        { "On iPhone: Share → AirDrop → {0}. Discovery may take a moment.", "在 iPhone 上選擇：分享 → AirDrop → {0}。裝置探索可能需要一點時間。" },
        { "On iPhone: Share → AirDrop → {0}.", "在 iPhone 上選擇：分享 → AirDrop → {0}。" },
        { "{0} wants to share {1}", "{0} 想要分享{1}" },
        { "a link", "一個連結" },
        { "{0} file(s)", " {0} 個檔案" },
        { "Accept or decline the transfer from {0}.", "請接受或拒絕來自 {0} 的傳輸。" },
        { "AirDrop from {0}", "來自 {0} 的 AirDrop" },
        { "{0} wants to share", "{0} 想要分享" },
        { "Web link", "網頁連結" },
        { "{0} web links", "{0} 個網頁連結" },
        { "Saved to {0}", "已儲存至 {0}" },
        { "Windows could not save the startup setting.", "Windows 無法儲存啟動設定。" },
        { "Download and run SwooshDropSetup.exe to set up this PC or change its adapter.", "請下載並執行 SwooshDropSetup.exe，以設定這台電腦或更換網卡。" }
    };

    static readonly Dictionary<string, string> EnglishByZh = BuildReverse();

    static Dictionary<string, string> BuildReverse()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Zh) if (!result.ContainsKey(entry.Value)) result.Add(entry.Value, entry.Key);
        return result;
    }

    public static string Language { get { return language; } }
    public static CultureInfo Culture { get { return language == "zh-TW" ? CultureInfo.GetCultureInfo("zh-TW") : CultureInfo.GetCultureInfo("en-US"); } }

    public static void SetLanguage(string value) { language = SettingsPolicy.NormalizeLanguage(value); }

    public static string Text(string english)
    {
        if (english == null) return null;
        string source;
        if (EnglishByZh.TryGetValue(english, out source)) english = source;
        if (language == "zh-TW") { string translated; if (Zh.TryGetValue(english, out translated)) return translated; }
        return english;
    }

    public static string Format(string english, params object[] args)
    {
        return string.Format(Culture, Text(english), args);
    }
}
