using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Riftbound.GodotClient;

using (var own = JsonDocument.Parse("""
    {"cardNo":"SFD·125/221","isFaceDown":true,"ownerId":"p1","controllerId":"p1","effectivePower":99,"damage":88}
    """))
{
    var visible = SnapshotCardRef.FromSnapshot("own-standby", own.RootElement, "p1");
    var hidden = SnapshotCardRef.FromSnapshot("enemy-standby", own.RootElement, "p2");
    var spectator = SnapshotCardRef.FromSnapshot("spectator-standby", own.RootElement, "");
    if (!visible.Visible || !visible.FaceDown || visible.CardNo != "SFD·125/221" || visible.CurrentPower is not null
        || hidden.Visible || hidden.CardNo != "" || hidden.CurrentPower is not null || hidden.Damage != 0
        || spectator.Visible || spectator.CardNo != "")
        throw new InvalidOperationException("Native standby identity boundary failed.");
}
using (var unit = JsonDocument.Parse("""
    {"cardNo":"SFD·125/221","ownerId":"p1","tags":["CARD_TYPE:UNIT"],"location":{"zone":"BASE"},"effectivePower":7,"damage":2}
    """))
{
    var visible = SnapshotCardRef.FromSnapshot("public-unit", unit.RootElement, "p2");
    if (!visible.Visible || visible.CurrentPower != 7 || visible.Damage != 2)
        throw new InvalidOperationException("Native card ignored authoritative power or damage.");
}
Console.WriteLine("Native snapshot projection passed: own standby, opponent/spectator redaction, authoritative public stats.");

var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL") ?? "http://127.0.0.1:15089";
var upstream = new Uri(url);
if (upstream.Scheme != "http" || !upstream.IsLoopback) throw new InvalidOperationException("This gate requires a local test API.");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
await using var proxy = new DropProxy(upstream, deadline.Token);
await using var client = new RiftboundGameHubClient(proxy.Url);
var suffix = Guid.NewGuid().ToString("N");
var handle = "native" + suffix[..12];
var key = "native-network-test-" + suffix;
var room = "NETWORK-" + suffix[..12];
string? reconnectToken = null;
var snapshots = 0;
var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var rejected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var errors = new ConcurrentQueue<string>();
var recoveryCalls = 0;
var failRecovery = false;
client.ServerMessageReceived += (kind, message) =>
{
    if (kind == "Joined" && message.Payload is JsonElement payload)
    {
        reconnectToken = payload.GetProperty("reconnectToken").GetString();
        joined.TrySetResult();
    }
    if (kind == "Snapshot") Interlocked.Increment(ref snapshots);
    if (kind == "Error") errors.Enqueue("Server rejected a recovery request.");
};
client.StatusChanged += status =>
{
    if (status == "Reconnecting" && client.IsConnected) throw new InvalidOperationException("Stale session is still actionable.");
    if (status == "Connected") restored.TrySetResult();
    if (status == "Recovery failed") rejected.TrySetResult();
};
client.RestoreSession += async () =>
{
    Interlocked.Increment(ref recoveryCalls);
    var auth = await client.AuthenticateAsync(handle, key, deadline.Token);
    if (!auth.Authenticated) throw new InvalidOperationException("Recovery authentication failed.");
    await client.ReconnectAsync(room, auth.Handle, failRecovery ? "invalid-test-token" : reconnectToken!, deadline.Token);
    await client.RequestSnapshotAsync(room, auth.Handle, deadline.Token);
};
await client.StartAsync(deadline.Token);
var authenticated = await client.AuthenticateAsync(handle, key, deadline.Token);
if (!authenticated.Authenticated) throw new InvalidOperationException("Initial authentication failed.");
await client.JoinRoomAsync(room, authenticated.Handle, null, deadline.Token);
await joined.Task.WaitAsync(deadline.Token);
await client.RequestSnapshotAsync(room, authenticated.Handle, deadline.Token);
var initialSnapshots = Volatile.Read(ref snapshots);
proxy.DropConnections();
await restored.Task.WaitAsync(deadline.Token);
if (!client.IsConnected || recoveryCalls != 1 || snapshots <= initialSnapshots || !errors.IsEmpty)
    throw new InvalidOperationException("Native client did not restore authentication, room membership and a fresh snapshot.");
// An invalid reconnect receipt must never turn a transport connection into a usable session.
failRecovery = true;
proxy.DropConnections();
await rejected.Task.WaitAsync(deadline.Token);
if (client.IsConnected || recoveryCalls != 2 || errors.IsEmpty)
    throw new InvalidOperationException("Rejected recovery incorrectly became actionable.");
Console.WriteLine("Native network recovery passed: socket loss, authenticated room restore, fresh snapshot, rejected-token guard.");

sealed class DropProxy : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentDictionary<TcpClient, byte> sockets = new();
    private readonly CancellationTokenSource stop;
    private readonly Task acceptLoop;
    private readonly Uri upstream;
    public string Url { get; }
    public DropProxy(Uri upstream, CancellationToken cancellationToken)
    {
        this.upstream = upstream;
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listener.Start();
        Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        acceptLoop = AcceptAsync();
    }
    private async Task AcceptAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var incoming = await listener.AcceptTcpClientAsync(stop.Token);
                _ = ForwardAsync(incoming);
            }
        }
        catch (Exception) when (stop.IsCancellationRequested) { }
    }
    private async Task ForwardAsync(TcpClient incoming)
    {
        using var outgoing = new TcpClient();
        using (incoming)
        {
            sockets.TryAdd(incoming, 0);
            sockets.TryAdd(outgoing, 0);
            try
            {
                await outgoing.ConnectAsync(upstream.Host, upstream.Port, stop.Token);
                var toServer = incoming.GetStream().CopyToAsync(outgoing.GetStream(), stop.Token);
                var toClient = outgoing.GetStream().CopyToAsync(incoming.GetStream(), stop.Token);
                await Task.WhenAny(toServer, toClient);
                incoming.Close(); outgoing.Close();
                try { await Task.WhenAll(toServer, toClient); } catch (IOException) { }
            }
            catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
            finally { sockets.TryRemove(incoming, out _); sockets.TryRemove(outgoing, out _); }
        }
    }
    public void DropConnections()
    {
        foreach (var socket in sockets.Keys) socket.Close();
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop(); DropConnections();
        await acceptLoop;
        stop.Dispose();
    }
}
