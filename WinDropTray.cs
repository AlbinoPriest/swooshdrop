using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Local prototype. Requires the provisioned AirDropLab WSL distribution and usbipd.
public sealed class WinDropTray : Form
{
    readonly string folder = AppDomain.CurrentDomain.BaseDirectory;
    readonly string usb = @"C:\Program Files\usbipd-win\usbipd.exe";
    readonly JavaScriptSerializer json = new JavaScriptSerializer();
    readonly Label status = new Label();
    readonly TextBox history = new TextBox();
    readonly Button start = new Button();
    readonly Button stop = new Button();
    readonly NotifyIcon tray = new NotifyIcon();
    Process receiver;
    string control;
    string bus;
    bool ownsSession, busy, quitting;
    Form consent;

    [STAThread]
    public static void Main(string[] args)
    {
        bool created;
        using (var single = new Mutex(true, @"Local\WinDropAirDropLabTray", out created))
        {
            if (!created) { MessageBox.Show("WinDrop is already running. Look in the system tray."); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var app = new WinDropTray())
            {
                if (args.Length == 2 && args[0] == "--render")
                {
                    app.Show(); app.Update();
                    using (var bitmap = new Bitmap(app.Width, app.Height))
                    { app.DrawToBitmap(bitmap, new Rectangle(0, 0, app.Width, app.Height)); bitmap.Save(args[1]); }
                    app.tray.Visible = false; return;
                }
                if (args.Length == 1 && args[0] == "--start") app.Shown += async delegate { await app.StartReceiving(); };
                Application.Run(app);
            }
        }
    }

