using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("SwooshDrop")]
[assembly: AssemblyDescription("Receive AirDrop photos, files, and web links on Windows")]
[assembly: AssemblyProduct("SwooshDrop")]
[assembly: AssemblyVersion("0.3.6.0")]
[assembly: AssemblyFileVersion("0.3.6.0")]

public class Preferences
{
    public string SaveFolder { get; set; }
    public bool ReceiveOnLaunch { get; set; }
    public bool Notifications { get; set; }
    public string NotificationMode { get; set; }
    public bool OpenLinksOnReceive { get; set; }
    public string RuntimeDistro { get; set; }
    public string AdapterInstanceId { get; set; }
    public Preferences() { SaveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SwooshDrop"); ReceiveOnLaunch = true; Notifications = true; OpenLinksOnReceive = true; RuntimeDistro = "WinDropRuntime"; }
}

public class TransferItem : INotifyPropertyChanged
{
    public string Id { get; set; }
    public string Path { get; set; }
    public string Name { get; set; }
    public string Sender { get; set; }
    public string Link { get; set; }
    public long Bytes { get; set; }
    public DateTime ReceivedAt { get; set; }
    string preview;
    public string PreviewPath { get { return preview; } set { preview = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("PreviewPath")); } }
    public string Kind { get { return !string.IsNullOrEmpty(Link) ? "WEB LINK" : IsPhoto(Path) ? "PHOTO" : "FILE"; } }
    public string Glyph { get { return Kind == "WEB LINK" ? "↗" : Kind == "PHOTO" ? "▧" : "≡"; } }
    public string Detail { get { return ReceivedAt.ToLocalTime().ToString("MMM d · HH:mm") + "  ·  " + (Bytes >= 1048576 ? (Bytes / 1048576.0).ToString("0.0") + " MB" : (Bytes / 1024.0).ToString("0") + " KB"); } }
    public event PropertyChangedEventHandler PropertyChanged;
    public static bool IsPhoto(string path) { string ext = System.IO.Path.GetExtension(path ?? "").ToLowerInvariant(); return Array.IndexOf(new string[] { ".heic", ".heif", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" }, ext) >= 0; }
}

public sealed class WinDropTray
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    const string PipeName = "WinDrop.PC.Activation.v2";
    readonly string exe = Assembly.GetExecutingAssembly().Location;
    readonly string data;
    readonly string runtime;
    readonly string usb = @"C:\Program Files\usbipd-win\usbipd.exe";
    readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 2097152 };
    readonly ObservableCollection<TransferItem> items = new ObservableCollection<TransferItem>();
    readonly Window window;
    readonly Forms.NotifyIcon tray = new Forms.NotifyIcon();
    readonly DispatcherTimer retry = new DispatcherTimer();
    readonly DispatcherTimer expiry = new DispatcherTimer();
    readonly DispatcherTimer trayClickDelay = new DispatcherTimer();
    readonly DispatcherTimer logRefresh = new DispatcherTimer();
    Preferences settings;
    Process receiver;
    string control, bus, token, sessionDevice;
    bool ownsSession, busy, quitting, receivingIntent, loadingSettings, rendering;
    Dictionary<string, object> incoming;
    DateTime deadline;
    string incomingPreview, lastSender;
    Window popup;
    Window flyout;
    DateTime ignoreTrayClicksUntil;

    [STAThread]
    public static void Main(string[] args)
    {
        if (Array.IndexOf(args, "--toast-server") >= 0) { RunToastServer(); return; }
        bool render = Array.IndexOf(args, "--render") >= 0 || Array.IndexOf(args, "--render-popup") >= 0 || Array.IndexOf(args, "--render-flyout") >= 0;
        bool created;
        using (var mutex = new Mutex(true, render ? "Local\\WinDrop.PC.Render" : "Local\\WinDrop.PC.Instance", out created))
        {
            if (!created) { ForwardActivation(args.Length > 0 ? (args[0] == "--quit" ? "windrop://quit" : args[0]) : "windrop://show"); return; }
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            string dataOverride = Option(args, "--data-dir");
            var app = new WinDropTray(dataOverride, render);
            if (render)
            {
                if (Array.IndexOf(args, "--render-flyout") >= 0)
                {
                    app.OpenTrayFlyout(); app.Render(app.flyout, Option(args, "--render-flyout"));
                }
                else if (Array.IndexOf(args, "--render-popup") >= 0)
                {
                    app.incoming = new Dictionary<string, object> { { "id", Guid.NewGuid().ToString("N") }, { "sender", "Your iPhone" }, { "files", new string[] { "Weekend photo.heic" } }, { "links", new string[0] } };
                    app.incomingPreview = Option(args, "--preview"); app.deadline = DateTime.UtcNow.AddSeconds(60);
                    app.OpenPopup(); app.Render(app.popup, Option(args, "--render-popup"));
                }
                else
                {
                    string page = Option(args, "--render-page");
                    if (page == "Received" || page == "Settings" || page == "About") app.Page(page);
                    app.window.Show(); app.Render(app.window, Option(args, "--render"));
                }
                app.quitting = true; application.Shutdown(); return;
            }
            app.ExtractRuntime();
            try { NativeIntegration.Register(app.exe); } catch (Exception ex) { app.Log("Windows registration: " + ex.Message); }
            app.ListenForActivation();
            ToastActivation.OnActivated = delegate(string activation) { app.OnUi(delegate { app.Activate(activation); }); };
            int toastCookie = ToastActivation.RegisterServer();
            string import = Option(args, "--import"); if (!string.IsNullOrEmpty(import)) app.ImportFolder(import);
            app.QueueMissingPreviews();
            bool background = Array.IndexOf(args, "--background") >= 0;
            if (!background) app.window.Show();
            app.window.Dispatcher.BeginInvoke(new Action(async delegate
            {
                if (settingsStart(app, args)) { app.receivingIntent = true; await app.StartReceiving(); }
                if (args.Length > 0 && args[0].StartsWith("windrop://", StringComparison.OrdinalIgnoreCase)) app.Activate(args[0]);
            }), DispatcherPriority.ApplicationIdle);
            try { application.Run(); } finally { ToastActivation.UnregisterServer(toastCookie); }
        }
    }

    static bool settingsStart(WinDropTray app, string[] args) { return app.settings.ReceiveOnLaunch || Array.IndexOf(args, "--start") >= 0; }
    static string Option(string[] args, string name) { int at = Array.IndexOf(args, name); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
    T Find<T>(string name) where T : class { return window.FindName(name) as T; }

    WinDropTray(string dataOverride, bool render)
    {
        rendering = render;
        data = dataOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinDrop", "Data");
        runtime = Path.Combine(data, "Runtime"); Directory.CreateDirectory(data); Directory.CreateDirectory(Path.Combine(data, "Previews"));
        settings = Read<Preferences>("settings.json") ?? new Preferences();
        window = (Window)XamlReader.Parse(ResourceText("MainWindow.xaml"));
        window.SourceInitialized += delegate
        {
            try
            {
                int enabled = 1;
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
            }
            catch (Exception ex) { Log("Dark title bar: " + ex.Message); }
        };
        try { window.Icon = Bitmap(ResourceBytes("AppIcon.png")); } catch { }
        var saved = Read<List<TransferItem>>("history.json");
        if (saved != null) foreach (var item in saved) { if (items.Count >= 500) break; items.Add(item); }
        Find<ItemsControl>("Transfers").ItemsSource = items;
        UpdateCount();
        Find<Button>("ReceivedNav").Click += delegate { Page("Received"); };
        Find<Button>("SettingsNav").Click += delegate { Page("Settings"); };
        Find<Button>("AboutNav").Click += delegate { Page("About"); };
        Find<Button>("FolderButton").Click += delegate { OpenFolder(); };
        Find<Button>("ReceiveButton").Click += async delegate { if (busy) return; if (ownsSession) { receivingIntent = false; await StopReceiving(); } else { receivingIntent = true; await StartReceiving(); } };
        Find<Button>("BannerAccept").Click += delegate { Decide(true); };
        Find<Button>("BannerDecline").Click += delegate { Decide(false); };
        Find<Button>("DiagnosticsButton").Click += delegate { Directory.CreateDirectory(Path.Combine(data, "Logs")); Process.Start("explorer.exe", Quote(Path.Combine(data, "Logs"))); };
        Find<Button>("SetupButton").Click += async delegate
        {
            string setup = Path.Combine(Path.GetDirectoryName(exe), "SwooshDropSetup.exe");
            if (!File.Exists(setup)) { MessageBox.Show("Download and run SwooshDropSetup.exe to set up this PC or change its adapter.", "SwooshDrop Setup"); return; }
            receivingIntent = false; await StopReceiving(); if (ownsSession) return;
            Process.Start(setup); quitting = true; tray.Dispose(); Application.Current.Shutdown();
        };
        Find<Button>("LicensesButton").Click += delegate { MessageBox.Show(ResourceText("App-LICENSE.txt") + "\n\nWinDrop protocol dependency\n\n" + ResourceText("Protocol-LICENSE.txt"), "SwooshDrop licenses"); };
        Find<ItemsControl>("Transfers").AddHandler(Button.ClickEvent, new RoutedEventHandler(OpenItem));
        loadingSettings = true;
        Find<CheckBox>("AutostartCheck").IsChecked = NativeIntegration.IsAutostartEnabled();
        Find<CheckBox>("ReceiveOnLaunchCheck").IsChecked = settings.ReceiveOnLaunch;
        Find<RadioButton>("WindowsNotificationRadio").IsChecked = NotificationMode == "windows";
        Find<RadioButton>("AppPopupRadio").IsChecked = NotificationMode == "app";
        Find<CheckBox>("OpenLinksCheck").IsChecked = settings.OpenLinksOnReceive;
        Find<TextBlock>("SaveFolderText").Text = settings.SaveFolder;
        loadingSettings = false;
        Find<CheckBox>("AutostartCheck").Click += delegate
        {
            if (loadingSettings) return;
            try { NativeIntegration.SetAutostart(exe, Find<CheckBox>("AutostartCheck").IsChecked == true); }
            catch (Exception ex) { Log("Autostart: " + ex.Message); Find<CheckBox>("AutostartCheck").IsChecked = NativeIntegration.IsAutostartEnabled(); MessageBox.Show("Windows could not save the startup setting.", "SwooshDrop"); }
        };
        Find<CheckBox>("ReceiveOnLaunchCheck").Click += delegate { settings.ReceiveOnLaunch = Find<CheckBox>("ReceiveOnLaunchCheck").IsChecked == true; SaveSettings(); };
        Find<RadioButton>("WindowsNotificationRadio").Click += delegate { SetNotificationMode("windows"); };
        Find<RadioButton>("AppPopupRadio").Click += delegate { SetNotificationMode("app"); };
        Find<CheckBox>("OpenLinksCheck").Click += delegate { settings.OpenLinksOnReceive = Find<CheckBox>("OpenLinksCheck").IsChecked == true; SaveSettings(); };
        Find<Button>("ChangeFolderButton").Click += delegate
        {
            using (var picker = new Forms.FolderBrowserDialog { Description = "Choose where SwooshDrop saves received files", SelectedPath = settings.SaveFolder })
                if (picker.ShowDialog() == Forms.DialogResult.OK) { settings.SaveFolder = picker.SelectedPath; Find<TextBlock>("SaveFolderText").Text = settings.SaveFolder; SaveSettings(); }
        };
        Find<Button>("CopyLogsButton").Click += delegate { try { Clipboard.SetText(Find<TextBox>("LogsTextBox").Text ?? ""); } catch (Exception ex) { Log("Copy logs: " + ex.Message); } };
        Find<Button>("OpenLogsButton").Click += delegate { string folder = Path.GetDirectoryName(SelectedLogPath()); Directory.CreateDirectory(folder); Process.Start("explorer.exe", Quote(folder)); };
        Find<ComboBox>("LogSourceCombo").SelectionChanged += delegate { RefreshLogView(); };
        logRefresh.Interval = TimeSpan.FromSeconds(2);
        logRefresh.Tick += delegate { if (Find<UIElement>("SettingsPage").Visibility == Visibility.Visible) RefreshLogView(); };
        if (!render) logRefresh.Start();
        window.Closing += delegate(object sender, CancelEventArgs e) { if (!quitting) { e.Cancel = true; window.Hide(); } };
        if (!render)
        {
            tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exe); tray.Text = "SwooshDrop"; tray.Visible = true;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open SwooshDrop", null, delegate { ShowWindow(); });
            menu.Items.Add("Open received folder", null, delegate { OpenFolder(); });
            menu.Items.Add("Start receiving", null, async delegate { receivingIntent = true; await StartReceiving(); });
            menu.Items.Add("Stop receiving", null, async delegate { receivingIntent = false; await StopReceiving(); });
            menu.Items.Add("Quit", null, async delegate { if (busy) return; receivingIntent = false; await StopReceiving(); if (ownsSession) return; quitting = true; tray.Dispose(); Application.Current.Shutdown(); });
            tray.ContextMenuStrip = menu;
            trayClickDelay.Interval = TimeSpan.FromMilliseconds(240);
            trayClickDelay.Tick += delegate { trayClickDelay.Stop(); ToggleTrayFlyout(); };
            tray.MouseClick += delegate(object sender, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left) OnUi(delegate { if (DateTime.UtcNow < ignoreTrayClicksUntil) return; trayClickDelay.Stop(); trayClickDelay.Start(); }); };
            tray.MouseDoubleClick += delegate(object sender, Forms.MouseEventArgs e) { if (e.Button == Forms.MouseButtons.Left) OnUi(delegate { ignoreTrayClicksUntil = DateTime.UtcNow.AddMilliseconds(400); trayClickDelay.Stop(); HideTrayFlyout(); ShowWindow(); }); };
            retry.Interval = TimeSpan.FromSeconds(20); retry.Tick += async delegate { if (receivingIntent && !busy && !ownsSession) await StartReceiving(); }; retry.Start();
            expiry.Interval = TimeSpan.FromSeconds(1); expiry.Tick += delegate { if (incoming == null) return; int remaining = Math.Max(0, (int)(deadline - DateTime.UtcNow).TotalSeconds); if (popup != null) ((TextBlock)popup.FindName("CountdownText")).Text = remaining + "s"; if (remaining == 0) Decide(false); }; expiry.Start();
        }
    }

