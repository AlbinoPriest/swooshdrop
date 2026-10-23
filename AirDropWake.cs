using System;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

// A short-lived Continuity wake beacon. It contains no contact identifiers and
// only runs while the user explicitly searches for or sends to a recipient.
public sealed class AirDropWake : IDisposable
{
    BluetoothLEAdvertisementPublisher publisher;
    readonly Action<string> status;

    public AirDropWake(Action<string> onStatus)
    {
        status = onStatus;
        var advertisement = new BluetoothLEAdvertisement();
        using (var writer = new DataWriter())
        {
            // AirDrop TLV: type 0x05, 18 bytes, reserved prefix, version 1,
            // empty contact hashes and terminator. Everyone mode is required.
            byte[] payload = new byte[20]; payload[0] = 5; payload[1] = 18; payload[10] = 1;
            writer.WriteBytes(payload);
            advertisement.ManufacturerData.Add(new BluetoothLEManufacturerData(0x004c, writer.DetachBuffer()));
        }
        publisher = new BluetoothLEAdvertisementPublisher(advertisement);
        publisher.StatusChanged += Changed;
        publisher.Start();
    }

    void Changed(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
    { status("Bluetooth wake: " + args.Status + (args.Status == BluetoothLEAdvertisementPublisherStatus.Aborted ? " (" + args.Error + ")" : "")); }

    public void Dispose()
    {
        if (publisher == null) return;
        publisher.StatusChanged -= Changed;
        publisher.Stop(); publisher = null;
    }
}