    public WinDropTray()
    {
        Text = "WinDrop PC — AirDrop prototype";
        ClientSize = new Size(610, 400);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(626, 439);
        status.SetBounds(20, 18, 565, 30); status.Text = "Stopped";
        var instructions = new Label { Text = "On iPhone: Share → AirDrop → WinDrop PC. Approve each transfer here.", AutoSize = false };
        instructions.SetBounds(20, 55, 565, 45);
        start.Text = "Start receiving"; start.SetBounds(20, 104, 155, 36);
        stop.Text = "Stop"; stop.SetBounds(185, 104, 100, 36); stop.Enabled = false;
        var files = new Button { Text = "Open received files" }; files.SetBounds(295, 104, 180, 36);
        history.SetBounds(20, 157, 565, 220); history.Multiline = true; history.ReadOnly = true;
        history.ScrollBars = ScrollBars.Vertical; history.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        Controls.AddRange(new Control[] { status, instructions, start, stop, files, history });
        start.Click += async delegate { await StartReceiving(); };
        stop.Click += async delegate { await StopReceiving(); };
        files.Click += delegate { Directory.CreateDirectory(Path.Combine(folder, "received")); Process.Start("explorer.exe", Quote(Path.Combine(folder, "received"))); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show WinDrop", null, delegate { ShowWindow(); });
        menu.Items.Add("Start receiving", null, async delegate { await StartReceiving(); });
        menu.Items.Add("Stop receiving", null, async delegate { await StopReceiving(); });
        menu.Items.Add("Quit", null, async delegate { if (busy) return; await StopReceiving(); if (ownsSession) return; quitting = true; Close(); });
        tray.Icon = SystemIcons.Information; tray.Text = "WinDrop PC — stopped";
        tray.ContextMenuStrip = menu; tray.Visible = true;
        tray.DoubleClick += delegate { ShowWindow(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (!quitting) { e.Cancel = true; Hide(); } };
        FormClosed += delegate { tray.Visible = false; tray.Dispose(); };
        Log("Connect the TP-Link adapter, then click Start receiving. Closing this window keeps WinDrop in the tray.");
    }

    void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void Log(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine;
        history.AppendText(line);
        try { Directory.CreateDirectory(Path.Combine(folder, "logs")); File.AppendAllText(Path.Combine(folder, "logs", "tray.log"), line, Encoding.UTF8); } catch (IOException) { }
    }
    void OnUi(Action action) { if (!IsDisposed && IsHandleCreated) BeginInvoke(action); }
    void SetStatus(string text) { status.Text = text; tray.Text = ("WinDrop PC — " + text).Substring(0, Math.Min(63, ("WinDrop PC — " + text).Length)); }

    // Win32 argv quoting, including backslashes before a closing quote.
    static string Quote(string value)
    {
        // WSL's option parser treats unnecessarily quoted flags as a shell command.
        if (value.Length > 0 && value.IndexOfAny(new char[] { ' ', '\t', '\n', '\r', '"' }) < 0) return value;
        var text = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { text.Append('\\', slashes * 2 + 1); text.Append('"'); }
            else { text.Append('\\', slashes); text.Append(c); }
            slashes = 0;
        }
        text.Append('\\', slashes * 2); text.Append('"'); return text.ToString();
    }

    static ProcessStartInfo Info(string exe, params string[] args)
    {
        var quoted = new List<string>(); foreach (string arg in args) quoted.Add(Quote(arg));
        var info = new ProcessStartInfo(exe, string.Join(" ", quoted.ToArray()));
        info.UseShellExecute = false; info.CreateNoWindow = true;
        info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        info.StandardOutputEncoding = Encoding.UTF8; info.StandardErrorEncoding = Encoding.UTF8;
        info.EnvironmentVariables["WSL_UTF8"] = "1";
        return info;
    }

    static string Run(string exe, params string[] args)
    {
        using (var process = Process.Start(Info(exe, args)))
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(45000)) { process.Kill(); throw new Exception("Command timed out: " + Path.GetFileName(exe)); }
            Task.WaitAll(output, error);
            if (process.ExitCode != 0) throw new Exception(error.Result + output.Result);
            return output.Result.Trim();
        }
    }

    string Wsl(params string[] args)
    {
        var all = new List<string> { "-d", "AirDropLab", "-u", "root", "--exec" }; all.AddRange(args);
        return Run("wsl.exe", all.ToArray());
    }

    async Task StartReceiving()
    {
        if (busy || ownsSession) return;
        busy = true; start.Enabled = false; SetStatus("Starting…");
        bool failed = false;
        try
        {
            await Task.Run(delegate
            {
                control = Wsl("wslpath", "-u", Path.Combine(folder, "airdrop-lab-control.sh"));
                string script = Wsl("wslpath", "-u", Path.Combine(folder, "airdrop-lab.sh"));
                Wsl("bash", control, "available");
                var state = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Run(usb, "state"));
                Dictionary<string, object> target = null;
                foreach (Dictionary<string, object> device in (IEnumerable)state["Devices"])
                    if (Convert.ToString(device["InstanceId"]) == @"USB\VID_0BDA&PID_8179\00E04C0001") target = device;
                if (target == null || string.IsNullOrEmpty(Convert.ToString(target["BusId"]))) throw new Exception("The expected TP-Link adapter is not plugged in.");
                bus = Convert.ToString(target["BusId"]);
                if (!string.IsNullOrEmpty(Convert.ToString(target["ClientIPAddress"]))) Wsl("bash", control, "attached");
                else Run(usb, "attach", "--wsl", "AirDropLab", "--busid", bus);
                ownsSession = true;
                var info = Info("wsl.exe", "-d", "AirDropLab", "-u", "root", "--exec", "setsid", "--wait", "bash", script, "8188eu", "--ui-events");
                info.RedirectStandardInput = true;
                receiver = new Process { StartInfo = info, EnableRaisingEvents = true };
                receiver.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) OnUi(delegate { HandleLine(e.Data); }); };
                receiver.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) OnUi(delegate { Log(e.Data); }); };
                receiver.Exited += delegate { OnUi(async delegate { if (ownsSession && !busy) { Log("Receiver stopped."); await StopReceiving(); } }); };
                receiver.Start(); receiver.StandardInput.AutoFlush = true; receiver.BeginOutputReadLine(); receiver.BeginErrorReadLine();
            });
            stop.Enabled = true;
        }
        catch (Exception ex)
        {
            Log("Start failed: " + ex.Message); SetStatus("Could not start");
            failed = true;
        }
        finally { busy = false; }
        if (failed || (receiver != null && receiver.HasExited)) { await StopReceiving(); start.Enabled = !ownsSession; }
    }

    async Task StopReceiving()
    {
        if (busy) return;
        if (!ownsSession) { start.Enabled = true; stop.Enabled = false; return; }
        busy = true; stop.Enabled = false; SetStatus("Stopping…");
        if (consent != null) consent.Close();
        try
        {
            await Task.Run(delegate
            {
                Wsl("bash", control, "stop");
                if (receiver != null && !receiver.HasExited && !receiver.WaitForExit(10000)) throw new Exception("Receiver has not stopped; USB remains attached. Try Stop again.");
                // Resolve the serial again: unplugging can change the bus ID.
                var state = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Run(usb, "state"));
                foreach (Dictionary<string, object> device in (IEnumerable)state["Devices"])
                    if (Convert.ToString(device["InstanceId"]) == @"USB\VID_0BDA&PID_8179\00E04C0001" && !string.IsNullOrEmpty(Convert.ToString(device["ClientIPAddress"])))
                        Run(usb, "detach", "--busid", Convert.ToString(device["BusId"]));
            });
            ownsSession = false; if (receiver != null) { receiver.Dispose(); receiver = null; }
            SetStatus("Stopped"); Log("Stopped. The TP-Link adapter is available to Windows again.");
        }
        catch (Exception ex) { Log("Stop failed: " + ex.Message); SetStatus("Stop needs attention"); }
        finally { busy = false; start.Enabled = !ownsSession; stop.Enabled = ownsSession; }
    }

    void HandleLine(string line)
    {
        if (!line.StartsWith("{")) { Log(line); return; }
        try
        {
            var item = json.Deserialize<Dictionary<string, object>>(line);
            string kind = Convert.ToString(item["kind"]);
            if (kind == "ready") { SetStatus("Ready to receive"); Log("WinDrop PC is advertising. Discovery may take a while."); }
            else if (kind == "log") Log(Convert.ToString(item["message"]));
            else if (kind == "completed")
            {
                int count = ((ICollection)item["files"]).Count;
                string text = count + " file(s) saved, " + Convert.ToInt64(item["bytes"]).ToString("N0") + " bytes.";
                SetStatus("Ready to receive"); Log(text); tray.ShowBalloonTip(5000, "AirDrop received", text, ToolTipIcon.Info);
            }
            else if (kind == "consent") ShowConsent(item);
        }
        catch (Exception ex) { Log("UI event error: " + ex.Message); }
    }

    void ShowConsent(Dictionary<string, object> item)
    {
        if (consent != null) return;
        string id = Convert.ToString(item["id"]);
        var names = new StringBuilder(); foreach (object name in (IEnumerable)item["files"]) names.AppendLine(Convert.ToString(name));
        consent = new Form { Text = "Accept AirDrop?", ClientSize = new Size(470, 320), StartPosition = FormStartPosition.CenterParent, Font = Font, TopMost = true };
        var sender = new Label { Text = Convert.ToString(item["sender"]) + " wants to send:", AutoSize = false }; sender.SetBounds(20, 18, 430, 45);
        var list = new TextBox { Text = names.ToString(), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical }; list.SetBounds(20, 70, 430, 150);
        var note = new Label { Text = "Accept only a transfer you expect. Unanswered requests expire in 60 seconds.", AutoSize = false }; note.SetBounds(20, 229, 430, 40);
        var yes = new Button { Text = "Accept", DialogResult = DialogResult.OK }; yes.SetBounds(240, 275, 100, 32);
        var no = new Button { Text = "Decline", DialogResult = DialogResult.Cancel }; no.SetBounds(350, 275, 100, 32);
        consent.Controls.AddRange(new Control[] { sender, list, note, yes, no }); consent.CancelButton = no;
        var timer = new System.Windows.Forms.Timer { Interval = 55000 }; timer.Tick += delegate { if (consent != null) consent.Close(); }; timer.Start();
        SetStatus("Transfer awaiting approval"); ShowWindow();
        bool accepted = consent.ShowDialog(this) == DialogResult.OK; timer.Dispose(); consent.Dispose(); consent = null;
        try { if (receiver != null && !receiver.HasExited) receiver.StandardInput.WriteLine(json.Serialize(new { id = id, accept = accepted })); }
        catch (Exception ex) { Log("Could not send decision: " + ex.Message); }
        SetStatus(accepted ? "Receiving…" : "Ready to receive");
    }
}
