using System;
using System.Collections.Generic;
using System.IO;

public static class SetupTests
{
    static void Assert(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    static Dictionary<string, object> Device(string id, string bus) { return new Dictionary<string, object> { { "InstanceId", id }, { "BusId", bus } }; }
    public static int Main()
    {
        try
        {
            string serial = @"USB\VID_0BDA&PID_8179\00E04C0001", location = @"USB\VID_148F&PID_7601\6&42D09BA&0&1";
            string catalog = SetupEngine.ResourceText("Setup.adapters.json");
            Assert(AdapterIdentity.Parse(serial).Serial == "00E04C0001", "Real USB serial was lost");
            Assert(AdapterIdentity.Parse(location).Serial == "", "Windows location token must not become a USB serial");
            Assert(AdapterCatalog.Profile(serial.ToLowerInvariant(), catalog).Driver == "8188eu", "Tested chipset must use the validated driver");
            bool rejected = false; try { AdapterIdentity.Parse(@"PCI\VEN_8086&DEV_2725\123"); } catch (ArgumentException) { rejected = true; } Assert(rejected, "Built-in PCI internet adapter must be rejected");
            var devices = new[] { Device(serial, "2-2"), Device(location, "2-4") };
            rejected = false; try { AdapterCatalog.Select(devices, null, catalog); } catch (InvalidOperationException) { rejected = true; } Assert(rejected, "Multiple radios require an explicit selection");
            Assert(AdapterCatalog.Select(devices, location, catalog)["BusId"].ToString() == "2-4", "Explicit selection chose the wrong USB device");
            Assert(AdapterCatalog.Select(devices, @"USB\VID_0BDA&PID_8179\MISSING", catalog) == null, "A missing selected adapter must not silently switch to another radio");
            string file = Path.GetTempFileName(); try { File.WriteAllText(file, "changed installer"); Assert(!SetupEngine.Verified(file, SetupEngine.UsbHash), "Tampered prerequisite accepted"); } finally { File.Delete(file); }
            Assert(SetupEngine.Quote("--system") == "--system", "WSL option quoting is incompatible");
            Assert(SetupEngine.Quote(@"C:\folder name\") == "\"C:\\folder name\\\\\"", "Trailing slash argument is not escaped");
            Console.WriteLine("PASS: identity, chipset routing, PCI rejection, multiple-device selection, missing-device behavior, prerequisite checksum, and WSL argument quoting."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
