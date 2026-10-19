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
            I18n.SetLanguage("en-US");
            Assert(I18n.Text("接受") == "Accept", "Language switching did not restore English");
            Console.WriteLine("PASS: settings migration, Unicode device names, UTF-8 limit, and zh-TW/en-US localization.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
