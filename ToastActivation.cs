using System;
using System.Runtime.InteropServices;

[ComImport, Guid("53E31837-6600-4A81-9395-75CFFE746F94"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface INotificationActivationCallback
{
    void Activate([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string invokedArgs, IntPtr input, uint count);
}

[ComVisible(true), Guid("74331E92-9FD9-4BEE-8563-83110C44FF39"), ClassInterface(ClassInterfaceType.None)]
public sealed class ToastActivation : INotificationActivationCallback
{
    public const string ClassId = "{74331E92-9FD9-4BEE-8563-83110C44FF39}";
    public static Action<string> OnActivated;
    public void Activate(string appUserModelId, string invokedArgs, IntPtr input, uint count)
    {
        if (appUserModelId == "WinDrop.PC" && OnActivated != null)
            OnActivated(invokedArgs ?? "windrop://show");
    }
    public static int RegisterServer()
    {
        return new RegistrationServices().RegisterTypeForComClients(typeof(ToastActivation),
            RegistrationClassContext.LocalServer, RegistrationConnectionType.MultipleUse);
    }
    public static void UnregisterServer(int cookie)
    { new RegistrationServices().UnregisterTypeForComClients(cookie); }
}
