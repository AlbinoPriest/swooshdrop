using System.Net;
using WinDrop.Protocol.Discovery;

namespace WinDrop.Protocol.Tests;

public class DiscoveryCycleTests
{
    [Fact]
    public void FreshSearchCanReportSamePhoneAgainWithoutReportingOurselves()
    {
        var assembler = new ServiceAssembler("test") { OwnInstance = "SwooshDrop" };
        var phone = AirDropRecords.BuildAnnouncement(new AirDropServiceRecord("phone", 8770, AirDropReceiverFlags.None), "phone.local", new[] { IPAddress.Parse("fe80::1234") });
        var own = AirDropRecords.BuildAnnouncement(new AirDropServiceRecord("SwooshDrop", 8770, AirDropReceiverFlags.None), "self.local", new[] { IPAddress.Parse("fe80::5678") });
        Assert.Single(assembler.Absorb(phone, 1));
        Assert.Empty(assembler.Absorb(phone, 1));
        assembler.Reset();
        Assert.Empty(assembler.Absorb(own, 1));
        Assert.Single(assembler.Absorb(phone, 1));
    }
}
