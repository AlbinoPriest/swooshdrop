using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("SwooshDrop Setup")]
[assembly: AssemblyVersion("0.3.4.0")]
[assembly: AssemblyFileVersion("0.3.4.0")]

public sealed class SetupEngine
{
    public const string Distro = "WinDropRuntime", Kernel = "6.18.33.2-microsoft-standard-WSL2";
    public const string UsbHash = "1c984914aec944de19b64eff232421439629699f8138e3ddc29301175bc6d938";
    public const string WslHash = "a3505a50f4cc585551d11d9de824ba4375448d7a68f2e71d3fb315fa986fc754";
    public const string RootHash = "2a790896740b14d637dbdc583cce1ba081ac53b9e9cdb46dc09a2f73abbd9934";
    public static readonly string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinDrop");
    public static readonly string Usb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "usbipd-win", "usbipd.exe");
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 2097152 };
    public Action<string> Report = delegate { };
    public static string ResourceText(string name) { using (var r = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name))) return r.ReadToEnd(); }
    public static void Extract(string name, string destination) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) using (var output = File.Create(destination)) input.CopyTo(output); }
    public static string Quote(string value)
    {
        if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) < 0) return value;
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value) { if (c == '\\') { slashes++; continue; } if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append('"'); } else { result.Append('\\', slashes); result.Append(c); } slashes = 0; }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
    public static string Run(string file, params string[] args)
    {
        var info = new ProcessStartInfo(file, string.Join(" ", args.Select(Quote))) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.EnvironmentVariables["WSL_UTF8"] = "1"; info.StandardOutputEncoding = info.StandardErrorEncoding = Encoding.UTF8;
        using (var p = Process.Start(info)) { var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync(); p.WaitForExit(); Task.WaitAll(stdout, stderr); string text = (stdout.Result + "\n" + stderr.Result).Replace("\0", "").Trim(); if (p.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(file) + ": " + text); return stdout.Result.Replace("\0", "").Trim(); }
    }
    public static string Hash(string path) { using (var sha = SHA256.Create()) using (var f = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }
    public static bool Verified(string path, string hash) { return File.Exists(path) && Hash(path) == hash; }
    public static string KernelVersion() { try { return Run("wsl.exe", "--system", "--exec", "uname", "-r"); } catch { return ""; } }
    public static List<string> Distros() { return Run("wsl.exe", "--list", "--quiet").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(); }
    public static List<Dictionary<string, object>> Devices()
    {
        var result = new List<Dictionary<string, object>>();
        if (!File.Exists(Usb)) return result;
        var state = Json.Deserialize<Dictionary<string, object>>(Run(Usb, "state"));
        foreach (Dictionary<string, object> d in (IEnumerable)state["Devices"])
            if (!string.IsNullOrEmpty(Convert.ToString(d["BusId"]))) result.Add(d);
        return result;
    }
    public static Dictionary<string, object> Check()
    {
        return new Dictionary<string, object> { { "WindowsX64", Environment.Is64BitOperatingSystem && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "AMD64" }, { "Kernel", KernelVersion() }, { "RequiredKernel", Kernel }, { "UsbIpdInstalled", File.Exists(Usb) }, { "InstallDirectory", Home }, { "DriverProfiles", Json.Deserialize<List<AdapterProfile>>(ResourceText("Setup.adapters.json")).Count } };
    }
    public void Download(string url, string path, string hash)
    {
        if (Verified(path, hash)) { Report("Using verified cached " + Path.GetFileName(path)); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)); string partial = path + ".partial";
        Report("Downloading " + Path.GetFileName(path)); ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (var client = new WebClient())
        {
            DateTime last = DateTime.MinValue;
            client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e) { if ((DateTime.UtcNow - last).TotalSeconds >= 3) { last = DateTime.UtcNow; Report(Path.GetFileName(path) + ": " + e.ProgressPercentage + "% · " + (e.BytesReceived / 1048576) + " MB"); } };
            client.DownloadFileTaskAsync(new Uri(url), partial).GetAwaiter().GetResult();
        }
        if (!Verified(partial, hash)) { File.Delete(partial); throw new InvalidOperationException("Download checksum did not match. Nothing was installed. The upstream download may have changed; use an updated SwooshDrop installer."); }
        if (File.Exists(path)) File.Delete(path); File.Move(partial, path);
    }
    public static void SignedMsi(string path, string hash)
    {
        if (!Verified(path, hash)) throw new InvalidOperationException("Invalid prerequisite checksum.");
        string script = "$s=Get-AuthenticodeSignature -LiteralPath '" + path.Replace("'", "''") + "'; if($s.Status -ne 'Valid'){throw 'Prerequisite signature is not valid'}";
        Run("powershell.exe", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
    }
    public static int InstallMsi(string path, string hash)
    {
        // Verify and execute a protected copy so the normal user's download cache
        // cannot be replaced between elevated verification and MSI execution.
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(admins);
        foreach (var sid in new[] { admins, system }) acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        string staging = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinDropSetup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging, acl); string protectedMsi = Path.Combine(staging, "prerequisite.msi");
        try
        {
            File.Copy(path, protectedMsi); SignedMsi(protectedMsi, hash);
            using (var p = Process.Start(new ProcessStartInfo("msiexec.exe", "/i " + Quote(protectedMsi) + " /passive /norestart") { UseShellExecute = true })) { p.WaitForExit(); if (p.ExitCode != 0 && p.ExitCode != 3010) throw new InvalidOperationException("Prerequisite installer returned " + p.ExitCode); return p.ExitCode; }
        }
        finally { if (File.Exists(protectedMsi)) File.Delete(protectedMsi); Directory.Delete(staging); }
    }
    public static int Admin(string[] args)
    {
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Administrator permission is required for this setup step.");
        if (args[0] == "--install-usb") return InstallMsi(args[1], UsbHash);
        if (args[0] == "--install-wsl" || args[0] == "--enable-wsl")
        {
            // No shutdown, downgrade, custom kernel, or automatic reboot.
            bool restart = false;
            foreach (string feature in new[] { "Microsoft-Windows-Subsystem-Linux", "VirtualMachinePlatform" })
                using (var p = Process.Start(new ProcessStartInfo("dism.exe", "/online /enable-feature /featurename:" + feature + " /all /norestart") { UseShellExecute = false, CreateNoWindow = true })) { p.WaitForExit(); if (p.ExitCode != 0 && p.ExitCode != 3010) throw new InvalidOperationException("Windows could not enable " + feature + ". Return code " + p.ExitCode); restart |= p.ExitCode == 3010; }
            if (args[0] == "--install-wsl") restart |= InstallMsi(args[1], WslHash) == 3010; return restart ? 3010 : 0;
        }
        if (args[0] == "--bind")
        {
            string identity = args[1], bus = args[2]; AdapterIdentity.Parse(identity);
            if (!Regex.IsMatch(bus, @"^\d+-\d+(?:\.\d+)*$")) throw new ArgumentException("Invalid USB bus.");
            if (AdapterCatalog.Profile(identity, ResourceText("Setup.adapters.json")) == null) throw new InvalidOperationException("Unsupported USB adapter.");
            var selected = Devices().SingleOrDefault(d => string.Equals(Convert.ToString(d["InstanceId"]), identity, StringComparison.OrdinalIgnoreCase) && Convert.ToString(d["BusId"]) == bus);
            if (selected == null) throw new InvalidOperationException("The selected adapter moved or disconnected. Refresh and select it again.");
            Run(Usb, "bind", "--busid", bus); return 0;
        }
        throw new ArgumentException("Unknown administrator action.");
    }
    public int Elevate(params string[] args)
    {
        Report("Windows will ask for permission for this setup step.");
        using (var p = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, string.Join(" ", args.Select(Quote))) { UseShellExecute = true, Verb = "runas" })) { p.WaitForExit(); if (p.ExitCode != 0 && p.ExitCode != 3010) throw new InvalidOperationException("Setup step failed or was cancelled. Return code " + p.ExitCode); return p.ExitCode; }
    }
    public void Prerequisites()
    {
        if (!Environment.Is64BitOperatingSystem || Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") != "AMD64") throw new InvalidOperationException("This beta supports Intel/AMD 64-bit Windows only. ARM and 32-bit PCs need a separate build.");
        using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            if (int.Parse(Convert.ToString(key.GetValue("CurrentBuildNumber"))) < 19045) throw new InvalidOperationException("This beta requires Windows 10 22H2 or Windows 11.");
        string cache = Path.Combine(Home, "SetupCache");
        if (KernelVersion() == "")
        {
            string installed = ""; try { installed = Run("wsl.exe", "--version"); } catch { }
            int code;
            if (installed != "") code = Elevate("--enable-wsl");
            else
            {
                Download("https://github.com/microsoft/WSL/releases/download/2.7.13/wsl.2.7.13.0.x64.msi", Path.Combine(cache, "wsl.msi"), WslHash);
                code = Elevate("--install-wsl", Path.Combine(cache, "wsl.msi"));
            }
            if (code == 3010 || KernelVersion() == "") throw new InvalidOperationException("Windows needs a restart, or virtualization needs enabling in BIOS. Restart manually, then run SwooshDrop Setup again.");
        }
        string kernel = KernelVersion();
        if (kernel != Kernel) throw new InvalidOperationException("Your WSL kernel is " + kernel + ". This beta includes radio drivers for " + Kernel + " only. Update WSL if yours is older, or use a SwooshDrop release supporting your kernel. Setup will not downgrade or replace an existing kernel.");
        if (!File.Exists(Usb))
        {
            Download("https://github.com/dorssel/usbipd-win/releases/download/v5.3.0/usbipd-win_5.3.0_x64.msi", Path.Combine(cache, "usbipd.msi"), UsbHash);
            if (Elevate("--install-usb", Path.Combine(cache, "usbipd.msi")) == 3010) throw new InvalidOperationException("USB sharing needs a restart. Restart manually and run SwooshDrop Setup again.");
        }
        Report("Prerequisites ready. Select your dedicated USB Wi-Fi adapter.");
    }
    public void Install(string instance, bool autostart)
    {
        if (Process.GetProcessesByName("WinDrop").Length > 0 || Process.GetProcessesByName("SwooshDrop").Length > 0) throw new InvalidOperationException("Quit SwooshDrop or the previous WinDrop app from its tray menu before installing or changing its adapter.");
        var profile = AdapterCatalog.Profile(instance, ResourceText("Setup.adapters.json")); if (profile == null) throw new InvalidOperationException("Unsupported USB adapter.");
        if (KernelVersion() != Kernel) throw new InvalidOperationException("The WSL kernel changed. Check prerequisites again.");
        var selected = Devices().SingleOrDefault(d => string.Equals(Convert.ToString(d["InstanceId"]), instance, StringComparison.OrdinalIgnoreCase));
        if (selected == null) throw new InvalidOperationException("Connect the selected adapter and try again.");
        if (!string.IsNullOrEmpty(Convert.ToString(selected["ClientIPAddress"]))) throw new InvalidOperationException("The adapter is currently attached to WSL. Stop its receiver and detach it before setup changes.");
        if (selected["PersistedGuid"] == null || string.IsNullOrEmpty(Convert.ToString(selected["PersistedGuid"]))) Elevate("--bind", instance, Convert.ToString(selected["BusId"]));
        string cache = Path.Combine(Home, "SetupCache"), payload = Path.Combine(cache, "runtime-payload.tar.gz"), script = Path.Combine(cache, "bootstrap-runtime.sh");
        Extract("Setup.runtime-payload.tar.gz", payload); Extract("Setup.bootstrap-runtime.sh", script);
        var distros = Distros();
        if (!distros.Contains(Distro, StringComparer.OrdinalIgnoreCase))
        {
            string rootfs = Path.Combine(cache, "ubuntu-noble.rootfs.tar.gz");
            Download("https://cloud-images.ubuntu.com/wsl/releases/24.04/20240423/ubuntu-noble-wsl-amd64-24.04lts.rootfs.tar.gz", rootfs, RootHash);
            string storage = Path.Combine(Home, "Data", "WSL"); Directory.CreateDirectory(storage);
            Report("Creating SwooshDrop's dedicated runtime…"); Run("wsl.exe", "--import", Distro, storage, rootfs, "--version", "2");
            Run("wsl.exe", "-d", Distro, "-u", "root", "--exec", "touch", "/etc/windrop-owned-distro");
        }
        else
        {
            // Marker check prevents overwriting a distribution merely because it has our name.
            Run("wsl.exe", "-d", Distro, "-u", "root", "--exec", "test", "-f", "/etc/windrop-owned-distro");
        }
        string linuxScript = Run("wsl.exe", "-d", Distro, "-u", "root", "--exec", "wslpath", "-u", script);
        string linuxPayload = Run("wsl.exe", "-d", Distro, "-u", "root", "--exec", "wslpath", "-u", payload);
        Report("Installing Linux dependencies and checking the runtime. This can take several minutes…");
        Run("wsl.exe", "-d", Distro, "-u", "root", "--exec", "bash", linuxScript, linuxPayload);
        Report("Installing the Windows app, shortcuts, and notification support…");
        string app = Path.Combine(Home, "SwooshDrop.exe"); Extract("Setup.SwooshDrop.exe", app);
        Directory.CreateDirectory(Path.Combine(Home, "Data", "Runtime")); Directory.CreateDirectory(Path.Combine(Home, "Data", "Previews")); Directory.CreateDirectory(Path.Combine(Home, "Data", "Logs"));
        string settingsPath = Path.Combine(Home, "Data", "settings.json");
        var prefs = File.Exists(settingsPath) ? Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(settingsPath)) : new Dictionary<string, object> { { "SaveFolder", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SwooshDrop") }, { "ReceiveOnLaunch", true }, { "Notifications", true }, { "OpenLinksOnReceive", true } };
        prefs["RuntimeDistro"] = Distro; prefs["AdapterInstanceId"] = instance; File.WriteAllText(settingsPath, Json.Serialize(prefs), new UTF8Encoding(false));
        Extract("Setup.corresponding-source.tar.gz", Path.Combine(Home, "Sources", "corresponding-source-0.3.4.tar.gz"));
        Extract("Setup.NOTICES.txt", Path.Combine(Home, "Sources", "NOTICES.txt"));
        string setup = Path.Combine(Home, "SwooshDropSetup.exe"); if (!string.Equals(setup, Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase)) File.Copy(Assembly.GetExecutingAssembly().Location, setup, true);
        NativeIntegration.Register(app); NativeIntegration.SetAutostart(app, autostart);
        // Desktop shortcut copies the registered Start-menu shortcut, including notification metadata.
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SwooshDrop.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SwooshDrop.lnk"), true);
        // Keep the old data/runtime, but retire old launch points only after the new app is installed.
        foreach (string legacy in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WinDrop.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WinDrop.lnk"), Path.Combine(Home, "WinDrop.exe"), Path.Combine(Home, "WinDropSetup.exe") }) if (File.Exists(legacy)) File.Delete(legacy);
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinDrop"))
        { key.SetValue("DisplayName", "SwooshDrop"); key.SetValue("DisplayVersion", "0.3.4"); key.SetValue("Publisher", "SwooshDrop contributors"); key.SetValue("InstallLocation", Home); key.SetValue("DisplayIcon", app); key.SetValue("UninstallString", Quote(setup) + " --uninstall"); key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1); }
        Report("Installed. Open SwooshDrop, then use Share → AirDrop on your iPhone.");
    }
    public static void Uninstall()
    {
        if (Process.GetProcessesByName("WinDrop").Length > 0 || Process.GetProcessesByName("SwooshDrop").Length > 0) throw new InvalidOperationException("Quit SwooshDrop from its tray menu first.");
        NativeIntegration.SetAutostart(Path.Combine(Home, "SwooshDrop.exe"), false);
        foreach (string path in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SwooshDrop.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SwooshDrop.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WinDrop.lnk"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WinDrop.lnk"), Path.Combine(Home, "SwooshDrop.exe"), Path.Combine(Home, "WinDrop.exe"), Path.Combine(Home, "WinDropSetup.exe") }) if (File.Exists(path)) File.Delete(path);
        foreach (string key in new[] { @"Software\Classes\windrop", @"Software\Classes\AppUserModelId\WinDrop.PC", @"Software\Classes\CLSID\{74331E92-9FD9-4BEE-8563-83110C44FF39}", @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WinDrop" }) Registry.CurrentUser.DeleteSubKeyTree(key, false);
        // Keep received files, history, and the dedicated WSL distribution for reinstall.
    }
}

public static class SetupStringExtensions
{
    public static string ReplaceEndSlash(this string s) { int n = s.Length - s.TrimEnd('\\').Length; return s + new string('\\', n); }
}

public sealed class AdapterChoice
{
    public string Instance, Label; public AdapterProfile Profile;
    public override string ToString() { return Label; }
}

public sealed class SetupWindow : Form
{
    readonly Label title = new Label(), subtitle = new Label(), hardware = new Label();
    readonly ComboBox adapters = new ComboBox(); readonly CheckBox startup = new CheckBox();
    readonly Button check = new Button(), install = new Button(), open = new Button();
    readonly TextBox log = new TextBox(); readonly ProgressBar progress = new ProgressBar();
    readonly SetupEngine engine = new SetupEngine(); bool busy;
    public SetupWindow()
    {
        Text = "SwooshDrop Setup"; ClientSize = new Size(760, 600); MinimumSize = MaximumSize = Size; StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 249, 253); Font = new Font("Segoe UI", 10); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        title.Text = "AirDrop, meet your PC."; title.Font = new Font("Segoe UI", 24, FontStyle.Bold); title.SetBounds(32, 26, 700, 52);
        subtitle.Text = "SwooshDrop · 0.3.4 beta\nReceive original photos, files, and links from your iPhone."; subtitle.SetBounds(34, 86, 690, 52);
        hardware.Text = "Use a dedicated USB Wi-Fi adapter. SwooshDrop reserves it while receiving.\nWindows 10/11 x64 · Internet for setup · Compatible USB required."; hardware.SetBounds(34, 157, 690, 70);
        check.Text = "1  Check prerequisites"; check.SetBounds(34, 228, 245, 42);
        adapters.SetBounds(34, 290, 690, 32); adapters.DropDownStyle = ComboBoxStyle.DropDownList;
        startup.Text = "Start SwooshDrop when I sign in"; startup.SetBounds(34, 339, 320, 30); startup.Checked = NativeIntegration.IsAutostartEnabled();
        log.SetBounds(34, 383, 690, 116); log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.BorderStyle = BorderStyle.FixedSingle; log.BackColor = Color.White;
        log.Text = "Setup creates a separate Linux runtime and keeps your Windows internet connection available. Only the selected USB adapter is shared.\r\nYour photos and files remain in their received folder when updating or removing the app.";
        progress.SetBounds(34, 512, 690, 7); progress.Style = ProgressBarStyle.Marquee; progress.Visible = false;
        install.Text = "2  Install SwooshDrop"; install.SetBounds(34, 537, 245, 42); install.Enabled = false; install.BackColor = Color.FromArgb(51, 122, 245); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat;
        open.Text = "Open SwooshDrop"; open.SetBounds(499, 537, 225, 42); open.Enabled = false;
        Controls.AddRange(new Control[] { title, subtitle, hardware, check, adapters, startup, log, progress, install, open });
        engine.Report = Report;
        check.Click += async delegate { await Work(delegate { engine.Prerequisites(); }); if (!busy) RefreshAdapters(); };
        adapters.SelectedIndexChanged += delegate { var a = adapters.SelectedItem as AdapterChoice; install.Enabled = !busy && a != null && a.Profile != null; };
        install.Click += async delegate
        {
            var a = adapters.SelectedItem as AdapterChoice; if (a == null || a.Profile == null) return;
            if (a.Profile.Status != "tested" && MessageBox.Show(this, "This adapter has a bundled driver but has not passed an AirDrop test. Discovery or transfers may fail. Continue with this experimental adapter?", "Experimental adapter", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            bool start = startup.Checked; bool success = await Work(delegate { engine.Install(a.Instance, start); }); open.Enabled = success;
        };
        open.Click += delegate { Process.Start(Path.Combine(SetupEngine.Home, "SwooshDrop.exe")); Close(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; Report("Wait for the current setup step to finish before closing."); } };
    }
    void Report(string message) { if (InvokeRequired) { BeginInvoke(new Action<string>(Report), message); return; } log.AppendText("\r\n" + message); }
    async Task<bool> Work(Action action)
    {
        busy = true; check.Enabled = install.Enabled = open.Enabled = adapters.Enabled = startup.Enabled = false; progress.Visible = true;
        try { await Task.Run(action); return true; } catch (Exception ex) { Report(ex.Message); MessageBox.Show(this, ex.Message, "SwooshDrop Setup", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
        finally { busy = false; progress.Visible = false; check.Enabled = adapters.Enabled = startup.Enabled = true; }
    }
    public void RefreshAdapters()
    {
        adapters.Items.Clear();
        try
        {
            if (SetupEngine.KernelVersion() != SetupEngine.Kernel) return;
            foreach (var d in SetupEngine.Devices())
            {
                string id = Convert.ToString(d["InstanceId"]); AdapterProfile p;
                try { p = AdapterCatalog.Profile(id, SetupEngine.ResourceText("Setup.adapters.json")); } catch { continue; }
                // Show known Wi-Fi hardware only. Unknown USB devices might be storage or keyboards.
                if (p != null) adapters.Items.Add(new AdapterChoice { Instance = id, Profile = p, Label = Convert.ToString(d["Description"]) + " · " + p.Vendor + ":" + p.Product + " · " + p.Status + " · USB " + d["BusId"] });
            }
            if (adapters.Items.Count == 0) Report("No adapter from the driver catalog is connected. Plug in a compatible dedicated USB Wi-Fi adapter, then check again. See the compatibility guide for supported device IDs.");
            else adapters.SelectedIndex = 0;
        }
        catch (Exception ex) { Report(ex.Message); }
    }
    [STAThread] public static void Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && new[] { "--install-usb", "--install-wsl", "--enable-wsl", "--bind" }.Contains(args[0])) { try { Environment.Exit(SetupEngine.Admin(args)); } catch (Exception ex) { MessageBox.Show(ex.Message, "SwooshDrop Setup"); Environment.Exit(1); } return; }
        if (args.Length == 2 && args[0] == "--check-json") { File.WriteAllText(args[1], SetupEngine.Json.Serialize(SetupEngine.Check())); return; }
        if (args.Length > 0 && args[0] == "--uninstall")
        {
            if (MessageBox.Show("Remove the SwooshDrop app and shortcuts? Your received files, history, source archive, and Linux runtime will be kept.", "Remove SwooshDrop", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            try { SetupEngine.Uninstall(); MessageBox.Show("SwooshDrop removed. Received files and the runtime were kept.", "SwooshDrop"); } catch (Exception ex) { MessageBox.Show(ex.Message, "SwooshDrop"); } return;
        }
        var window = new SetupWindow();
        if (args.Length == 2 && args[0] == "--render") { window.Show(); Application.DoEvents(); window.RefreshAdapters(); Application.DoEvents(); using (var bitmap = new Bitmap(window.Width, window.Height)) { window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(args[1]); } window.Dispose(); return; }
        Application.Run(window);
    }
}