    void Page(string name)
    {
        foreach (string page in new string[] { "Received", "Settings", "About" })
        {
            ((UIElement)window.FindName(page + "Page")).Visibility = page == name ? Visibility.Visible : Visibility.Collapsed;
            var nav = Find<Button>(page + "Nav");
            nav.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(page == name ? "#4A3834" : "#2E2927"));
            nav.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(page == name ? "#F6F0E9" : "#D5CBC4"));
            nav.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EE725D"));
            nav.BorderThickness = page == name ? new Thickness(3, 0, 0, 0) : new Thickness(0);
        }
        var title = Find<TextBlock>("PageTitleText");
        var subtitle = Find<TextBlock>("PageSubtitleText");
        if (title != null) title.Text = name == "Received" ? "Send it. Got it." : name == "Settings" ? "Make it yours." : "About SwooshDrop.";
        if (subtitle != null) subtitle.Text = name == "Received" ? "A direct route from your iPhone to this PC." : name == "Settings" ? "Choose how SwooshDrop runs and where files go." : "Project information and licenses.";
        if (name == "Settings") RefreshLogView();
    }
    void ShowWindow() { HideTrayFlyout(); window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
    void OpenFolder() { HideTrayFlyout(); Directory.CreateDirectory(settings.SaveFolder); Process.Start("explorer.exe", Quote(settings.SaveFolder)); }
    T FindFlyout<T>(string name) where T : class { return flyout == null ? null : flyout.FindName(name) as T; }
    void ToggleTrayFlyout()
    {
        try { if (flyout != null && flyout.IsVisible) HideTrayFlyout(); else OpenTrayFlyout(); }
        catch (Exception ex) { Log("Tray flyout: " + ex.Message); ShowWindow(); }
    }
    void HideTrayFlyout() { if (flyout != null && flyout.IsVisible) flyout.Hide(); }
    void OpenTrayFlyout()
    {
        if (flyout == null)
        {
            flyout = (Window)XamlReader.Parse(ResourceText("TrayFlyout.xaml"));
            flyout.Deactivated += delegate { if (!rendering) HideTrayFlyout(); };
            FindFlyout<Button>("FlyoutOpenButton").Click += delegate { ShowWindow(); };
            FindFlyout<Button>("FlyoutFolderButton").Click += delegate { OpenFolder(); };
            FindFlyout<Button>("FlyoutSettingsButton").Click += delegate { ShowWindow(); Page("Settings"); };
            FindFlyout<Button>("FlyoutReceiveButton").Click += async delegate
            {
                if (busy) return;
                if (ownsSession) { receivingIntent = false; await StopReceiving(); }
                else { receivingIntent = true; await StartReceiving(); }
            };
            var close = FindFlyout<Button>("FlyoutCloseButton"); if (close != null) close.Click += delegate { HideTrayFlyout(); };
        }
        UpdateTrayFlyout();
        flyout.Opacity = rendering ? 1 : 0;
        flyout.Show();
        if (!rendering)
        {
            var cursor = Forms.Cursor.Position;
            var bounds = Forms.Screen.FromPoint(cursor).WorkingArea;
            var source = PresentationSource.FromVisual(flyout);
            if (source != null && source.CompositionTarget != null)
            {
                var scale = source.CompositionTarget.TransformFromDevice;
                var point = scale.Transform(new System.Windows.Point(cursor.X, cursor.Y));
                var topLeft = scale.Transform(new System.Windows.Point(bounds.Left, bounds.Top));
                var bottomRight = scale.Transform(new System.Windows.Point(bounds.Right, bounds.Bottom));
                double width = flyout.ActualWidth > 0 ? flyout.ActualWidth : flyout.Width;
                double height = flyout.ActualHeight > 0 ? flyout.ActualHeight : flyout.Height;
                double left = Math.Max(topLeft.X + 8, Math.Min(point.X - width + 20, bottomRight.X - width - 8));
                double top = point.Y - height - 12;
                if (top < topLeft.Y + 8) top = point.Y + 12;
                top = Math.Max(topLeft.Y + 8, Math.Min(top, bottomRight.Y - height - 8));
                flyout.Left = left; flyout.Top = top;
            }
        }
        flyout.Opacity = 1;
        flyout.Activate();
    }
    void UpdateTrayFlyout()
    {
        if (flyout == null) return;
        var status = FindFlyout<TextBlock>("FlyoutStatusText"); if (status != null) status.Text = Find<TextBlock>("StatusText").Text;
        var detail = FindFlyout<TextBlock>("FlyoutStatusDetail"); if (detail != null) detail.Text = Find<TextBlock>("StatusDetail").Text;
        var last = FindFlyout<TextBlock>("FlyoutLastText"); if (last != null) last.Text = items.Count == 0 ? "No transfers yet" : items[0].Name + " · " + items[0].ReceivedAt.ToLocalTime().ToString("HH:mm");
        var count = FindFlyout<TextBlock>("FlyoutCountText"); if (count != null) count.Text = items.Count + (items.Count == 1 ? " received item" : " received items");
        var adapter = FindFlyout<TextBlock>("FlyoutAdapterText"); if (adapter != null) adapter.Text = ownsSession ? "Connected" : string.IsNullOrEmpty(settings.AdapterInstanceId) ? "Not selected" : "Released to Windows";
        var runtimeStatus = FindFlyout<TextBlock>("FlyoutRuntimeText"); if (runtimeStatus != null) runtimeStatus.Text = busy ? ownsSession ? "Stopping…" : "Starting…" : ownsSession ? "WSL2 running" : "Stopped";
        var dot = FindFlyout<System.Windows.Shapes.Ellipse>("FlyoutStatusDot"); if (dot != null) dot.Fill = Find<System.Windows.Shapes.Ellipse>("StatusDot").Fill;
        var toggle = FindFlyout<Button>("FlyoutReceiveButton"); if (toggle != null) { toggle.Content = busy ? "Working…" : ownsSession ? "Stop receiving" : "Start receiving"; toggle.IsEnabled = !busy; }
    }
    void UpdateCount() { Find<TextBlock>("CountText").Text = items.Count + (items.Count == 1 ? " item" : " items"); Find<StackPanel>("EmptyState").Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed; UpdateTrayFlyout(); }
    void SetStatus(string title, string detail, bool ready)
    {
        Find<TextBlock>("StatusText").Text = title; Find<TextBlock>("StatusDetail").Text = detail;
        Find<System.Windows.Shapes.Ellipse>("StatusDot").Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(ready ? "#EE725D" : "#A7988F"));
        Find<Button>("ReceiveButton").Content = ownsSession ? "Stop receiving" : "Start receiving";
        Find<Button>("ReceiveButton").IsEnabled = !busy;
        if (!rendering) tray.Text = "SwooshDrop · " + title.Substring(0, Math.Min(48, title.Length));
        UpdateTrayFlyout();
    }
    void OnUi(Action action) { if (!quitting) window.Dispatcher.BeginInvoke(action); }
    void Log(string text)
    {
        try { string path = Path.Combine(data, "Logs", "app.log"); Directory.CreateDirectory(Path.GetDirectoryName(path)); if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.WriteAllText(path, ""); File.AppendAllText(path, DateTime.Now.ToString("s") + " " + text + Environment.NewLine, Encoding.UTF8); } catch (IOException) { }
    }
    void RefreshLogView()
    {
        var box = Find<TextBox>("LogsTextBox"); if (box == null) return;
        string path = SelectedLogPath();
        try
        {
            if (!File.Exists(path)) { box.Text = "No events recorded yet."; return; }
            string tail;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long start = Math.Max(0, file.Length - 48000); file.Seek(start, SeekOrigin.Begin);
                using (var reader = new StreamReader(file, Encoding.UTF8, true)) tail = reader.ReadToEnd();
                if (start > 0) { int newline = tail.IndexOf('\n'); if (newline >= 0) tail = tail.Substring(newline + 1); }
            }
            tail = tail.TrimEnd();
            if (box.Text != tail)
            {
                bool follow = box.VerticalOffset >= box.ExtentHeight - box.ViewportHeight - 2;
                double offset = box.VerticalOffset;
                box.Text = tail;
                if (follow) box.ScrollToEnd(); else box.ScrollToVerticalOffset(offset);
            }
        }
        catch (IOException) { }
    }
    string SelectedLogPath()
    {
        var source = Find<ComboBox>("LogSourceCombo");
        if (source != null && source.SelectedIndex == 1) return Path.Combine(runtime, "logs", "owl.log");
        if (source != null && source.SelectedIndex == 2) return Path.Combine(runtime, "logs", "bridge.log");
        return Path.Combine(data, "Logs", "app.log");
    }
    T Read<T>(string file) where T : class
    { try { string path = Path.Combine(data, file); if (File.Exists(path) && new FileInfo(path).Length < 2097152) return json.Deserialize<T>(File.ReadAllText(path)); } catch (Exception ex) { Log("Read " + file + ": " + ex.Message); } return null; }
    void Write(string file, object value)
    {
        string path = Path.Combine(data, file), temporary = path + ".tmp";
        File.WriteAllText(temporary, json.Serialize(value), Encoding.UTF8);
        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
    }
    void SaveSettings() { try { Write("settings.json", settings); } catch (IOException ex) { Log(ex.Message); } }
    string NotificationMode { get { return settings.NotificationMode == "app" || (string.IsNullOrEmpty(settings.NotificationMode) && !settings.Notifications) ? "app" : "windows"; } }
    void SetNotificationMode(string mode)
    {
        settings.NotificationMode = mode;
        settings.Notifications = mode == "windows"; // preserve the older setting for upgrades
        SaveSettings();
    }
    void SaveHistory() { try { Write("history.json", new List<TransferItem>(items)); } catch (IOException ex) { Log(ex.Message); } }
    void ImportFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (string file in Directory.GetFiles(folder))
        {
            bool found = false; foreach (var item in items) if (string.Equals(item.Path, file, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
            if (found) continue;
            var info = new FileInfo(file); items.Insert(0, new TransferItem { Id = Guid.NewGuid().ToString("N"), Path = info.FullName, Name = info.Name, Sender = "Earlier transfer", Bytes = info.Length, ReceivedAt = info.CreationTimeUtc });
        }
        while (items.Count > 500) items.RemoveAt(items.Count - 1);
        SaveHistory(); UpdateCount();
    }
    void OpenItem(object sender, RoutedEventArgs e)
    {
        var button = e.OriginalSource as Button; if (button == null) return;
        var item = button.Tag as TransferItem; if (item == null) return;
        try
        {
            if (Convert.ToString(button.Content) == "Show in folder") Process.Start("explorer.exe", "/select," + Quote(item.Path));
            else if (!string.IsNullOrEmpty(item.Link))
            {
                OpenLink(item.Link);
            }
            else if (File.Exists(item.Path)) Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true });
            else MessageBox.Show("This file was moved or deleted. You can still see its transfer details here.", "SwooshDrop");
        }
        catch (Exception ex) { Log("Open: " + ex.Message); MessageBox.Show("Windows couldn't open this item.", "SwooshDrop"); }
    }
    static void OpenLink(string link)
    {
        Uri url;
        if (!Uri.TryCreate(link, UriKind.Absolute, out url) || (url.Scheme != "http" && url.Scheme != "https") || !string.IsNullOrEmpty(url.UserInfo))
            throw new InvalidOperationException("Only HTTP and HTTPS web links can be opened.");
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }

    static string Quote(string value)
    {
        if (value.Length > 0 && value.IndexOfAny(new char[] { ' ', '\t', '\n', '\r', '"' }) < 0) return value;
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value) { if (c == '\\') { slashes++; continue; } if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append('"'); } else { result.Append('\\', slashes); result.Append(c); } slashes = 0; }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
    static ProcessStartInfo Info(string exe, params string[] args)
    {
        var quoted = new List<string>(); foreach (string arg in args) quoted.Add(Quote(arg));
        var info = new ProcessStartInfo(exe, string.Join(" ", quoted.ToArray())) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        info.EnvironmentVariables["WSL_UTF8"] = "1"; return info;
    }
    static string Run(string exe, params string[] args)
    {
        using (var process = Process.Start(Info(exe, args)))
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(45000)) { process.Kill(); throw new Exception("Command timed out: " + Path.GetFileName(exe)); }
            Task.WaitAll(output, error); if (process.ExitCode != 0) throw new Exception(error.Result + output.Result); return output.Result.Trim();
        }
    }
    string Wsl(params string[] args) { var all = new List<string> { "-d", settings.RuntimeDistro, "-u", "root", "--exec" }; all.AddRange(args); return Run("wsl.exe", all.ToArray()); }
    string LinuxPath(string path) { return Wsl("wslpath", "-u", path); }

    async Task StartReceiving()
    {
        if (busy || ownsSession || rendering) return;
        busy = true; SetStatus("Getting ready…", "Connecting your dedicated adapter.", false); bool failed = false;
        try
        {
            string saveFolder = settings.SaveFolder;
            await Task.Run(delegate
            {
                control = LinuxPath(Path.Combine(runtime, "airdrop-lab-control.sh"));
                string script = LinuxPath(Path.Combine(runtime, "airdrop-lab.sh"));
                Wsl("bash", control, "available");
                var state = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Run(usb, "state"));
                var target = AdapterCatalog.Select((IEnumerable)state["Devices"], settings.AdapterInstanceId, ResourceText("adapters.json"));
                if (target == null || string.IsNullOrEmpty(Convert.ToString(target["BusId"]))) throw new Exception("adapter-missing");
                sessionDevice = Convert.ToString(target["InstanceId"]);
                var identity = AdapterIdentity.Parse(sessionDevice);
                var profile = AdapterCatalog.Profile(sessionDevice, ResourceText("adapters.json"));
                if (profile == null) throw new Exception("This adapter is not supported by the installed radio driver package.");
                bus = Convert.ToString(target["BusId"]);
                int count = int.Parse(Wsl("bash", control, "count", identity.Vendor, identity.Product, identity.Serial));
                bool reportedAttached = !string.IsNullOrEmpty(Convert.ToString(target["ClientIPAddress"]));
                if (count > 1 || (count == 1 && !reportedAttached))
                {
                    if (count > 1 && string.IsNullOrEmpty(identity.Serial)) throw new Exception("Multiple matching USB adapters lack a unique serial. Reconnect only the selected adapter before receiving.");
                    if (reportedAttached) Run(usb, "detach", "--busid", bus);
                    Wsl("bash", control, "release", identity.Vendor, identity.Product, identity.Serial);
                    count = 0; reportedAttached = false;
                }
                if (count == 0)
                {
                    if (reportedAttached) Run(usb, "detach", "--busid", bus);
                    Run(usb, "attach", "--wsl", settings.RuntimeDistro, "--busid", bus);
                    Wsl("bash", control, "attached", identity.Vendor, identity.Product, identity.Serial);
                }
                ownsSession = true; token = Guid.NewGuid().ToString("N"); Directory.CreateDirectory(saveFolder);
                var info = Info("wsl.exe", "-d", settings.RuntimeDistro, "-u", "root", "--exec", "setsid", "--wait", "bash", script, profile.Driver, "--ui-events", LinuxPath(saveFolder), token, identity.Vendor, identity.Product, identity.Serial);
                info.RedirectStandardInput = true;
                var child = new Process { StartInfo = info, EnableRaisingEvents = true }; receiver = child;
                child.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) OnUi(delegate { if (child == receiver) HandleLine(e.Data); }); };
                child.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) OnUi(delegate { if (child == receiver) Log(e.Data); }); };
                child.Exited += delegate { OnUi(async delegate { if (child == receiver && ownsSession && !busy) { if (child.ExitCode == 75) ownsSession = false; else await StopReceiving(); SetStatus("Connection paused", "Click Start to reconnect, or wait for an automatic retry.", false); } }); };
                child.Start(); child.StandardInput.AutoFlush = true; child.BeginOutputReadLine(); child.BeginErrorReadLine();
            });
        }
        catch (Exception ex) { failed = true; Log("Start: " + ex.Message); SetStatus(ex.Message.Contains("adapter-missing") ? "Connect your adapter" : "Couldn't start receiving", ex.Message.Contains("adapter-missing") ? "Plug in your selected USB adapter. SwooshDrop will try again." : "Open Settings → Adapter setup to check this PC, or About → Open diagnostics.", false); }
        finally { busy = false; Find<Button>("ReceiveButton").IsEnabled = true; Find<Button>("ReceiveButton").Content = ownsSession ? "Stop receiving" : "Start receiving"; UpdateTrayFlyout(); }
        if (failed && ownsSession) await StopReceiving();
        if (receiver != null && receiver.HasExited && ownsSession) await StopReceiving();
    }
    async Task StopReceiving()
    {
        if (busy || !ownsSession) return;
        busy = true; Decide(false); SetStatus("Stopping…", "Returning the adapter to Windows.", false);
        try
        {
            await Task.Run(delegate
            {
                Wsl("bash", control, "stop", token);
                if (receiver != null && !receiver.HasExited && !receiver.WaitForExit(10000)) throw new Exception("Receiver has not stopped yet.");
                var state = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Run(usb, "state"));
                foreach (Dictionary<string, object> device in (IEnumerable)state["Devices"]) if (string.Equals(Convert.ToString(device["InstanceId"]), sessionDevice, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(Convert.ToString(device["ClientIPAddress"]))) Run(usb, "detach", "--busid", Convert.ToString(device["BusId"]));
                var identity = AdapterIdentity.Parse(sessionDevice);
                Wsl("bash", control, "release", identity.Vendor, identity.Product, identity.Serial);
                // The managed runtime has no work after the radio is detached.
                // Ending only this distro releases its idle services and cache;
                // other WSL distributions (including Docker) stay running.
                if (string.Equals(settings.RuntimeDistro, "WinDropRuntime", StringComparison.OrdinalIgnoreCase))
                {
                    try { Run("wsl.exe", "--terminate", settings.RuntimeDistro); }
                    catch (Exception ex) { Log("Runtime idle shutdown: " + ex.Message); }
                }
            });
            ownsSession = false; if (receiver != null) { receiver.Dispose(); receiver = null; }
            SetStatus("Receiving is off", "Start receiving whenever you're ready.", false);
        }
        catch (Exception ex) { Log("Stop: " + ex.Message); SetStatus("Still stopping", "Please try Stop again.", false); }
        finally { busy = false; Find<Button>("ReceiveButton").IsEnabled = true; Find<Button>("ReceiveButton").Content = ownsSession ? "Stop receiving" : "Start receiving"; UpdateTrayFlyout(); }
    }

    void HandleLine(string line)
    {
        if (!line.StartsWith("{")) { Log(line); return; }
        try
        {
            var item = json.Deserialize<Dictionary<string, object>>(line); string kind = Convert.ToString(item["kind"]);
            if (kind == "ready") SetStatus("Ready to receive", "On iPhone: Share → AirDrop → SwooshDrop. Discovery may take a moment.", true);
            else if (kind == "log") Log(Convert.ToString(item["message"]));
            else if (kind == "consent") ReceiveConsent(item);
            else if (kind == "completed") Complete(item);
        }
        catch (Exception ex) { Log("UI event: " + ex.Message); }
    }
    static string[] Strings(Dictionary<string, object> item, string key)
    { var result = new List<string>(); object value; if (item.TryGetValue(key, out value) && value is IEnumerable && !(value is string)) foreach (object entry in (IEnumerable)value) if (entry is string) result.Add((string)entry); return result.ToArray(); }
    void ReceiveConsent(Dictionary<string, object> item)
    {
        if (incoming != null) Decide(false);
        incoming = item; deadline = DateTime.UtcNow.AddSeconds(55); lastSender = Convert.ToString(item["sender"]); incomingPreview = null;
        object preview; if (item.TryGetValue("preview", out preview) && preview is string) incomingPreview = SavePreview((string)preview, Convert.ToString(item["id"]));
        string[] links = Strings(item, "links"), files = Strings(item, "files");
        string description = links.Length > 0 ? string.Join("\n", links) : string.Join("\n", files);
        Find<TextBlock>("PendingText").Text = lastSender + " wants to share " + (links.Length > 0 ? "a link" : files.Length + " file(s)"); Find<Border>("PendingBanner").Visibility = Visibility.Visible;
        SetStatus("Incoming AirDrop", "Accept or decline the transfer from " + lastSender + ".", true);
        string id = Convert.ToString(item["id"]);
        Notify("AirDrop from " + lastSender, description, incomingPreview, id, async delegate(bool shown)
        {
            if (!shown && incoming != null && Convert.ToString(incoming["id"]) == id) OpenPopup();
            await Task.FromResult(0);
        });
    }
    void OpenPopup()
    {
        if (incoming == null || popup != null) return;
        popup = (Window)XamlReader.Parse(ResourceText("TransferPopup.xaml"));
        popup.Closed += delegate(object sender, EventArgs e) { if (ReferenceEquals(popup, sender)) popup = null; if (incoming != null) Decide(false); };
        ((TextBlock)popup.FindName("SenderText")).Text = Convert.ToString(incoming["sender"]) + " wants to share";
        string[] links = Strings(incoming, "links"), files = Strings(incoming, "files");
        ((TextBlock)popup.FindName("FilesText")).Text = links.Length > 0 ? (links.Length == 1 ? "Web link" : links.Length + " web links") : string.Join(", ", files);
        if (!string.IsNullOrEmpty(incomingPreview)) ((System.Windows.Controls.Image)popup.FindName("PreviewImage")).Source = ImageFile(incomingPreview);
        if (links.Length > 0) { var link = (TextBox)popup.FindName("LinkText"); link.Text = string.Join("\n\n", links); link.Visibility = Visibility.Visible; ((TextBlock)popup.FindName("PreviewGlyph")).Visibility = Visibility.Collapsed; }
        ((Button)popup.FindName("AcceptButton")).Click += delegate { Decide(true); };
        ((Button)popup.FindName("DeclineButton")).Click += delegate { Decide(false); };
        popup.Opacity = rendering ? 1 : 0;
        popup.Show();
        if (!rendering)
        {
            var bounds = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
            var source = PresentationSource.FromVisual(popup);
            if (source != null && source.CompositionTarget != null)
            {
                var scale = source.CompositionTarget.TransformFromDevice;
                var topLeft = scale.Transform(new System.Windows.Point(bounds.Left, bounds.Top));
                var bottomRight = scale.Transform(new System.Windows.Point(bounds.Right, bounds.Bottom));
                popup.Left = bottomRight.X - popup.Width - 18;
                popup.Top = bottomRight.Y - popup.Height - 18;
                popup.Left = Math.Max(topLeft.X + 8, popup.Left);
                popup.Top = Math.Max(topLeft.Y + 8, popup.Top);
            }
        }
        popup.Opacity = 1;
    }
    void Decide(bool accepted)
    {
        if (incoming == null) return;
        string id = Convert.ToString(incoming["id"]); if (DateTime.UtcNow >= deadline) accepted = false;
        try { if (receiver != null && !receiver.HasExited) receiver.StandardInput.WriteLine(json.Serialize(new { id = id, accept = accepted })); }
        catch (Exception ex) { Log("Consent: " + ex.Message); accepted = false; }
        incoming = null; if (popup != null) { popup.Close(); popup = null; }
        Find<Border>("PendingBanner").Visibility = Visibility.Collapsed;
        SetStatus(accepted ? "Receiving your drop…" : "Ready to receive", accepted ? "Saving the original files to your received folder." : "On iPhone: Share → AirDrop → SwooshDrop.", true);
    }
    void Complete(Dictionary<string, object> eventItem)
    {
        string[] files = Strings(eventItem, "files"), links = Strings(eventItem, "links");
        var added = new List<TransferItem>();
        foreach (string linux in files)
        {
            string path = WindowsPath(linux); if (!File.Exists(path)) continue;
            var file = new FileInfo(path);
            if (links.Length > 0)
                foreach (string link in links) added.Add(new TransferItem { Id = Guid.NewGuid().ToString("N"), Path = path, Name = link, Link = link, Sender = lastSender ?? "AirDrop", Bytes = file.Length, ReceivedAt = DateTime.UtcNow });
            else added.Add(new TransferItem { Id = Guid.NewGuid().ToString("N"), Path = path, Name = file.Name, Sender = lastSender ?? "AirDrop", Bytes = file.Length, ReceivedAt = DateTime.UtcNow });
        }
        if (added.Count > 0 && links.Length == 0) added[0].PreviewPath = incomingPreview;
        foreach (var item in added) items.Insert(0, item);
        while (items.Count > 500) items.RemoveAt(items.Count - 1);
        SaveHistory(); UpdateCount(); SetStatus("Ready for your next drop", "Saved to " + settings.SaveFolder, true);
        Notify(links.Length > 0 ? "Link received" : "AirDrop received", links.Length > 0 ? string.Join("\n", links) : string.Join(", ", Array.ConvertAll(added.ToArray(), entry => entry.Name)), incomingPreview, null, null);
        incomingPreview = null; QueueMissingPreviews();
        if (settings.OpenLinksOnReceive && added.Count > 0)
            foreach (string link in new HashSet<string>(links, StringComparer.Ordinal))
                try { OpenLink(link); Log("Opened an accepted web link in the default browser."); }
                catch (Exception ex) { Log("Open received link: " + ex.Message); SetStatus("Link saved", "Windows couldn't open your browser. Use Open in the gallery to try again.", true); }
    }
    static string WindowsPath(string linux)
    { if (linux.StartsWith("/mnt/") && linux.Length > 7 && char.IsLetter(linux[5]) && linux[6] == '/') return char.ToUpperInvariant(linux[5]) + ":\\" + linux.Substring(7).Replace('/', '\\'); return linux; }
    string SavePreview(string encoded, string id)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(encoded); if (bytes.Length > 450000 || bytes.Length < 8 || bytes[0] != 137 || bytes[1] != 80) return null;
            var decoded = Bitmap(bytes); if (decoded.PixelWidth > 440 || decoded.PixelHeight > 300) return null;
            string path = Path.Combine(data, "Previews", Guid.NewGuid().ToString("N") + ".png"); File.WriteAllBytes(path, bytes); return path;
        }
        catch { return null; }
    }
    static BitmapImage Bitmap(byte[] bytes)
    { using (var stream = new MemoryStream(bytes)) { var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; } }
    static BitmapImage ImageFile(string path) { return Bitmap(File.ReadAllBytes(path)); }
    bool thumbnailsRunning;
    async void QueueMissingPreviews()
    {
        if (thumbnailsRunning || rendering) return; thumbnailsRunning = true;
        try
        {
            var attempted = new HashSet<string>();
            while (true)
            {
                TransferItem item = null;
                foreach (var candidate in items)
                    if (!attempted.Contains(candidate.Id) && TransferItem.IsPhoto(candidate.Path) && File.Exists(candidate.Path) && (string.IsNullOrEmpty(candidate.PreviewPath) || !File.Exists(candidate.PreviewPath))) { item = candidate; break; }
                if (item == null) break;
                attempted.Add(item.Id);
                string encoded = await Task.Run(delegate
                {
                    try
                    {
                        var info = Info("wsl.exe", "-d", settings.RuntimeDistro, "-u", "root", "--exec", "python3", LinuxPath(Path.Combine(runtime, "render-preview.py"))); info.RedirectStandardInput = true;
                        using (var process = Process.Start(info))
                        {
                            Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
                            process.StandardInput.Write(new JavaScriptSerializer().Serialize(new { file = LinuxPath(item.Path) })); process.StandardInput.Close();
                            if (!process.WaitForExit(9000)) { process.Kill(); return null; }
                            Task.WaitAll(output, errors); return process.ExitCode == 0 ? output.Result.Trim() : null;
                        }
                    }
                    catch (Exception ex) { Log("Thumbnail: " + ex.Message); return null; }
                });
                if (!string.IsNullOrEmpty(encoded)) item.PreviewPath = SavePreview(encoded, item.Id);
            }
            SaveHistory();
        }
        finally { thumbnailsRunning = false; }
    }
    async void Notify(string title, string body, string image, string id, Func<bool, Task> callback)
    {
        bool shown = false;
        if (NotificationMode == "windows")
            shown = await Task.Run(delegate
            {
                string request = Path.Combine(data, "toast-" + Guid.NewGuid().ToString("N") + ".json");
                try { File.WriteAllText(request, new JavaScriptSerializer().Serialize(new { title = title, body = body, image = image, id = id, tag = id == null ? "received" : "incoming" }), Encoding.UTF8); Run("powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(runtime, "Show-Toast.ps1"), "-RequestPath", request); return true; }
                catch (Exception ex) { Log("Notification: " + ex.Message); return false; }
                finally { try { File.Delete(request); } catch (IOException) { } }
            });
        if (callback != null) await callback(shown);
        else if (!shown && NotificationMode == "windows") tray.ShowBalloonTip(5000, title, body.Length > 200 ? body.Substring(0, 200) : body, Forms.ToolTipIcon.Info);
    }
    void Activate(string activation)
    {
        Uri uri; if (!Uri.TryCreate(activation, UriKind.Absolute, out uri) || uri.Scheme != "windrop") return;
        Log("Notification activation: " + uri.Host);
        if (uri.Host == "show") { ShowWindow(); return; }
        if (uri.Host == "quit") { QuitFromActivation(); return; }
        if (incoming == null) return;
        string expected = "?id=" + Convert.ToString(incoming["id"]);
        if (uri.Query != expected) return;
        if (uri.Host == "accept") Decide(true); else if (uri.Host == "decline") Decide(false);
    }
    async void QuitFromActivation()
    {
        if (busy) return; receivingIntent = false; await StopReceiving(); if (ownsSession) return;
        quitting = true; tray.Dispose(); Application.Current.Shutdown();
    }
    static bool ForwardActivation(string activation)
    {
        try { using (var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out)) { pipe.Connect(1500); using (var writer = new StreamWriter(pipe)) { writer.WriteLine(activation); writer.Flush(); } } return true; } catch { return false; }
    }
    static void RunToastServer()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timeout.Tick += delegate { application.Shutdown(); }; timeout.Start();
        ToastActivation.OnActivated = delegate(string activation)
        {
            Task.Run(delegate
            {
                try
                {
                    Uri uri;
                    if (Uri.TryCreate(activation, UriKind.Absolute, out uri) && uri.Scheme == "windrop" && (uri.Host == "show" || uri.Host == "accept" || uri.Host == "decline"))
                        if (!ForwardActivation(activation))
                            Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, Quote("windrop://show")) { UseShellExecute = false, CreateNoWindow = true });
                }
                finally { application.Dispatcher.BeginInvoke(new Action(delegate { application.Shutdown(); })); }
            });
        };
        int cookie = ToastActivation.RegisterServer();
        try { application.Run(); } finally { ToastActivation.UnregisterServer(cookie); }
    }
    void ListenForActivation()
    {
        Task.Run(delegate
        {
            while (!quitting)
                try { using (var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None)) { pipe.WaitForConnection(); using (var reader = new StreamReader(pipe)) { char[] buffer = new char[2048]; int count = reader.Read(buffer, 0, buffer.Length); string activation = new string(buffer, 0, count).Trim(); OnUi(delegate { Activate(activation); }); } } }
                catch (IOException) { }
        });
    }
    void ExtractRuntime()
    {
        Directory.CreateDirectory(runtime);
        foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
            if (!name.StartsWith("Runtime.")) continue;
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var output = File.Create(Path.Combine(runtime, name.Substring(8)))) input.CopyTo(output);
        }
    }
    static byte[] ResourceBytes(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly(); foreach (string name in assembly.GetManifestResourceNames()) if (name.EndsWith(suffix, StringComparison.Ordinal)) using (var input = assembly.GetManifestResourceStream(name)) using (var output = new MemoryStream()) { input.CopyTo(output); return output.ToArray(); }
        throw new FileNotFoundException("Missing embedded resource: " + suffix);
    }
    static string ResourceText(string suffix) { return Encoding.UTF8.GetString(ResourceBytes(suffix)); }
    void Render(Window target, string path)
    {
        target.UpdateLayout(); target.Dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Render);
        var content = (FrameworkElement)target.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(path)) encoder.Save(file);
    }
}
