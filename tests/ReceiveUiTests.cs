using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

class ReceiveUiTests
{
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type Type = typeof(WinDropTray);
    static object controller;
    static void Set(string field, object value) { Type.GetField(field, Private).SetValue(controller, value); }
    static object Get(string field) { return Type.GetField(field, Private).GetValue(controller); }
    static void Line(string value) { Type.GetMethod("HandleLine", Private).Invoke(controller, new object[] { value }); }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Pump()
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        timer.Tick += delegate { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    static Dictionary<string, object> Pending()
    { return new Dictionary<string, object> { { "id", "approval-b" }, { "connection", "connection-b" } }; }
    static void Setup()
    {
        Set("incoming", Pending()); Set("transferInProgress", true); Set("activeReceiveConnection", "connection-a");
        Set("ownsSession", true); Set("receiverReady", true);
    }
    [STAThread]
    static int Main()
    {
        var app = new Application();
        try
        {
            string data = Path.Combine(Path.GetTempPath(), "SwooshDrop-ui-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(data); File.WriteAllText(Path.Combine(data, "settings.json"), "{\"Language\":\"en-US\",\"NotificationMode\":\"app\",\"ReceiveOnLaunch\":false}");
            controller = Type.GetConstructor(Private, null, new[] { typeof(string), typeof(bool) }, null).Invoke(new object[] { data, true });
            Setup();
            Line("{\"kind\":\"log\",\"message\":\"Connection failed: unrelated TLS discovery failure\"}"); Pump();
            Assert(Get("incoming") != null && (bool)Get("transferInProgress"), "An unscoped log changed another transfer's consent or presentation.");
            Line("{\"kind\":\"transfer-failed\",\"connection\":\"discovery-c\",\"message\":\"connection reset\"}"); Pump();
            Assert(Get("incoming") != null && (bool)Get("transferInProgress"), "Unrelated structured failure dismissed an active transfer.");
            Line("{\"kind\":\"transfer-failed\",\"connection\":\"connection-a\",\"message\":\"connection reset\"}"); Pump();
            Assert(Get("incoming") != null && !(bool)Get("transferInProgress"), "Matching upload failure did not preserve a separate pending approval.");
            Setup();
            Line("{\"kind\":\"transfer-failed\",\"connection\":\"connection-b\",\"message\":\"connection reset\"}");
            Assert(Get("incoming") == null && (bool)Get("transferInProgress"), "Failure of the pending peer affected another active upload.");
            Setup();
            Line("{\"kind\":\"transfer-failed\",\"connection\":\"connection-a\",\"message\":\"connection reset\"}");
            Set("activeReceiveConnection", "replacement-transfer"); Pump();
            Assert((bool)Get("transferInProgress"), "A delayed failure incorrectly interrupted a replacement transfer.");
            Console.WriteLine("PASS: unrelated errors, matching failures, separate consent, and stale failure timers.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { app.Shutdown(); }
    }
}
