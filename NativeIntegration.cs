using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

public static class NativeIntegration
{
    const string ToastClassId = "{74331E92-9FD9-4BEE-8563-83110C44FF39}";
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    public static void Register(string executable)
    {
        SetCurrentProcessExplicitAppUserModelID("WinDrop.PC");
        using (var protocol = Registry.CurrentUser.CreateSubKey(@"Software\Classes\windrop"))
        {
            protocol.SetValue("", "URL:SwooshDrop"); protocol.SetValue("URL Protocol", "");
            using (var command = protocol.CreateSubKey(@"shell\open\command")) command.SetValue("", "\"" + executable + "\" \"%1\"");
            using (var icon = protocol.CreateSubKey("DefaultIcon")) icon.SetValue("", "\"" + executable + "\",0");
        }
        using (var app = Registry.CurrentUser.CreateSubKey(@"Software\Classes\AppUserModelId\WinDrop.PC"))
        { app.SetValue("DisplayName", "SwooshDrop"); app.SetValue("IconUri", executable); app.SetValue("CustomActivator", ToastClassId); }
        using (var callback = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\" + ToastClassId))
        {
            callback.SetValue("", "SwooshDrop notification callback");
            using (var server = callback.CreateSubKey("LocalServer32"))
            {
                server.SetValue("", "\"" + executable + "\" --toast-server");
                server.SetValue("ServerExecutable", executable);
            }
        }
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SwooshDrop.lnk");
        var shell = (IShellLinkW)new ShellLink();
        try
        {
            shell.SetPath(executable); shell.SetDescription("Receive AirDrop on Windows"); shell.SetWorkingDirectory(Path.GetDirectoryName(executable)); shell.SetIconLocation(executable, 0);
            var store = (IPropertyStore)shell;
            var key = new PropertyKey { FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), PropertyId = 5 };
            var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni("WinDrop.PC") };
            try { store.SetValue(ref key, ref value); store.Commit(); } finally { Marshal.FreeCoTaskMem(value.Pointer); }
            key.PropertyId = 26;
            value = new PropVariant { Type = 72, Pointer = Marshal.AllocCoTaskMem(16) };
            Marshal.Copy(new Guid(ToastClassId).ToByteArray(), 0, value.Pointer, 16);
            try { store.SetValue(ref key, ref value); store.Commit(); } finally { Marshal.FreeCoTaskMem(value.Pointer); }
            ((IPersistFile)shell).Save(shortcut, true);
        }
        finally { Marshal.ReleaseComObject(shell); }
        // Explorer caches unknown URI schemes. Refresh its cache after first registration.
        SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    }
    public static bool IsAutostartEnabled()
    { using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return key != null && (key.GetValue("SwooshDrop") != null || key.GetValue("WinDrop") != null); }
    public static void SetAutostart(string executable, bool enabled)
    {
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
        { key.DeleteValue("WinDrop", false); if (enabled) key.SetValue("SwooshDrop", "\"" + executable + "\" --background"); else key.DeleteValue("SwooshDrop", false); }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class ShellLink { }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int count, IntPtr findData, uint flags);
        void GetIDList(out IntPtr pidl); void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey); void SetHotkey(short hotkey);
        void GetShowCmd(out int show); void SetShowCmd(int show);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid FormatId; public uint PropertyId; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    interface IPropertyStore
    {
        void GetCount(out uint count); void GetAt(uint index, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        void SetValue(ref PropertyKey key, ref PropVariant value); void Commit();
    }
}
