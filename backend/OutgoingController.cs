using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using WinDrop.Protocol;
using WinDrop.Protocol.Discovery;
using WinDrop.Protocol.Tls;

namespace WinDrop.Cli;

// Shares the receiver's transport: opening a second BridgeTransport would replace
// the bridge controller and interrupt incoming transfers.
public sealed class OutgoingController(IAirDropTransport transport, Action<object> emit, CancellationToken lifetime)
{
    private readonly ConcurrentDictionary<string, (AirDropPeer Peer, DateTime Seen)> peers = new();
    private CancellationTokenSource? operation;
    private readonly object gate = new();

    public void Command(JsonElement command)
    {
        string? op = command.GetProperty("op").GetString();
        lock (gate)
        {
            if (op == "cancel-outgoing") { operation?.Cancel(); return; }
            if (operation != null) { emit(new { kind = "outgoing-error", message = "Another outgoing operation is running." }); return; }
            if (op != "browse" && op != "send") return;
            operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            operation.CancelAfter(op == "browse" ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5));
            var snapshot = command.Clone();
            var active = operation;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (op == "browse") await BrowseAsync(active.Token);
                    else await SendAsync(snapshot, active.Token);
                }
                catch (OperationCanceledException) { emit(new { kind = "outgoing-status", message = op == "browse" ? "Search finished. Choose a device, or search again." : "Sending cancelled or timed out." }); }
                catch (Exception ex) { emit(new { kind = "outgoing-error", message = ex.Message }); }
                finally
                {
                    lock (gate) { operation = null; active.Dispose(); }
                    emit(new { kind = "outgoing-idle" });
                }
            });
        }
    }

    private async Task BrowseAsync(CancellationToken ct)
    {
        peers.Clear();
        emit(new { kind = "outgoing-status", message = "Looking for nearby devices…" });
        await foreach (var peer in transport.BrowseAsync(ct))
        {
            string key = peer.InstanceName;
            if (!peers.TryAdd(key, (peer, DateTime.UtcNow))) continue;
            string name = peer.InstanceName;
            // Resolve the human-readable name, but a sleepy phone must not stall
            // discovery indefinitely. Keep its service name as a fallback.
            using var probe = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probe.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                using var certificate = AirDropCertificate.CreateSelfSigned("SwooshDrop");
                await using var raw = await transport.ConnectAsync(peer, probe.Token);
                await using var ssl = await AirDropTls.AuthenticateAsClientAsync(raw, certificate, probe.Token);
                await using var session = new AirDropSenderSession(ssl, peer.Flags);
                var identity = await session.DiscoverAsync(probe.Token);
                if (!string.IsNullOrWhiteSpace(identity?.ComputerName)) name = identity.ComputerName;
            }
            catch (Exception) when (!ct.IsCancellationRequested) { }
            emit(new { kind = "outgoing-peer", id = key, name });
        }
    }

    private async Task SendAsync(JsonElement command, CancellationToken ct)
    {
        string key = command.GetProperty("peer").GetString() ?? "";
        if (!peers.TryGetValue(key, out var target) || DateTime.UtcNow - target.Seen > TimeSpan.FromMinutes(2))
            throw new InvalidOperationException("This device is no longer current. Search again before sending.");
        string[] paths = command.GetProperty("paths").EnumerateArray().Select(p => p.GetString() ?? "").ToArray();
        ValidateFiles(paths);
        var files = paths.Select(p => new AirDropOutgoingFile(p, Path.GetFileName(p), FileType(p))).ToList();
        long total = files.Sum(f => f.TotalBytes);
        using var certificate = AirDropCertificate.CreateSelfSigned("SwooshDrop");
        emit(new { kind = "outgoing-status", message = "Connecting to your device…" });
        await using var raw = await ConnectForSendAsync(target.Peer, ct);
        await using var ssl = await AirDropTls.AuthenticateAsClientAsync(raw, certificate, ct);
        await using var session = new AirDropSenderSession(ssl, target.Peer.Flags);
        session.Log = message => emit(new { kind = "log", message = "Sender: " + message });
        // Discovery already resolved the selected recipient on a separate probe.
        // Start this transfer connection with /Ask, as OpenDrop does. Reusing
        // /Discover's connection is not required for the outgoing transfer.
        var request = new AirDropAskRequest("SwooshDrop", "Windows", Guid.NewGuid().ToString(),
            AirDropAskRequest.FinderBundleId, files.Select(f => f.ToEntry()).ToList());
        emit(new { kind = "outgoing-status", message = "Requesting approval from your iPhone…" });
        using var consent = CancellationTokenSource.CreateLinkedTokenSource(ct);
        consent.CancelAfter(TimeSpan.FromSeconds(90));
        bool accepted;
        try { accepted = await session.AskAsync(request, consent.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new TimeoutException("The iPhone did not answer the AirDrop request within 90 seconds."); }
        if (!accepted) throw new InvalidOperationException("The device declined this transfer.");
        emit(new { kind = "outgoing-status", message = "Sending original files…" });
        await session.UploadAsync(files, new TransferProgress(sent => emit(new { kind = "outgoing-progress", sent, total })), ct);
        emit(new { kind = "outgoing-sent", count = paths.Length, bytes = total });
    }

    public static void ValidateFiles(string[] paths)
    {
        if (paths.Length == 0 || paths.Length > 100) throw new ArgumentException("Choose between 1 and 100 files.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Choose existing regular files; folders and shortcuts are not supported yet.");
            if (!names.Add(Path.GetFileName(path))) throw new ArgumentException("Files with the same name must be sent separately.");
        }
    }

    private async Task<Stream> ConnectForSendAsync(AirDropPeer peer, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await transport.ConnectAsync(peer, ct); }
            catch (Exception ex) when (attempt < 2 && !ct.IsCancellationRequested &&
                ex is BridgeException or IOException or System.Net.Sockets.SocketException)
            {
                // Retry only opening TCP, before TLS, consent or file bytes. A
                // waking receiver can briefly refuse connections; uploads must
                // never be retried automatically after an ambiguous outcome.
                emit(new { kind = "outgoing-status", message = "Waiting for your device to wake…" });
                await Task.Delay(1200, ct);
            }
        }
    }

    private static string FileType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "public.jpeg", ".png" => "public.png", ".heic" => "public.heic",
        ".pdf" => "com.adobe.pdf", ".txt" => "public.plain-text", ".mp4" => "public.mpeg-4",
        ".mov" => "com.apple.quicktime-movie", _ => AirDropFileEntry.DefaultFileType
    };

    private sealed class TransferProgress(Action<long> report) : IProgress<long>
    {
        private long last;
        public void Report(long value)
        {
            long now = Environment.TickCount64;
            if (now - last < 200) return;
            last = now; report(value);
        }
    }
}
