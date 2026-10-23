using System.Net;
using System.Net.Sockets;
using WinDrop.Protocol.Discovery;
using WinDrop.Protocol.Http;
using WinDrop.Protocol.Plist;
using WinDrop.Protocol.Tls;

namespace WinDrop.Protocol.Tests;

public class SenderWireTests
{
    [Fact]
    public async Task UploadRepeatsTheTransferIdAcceptedInAsk()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string file = Path.Combine(Path.GetTempPath(), "sender-wire-" + Guid.NewGuid() + ".txt");
        await File.WriteAllTextAsync(file, "original test content", ct);
        try
        {
            var server = Task.Run(async () =>
            {
                using var tcp = await listener.AcceptTcpClientAsync(ct);
                using var certificate = AirDropCertificate.CreateSelfSigned("wire-test");
                await using var ssl = await AirDropTls.AuthenticateAsServerAsync(tcp.GetStream(), certificate, ct);
                await using var http = new HttpConnection(ssl, false);
                var ask = await http.ReadRequestHeadAsync(ct);
                Assert.Equal("/Ask", ask!.Target);
                var plist = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(BinaryPlistReader.Parse(await http.ReadBodyAsync(ask.Headers, 1048576, ct)));
                var transfer = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(plist["TransferID"]);
                string id = Assert.IsType<string>(transfer["id"]);
                Assert.True(Guid.TryParse(id, out _));
                var type = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(plist["TransferType"]);
                Assert.True(type.ContainsKey("files"));
                await http.WriteResponseAsync(200, "OK", new HttpHeaders(), Array.Empty<byte>(), ct);
                var upload = await http.ReadRequestHeadAsync(ct);
                Assert.Equal("/Upload", upload!.Target);
                Assert.Equal(id, upload.Headers["TransferID"]);
                Assert.Equal(new FileInfo(file).Length.ToString(), upload.Headers["TotalBytes"]);
                Assert.Equal("application/x-dvzip", upload.Headers["Content-Type"]);
                Assert.True(upload.Headers.IsChunked);
                Assert.NotEmpty(await http.ReadBodyAsync(upload.Headers, 1048576, ct));
                await http.WriteResponseAsync(200, "OK", new HttpHeaders(), Array.Empty<byte>(), ct);
            }, ct);
            using var client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, ct);
            using var clientCertificate = AirDropCertificate.CreateSelfSigned("sender");
            await using var clientSsl = await AirDropTls.AuthenticateAsClientAsync(client.GetStream(), clientCertificate, ct);
            await using var sender = new AirDropSenderSession(clientSsl, AirDropReceiverFlags.SupportsDvZip);
            var selected = AirDropOutgoingFile.FromPath(file);
            Assert.True(await sender.AskAsync(new AirDropAskRequest("SwooshDrop", "Windows", "sender", AirDropAskRequest.FinderBundleId, new[] { selected.ToEntry() }), ct));
            await sender.UploadAsync(new[] { selected }, ct: ct);
            await server;
        }
        finally { listener.Stop(); File.Delete(file); }
    }
}
