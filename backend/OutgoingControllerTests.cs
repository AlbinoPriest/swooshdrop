using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using WinDrop.Cli;
using WinDrop.Protocol;
using WinDrop.Protocol.Discovery;
using WinDrop.Protocol.Tls;

namespace WinDrop.Protocol.Tests;

public class OutgoingControllerTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public async Task SelectedPeerTransferRespectsConsentAndPreservesBytes(bool accept, int transientFailures)
    {
        string directory = Path.Combine(Path.GetTempPath(), "outgoing-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "original.txt");
            byte[] content = Enumerable.Range(0, 10000).Select(i => (byte)(i % 251)).ToArray();
            await File.WriteAllBytesAsync(source, content);
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var transport = new LocalPeer(Path.Combine(directory, "received"), accept, lifetime.Token);
            var events = Channel.CreateUnbounded<JsonElement>();
            var controller = new OutgoingController(transport, value => events.Writer.TryWrite(JsonSerializer.SerializeToElement(value)), lifetime.Token);
            controller.Command(JsonSerializer.SerializeToElement(new { op = "browse" }));
            var peer = await Next(events.Reader, "outgoing-peer", lifetime.Token);
            Assert.Equal("Test iPhone", peer.GetProperty("name").GetString());
            controller.Command(JsonSerializer.SerializeToElement(new { op = "cancel-outgoing" }));
            await Next(events.Reader, "outgoing-idle", lifetime.Token);
            transport.Refusals = transientFailures;
            transport.Sending = true;
            controller.Command(JsonSerializer.SerializeToElement(new { op = "send", peer = peer.GetProperty("id").GetString(), paths = new[] { source } }));
            var result = await Next(events.Reader, accept ? "outgoing-sent" : "outgoing-error", lifetime.Token);
            await Next(events.Reader, "outgoing-idle", lifetime.Token);
            Assert.StartsWith("request POST /Ask ", transport.Requests.First());
            if (accept)
            {
                Assert.Equal(1, result.GetProperty("count").GetInt32());
                string received = Assert.Single(Directory.GetFiles(Path.Combine(directory, "received"), "*", SearchOption.AllDirectories));
                Assert.Equal(content, await File.ReadAllBytesAsync(received));
            }
            else
            {
                Assert.Contains("declined", result.GetProperty("message").GetString());
                Assert.False(Directory.Exists(Path.Combine(directory, "received")) && Directory.GetFiles(Path.Combine(directory, "received"), "*", SearchOption.AllDirectories).Length > 0);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task UnknownPeerCannotTriggerConnection()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var transport = new LocalPeer(Path.GetTempPath(), false, lifetime.Token);
        var events = Channel.CreateUnbounded<JsonElement>();
        var controller = new OutgoingController(transport, value => events.Writer.TryWrite(JsonSerializer.SerializeToElement(value)), lifetime.Token);
        controller.Command(JsonSerializer.SerializeToElement(new { op = "send", peer = "stale", paths = new[] { "/tmp/test" } }));
        var result = await Next(events.Reader, "outgoing-error", lifetime.Token);
        Assert.Contains("Search again", result.GetProperty("message").GetString());
        await Next(events.Reader, "outgoing-idle", lifetime.Token);
        Assert.Equal(0, transport.Connections);
    }

    [Fact]
    public void SelectionRejectsMissingFilesAndDuplicateNames()
    {
        Assert.Throws<ArgumentException>(() => OutgoingController.ValidateFiles(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => OutgoingController.ValidateFiles(new[] { "/missing-file-" + Guid.NewGuid() }));
        string directory = Path.Combine(Path.GetTempPath(), "selection-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(directory, "second"));
        try
        {
            string first = Path.Combine(directory, "same.txt"), second = Path.Combine(directory, "second", "same.txt");
            File.WriteAllText(first, "one"); File.WriteAllText(second, "two");
            Assert.Throws<ArgumentException>(() => OutgoingController.ValidateFiles(new[] { first, second }));
            OutgoingController.ValidateFiles(new[] { first });
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<JsonElement> Next(ChannelReader<JsonElement> events, string kind, CancellationToken ct)
    {
        while (true)
        {
            var item = await events.ReadAsync(ct);
            if (item.GetProperty("kind").GetString() == kind) return item;
            if (item.GetProperty("kind").GetString() == "outgoing-error") throw new Exception(item.ToString());
        }
    }

    private sealed class LocalPeer(string destination, bool accept, CancellationToken lifetime) : IAirDropTransport
    {
        public int Connections;
        public int Refusals;
        public bool Sending;
        public readonly System.Collections.Concurrent.ConcurrentQueue<string> Requests = new();
        private readonly List<Task> servers = new();
        public string Name => "loopback";
        public bool CanReachAppleDevices => false;
        public Task<IAsyncDisposable> AdvertiseAsync(AirDropServiceRecord record, CancellationToken ct = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<AirDropPeer> BrowseAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new AirDropPeer("test-device", new IPEndPoint(IPAddress.Loopback, 8770), AirDropReceiverFlags.SupportsDvZip, Name);
            await Task.Delay(Timeout.Infinite, ct);
        }
        public IAsyncEnumerable<Stream> AcceptAsync(int port, CancellationToken ct = default) => throw new NotSupportedException();
        public async Task<Stream> ConnectAsync(AirDropPeer peer, CancellationToken ct = default)
        {
            Connections++;
            if (Refusals-- > 0) throw new SocketException((int)SocketError.ConnectionRefused);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var server = Task.Run(async () =>
            {
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(lifetime);
                    using var certificate = AirDropCertificate.CreateSelfSigned("Test iPhone");
                    await using var ssl = await AirDropTls.AuthenticateAsServerAsync(client.GetStream(), certificate, lifetime);
                    var receiver = new AirDropReceiver(new AirDropReceiverOptions { ComputerName = "Test iPhone", DownloadDirectory = destination,
                        ConsentHandler = (_, _) => Task.FromResult(accept), EarlyAskReply = false,
                        Log = message => { if (Sending && message.StartsWith("request ")) Requests.Enqueue(message); } });
                    await receiver.HandleConnectionAsync(ssl, lifetime);
                }
                finally { listener.Stop(); }
            }, lifetime);
            servers.Add(server);
            var tcp = new TcpClient();
            await tcp.ConnectAsync((IPEndPoint)listener.LocalEndpoint, ct);
            return tcp.GetStream();
        }
        public async ValueTask DisposeAsync() { await Task.WhenAll(servers); }
    }
}
