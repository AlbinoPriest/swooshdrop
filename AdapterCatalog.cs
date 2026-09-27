using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public sealed class AdapterProfile
{
    public string Vendor { get; set; }
    public string Product { get; set; }
    public string Driver { get; set; }
    public string Status { get; set; }
}
public sealed class AdapterIdentity
{
    public string InstanceId, Vendor, Product, Serial;
    public static AdapterIdentity Parse(string instance)
    {
        var match = Regex.Match(instance ?? "", @"^USB\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})(?:&[A-Z0-9_]+)*\\([^\\]{1,128})$", RegexOptions.IgnoreCase);
        if (!match.Success) throw new ArgumentException("Select an external USB Wi-Fi adapter.");
        string serial = match.Groups[3].Value;
        return new AdapterIdentity { InstanceId = instance, Vendor = match.Groups[1].Value.ToLowerInvariant(), Product = match.Groups[2].Value.ToLowerInvariant(), Serial = serial.IndexOf('&') >= 0 ? "" : serial };
    }
}
public static class AdapterCatalog
{
    public static AdapterProfile Profile(string instance, string catalogJson)
    {
        var identity = AdapterIdentity.Parse(instance);
        foreach (var profile in new JavaScriptSerializer().Deserialize<List<AdapterProfile>>(catalogJson))
            if (string.Equals(profile.Vendor, identity.Vendor, StringComparison.OrdinalIgnoreCase) && string.Equals(profile.Product, identity.Product, StringComparison.OrdinalIgnoreCase)) return profile;
        return null;
    }
    public static Dictionary<string, object> Select(IEnumerable devices, string selected, string catalogJson)
    {
        var supported = new List<Dictionary<string, object>>();
        foreach (Dictionary<string, object> device in devices)
        {
            string identity = Convert.ToString(device["InstanceId"]);
            if (!string.IsNullOrEmpty(selected)) { if (string.Equals(identity, selected, StringComparison.OrdinalIgnoreCase)) return device; continue; }
            try { if (Profile(identity, catalogJson) != null && !string.IsNullOrEmpty(Convert.ToString(device["BusId"]))) supported.Add(device); } catch (ArgumentException) { }
        }
        if (supported.Count > 1) throw new InvalidOperationException("More than one compatible adapter is connected. Select one in WinDrop Setup.");
        return supported.Count == 1 ? supported[0] : null;
    }
}
