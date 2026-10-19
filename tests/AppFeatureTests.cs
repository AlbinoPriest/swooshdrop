using System;
using System.Text;

public static class AppFeatureTests
{
    static void Assert(bool ok, string reason) { if (!ok) throw new Exception(reason); }

    public static int Main()
    {
        try
        {
            string value, error;
            Assert(SettingsPolicy.TryNormalizeDeviceName("ざこ～♡", out value, out error), "Expected Unicode AirDrop name to be accepted");
            Assert(value == "ざこ～♡", "Unicode AirDrop name changed during normalization");
            Assert(!SettingsPolicy.TryNormalizeDeviceName("   ", out value, out error), "Blank AirDrop name was accepted");
            Assert(!SettingsPolicy.TryNormalizeDeviceName("bad\nname", out value, out error), "Control character was accepted");
            Assert(!SettingsPolicy.TryNormalizeDeviceName(new string('界', 22), out value, out error), "UTF-8 byte limit was not enforced");
            Assert(Encoding.UTF8.GetByteCount(new string('界', 21)) == 63, "UTF-8 test boundary is invalid");
            Assert(SettingsPolicy.TryNormalizeDeviceName(new string('界', 21), out value, out error), "63-byte name was rejected");
            var old = new Preferences { Language = null, DeviceName = null, SaveFolder = null, RuntimeDistro = null };
            Assert(SettingsPolicy.Normalize(old), "Legacy settings migration reported no change");
            Assert(old.DeviceName == "SwooshDrop" && old.RuntimeDistro == "WinDropRuntime", "Legacy defaults were not restored");
            I18n.SetLanguage("zh-TW");
            Assert(I18n.Text("Accept") == "接受", "Traditional Chinese translation failed");
            Assert(I18n.Format("Choose {0} on your iPhone.", "ざこ～♡").Contains("ざこ～♡"), "Localized format dropped the device name");
            Assert(I18n.Text("Restart receiving now?") == "要立即重新啟用接收嗎？", "Restart confirmation was not localized");
            Assert(I18n.Text("Applying this change now will stop any transfer currently in progress. Choose No to apply it the next time receiving starts.").Contains("中止目前進行中的傳輸"), "Restart warning omitted the active-transfer risk");
            Assert(I18n.Format("Receiving from {0}", "Natsu’s iPhone").Contains("Natsu’s iPhone"), "Transfer progress dropped the sender name");
            Assert(I18n.Format("File {0} of {1} · {2}% · {3} / {4}", 2, 3, 50, "1.5 MB", "3.0 MB").Contains("50%"), "Transfer progress formatting failed");
            string reason;
            Assert(TransferFailurePolicy.TryDescribe("Connection failed: Received an unexpected EOF or 0 bytes from the transport stream.", out reason), "EOF transfer failure was not detected");
            Assert(reason == "The iPhone or wireless link closed the connection.", "EOF transfer failure had the wrong reason");
            Assert(TransferFailurePolicy.TryDescribe("Connection failed: Connection closed before a chunk header.", out reason), "Chunk failure was not detected");
            Assert(TransferFailurePolicy.TryDescribe("Connection failed: The operation timed out.", out reason) && reason.Contains("no data"), "Timeout transfer failure had the wrong reason");
            Assert(!TransferFailurePolicy.TryDescribe("Upload complete.", out reason), "A successful transfer was treated as a failure");
            Assert(I18n.Text("The iPhone or wireless link closed the connection.").Contains("iPhone"), "Transfer failure reason was not localized");
            I18n.SetLanguage("en-US");
            Assert(I18n.Text("接受") == "Accept", "Language switching did not restore English");
            Console.WriteLine("PASS: settings migration, transfer failure recovery messages, Unicode device names, UTF-8 limit, and zh-TW/en-US localization.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
