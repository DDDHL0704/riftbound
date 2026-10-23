using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Riftbound.Contracts;
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
if (args.Length == 2 && args[0] is "--payment-resource-surplus-play" or "--payment-resource-surplus-recover")
{
    await PaymentResourceSurplusNetworkAcceptance.RunAsync(url, args[1], args[0] == "--payment-resource-surplus-recover");
    return;
}
if (args.Length != 0) throw new ArgumentException("Unknown native network acceptance selector.");
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
await using var proxy = new DropProxy(upstream, deadline.Token);
await using var client = new RiftboundGameHubClient(proxy.Url);
var suffix = Guid.NewGuid().ToString("N");
var handle = "NaTiVe" + suffix[..12];
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
    if ((kind is "Joined" or "Snapshot" or "Prompt")
        && message.PlayerId != handle.ToLowerInvariant())
        errors.Enqueue("Room response did not use the authenticated canonical actor.");
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
    await client.ReconnectAsync(room, handle.ToUpperInvariant(), failRecovery ? "invalid-test-token" : reconnectToken!, deadline.Token);
    await client.RequestSnapshotAsync(room, " " + handle + " ", deadline.Token);
};
await client.StartAsync(deadline.Token);
var authenticated = await client.AuthenticateAsync(handle, key, deadline.Token);
if (!authenticated.Authenticated) throw new InvalidOperationException("Initial authentication failed.");
await client.JoinRoomAsync(room, " " + handle + " ", null, deadline.Token);
await joined.Task.WaitAsync(deadline.Token);
await client.RequestSnapshotAsync(room, authenticated.Handle, deadline.Token);
var initialSnapshots = Volatile.Read(ref snapshots);
await VerifyRoomIdentityBoundaryAsync(url, room, handle, reconnectToken!, deadline.Token);
proxy.DropConnections();
await restored.Task.WaitAsync(deadline.Token);
if (!client.IsConnected || recoveryCalls != 1 || snapshots <= initialSnapshots || !errors.IsEmpty)
    throw new InvalidOperationException("Native client did not restore authentication, room membership and a fresh snapshot.");
Console.WriteLine("Native canonical room identity passed: mixed-case join, canonical snapshot, uppercase reconnect, spaced snapshot; original private identity boundary retained.");
// An invalid reconnect receipt must never turn a transport connection into a usable session.
failRecovery = true;
proxy.DropConnections();
await rejected.Task.WaitAsync(deadline.Token);
if (client.IsConnected || recoveryCalls != 2 || errors.IsEmpty)
    throw new InvalidOperationException("Rejected recovery incorrectly became actionable.");
Console.WriteLine("Native network recovery passed: socket loss, authenticated room restore, fresh snapshot, rejected-token guard.");
await VerifyRoomSeatPeerPushAsync(url, handle, key);

static async Task VerifyRoomSeatPeerPushAsync(string url, string originalActor, string originalKey)
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var ct = timeout.Token;
    var suffix = Guid.NewGuid().ToString("N");
    var room = "NETWORK-SEAT-" + suffix[..12];
    var peerActor = "seatpeer" + suffix[..12];
    var peerKey = "room-seat-private-key-" + suffix;
    await using var first = new RiftboundGameHubClient(url);
    await using var second = new RiftboundGameHubClient(url);
    var firstMessages = Channel.CreateUnbounded<WsServerMessage>();
    var secondMessages = Channel.CreateUnbounded<WsServerMessage>();
    first.ServerMessageReceived += (_, message) => firstMessages.Writer.TryWrite(message);
    second.ServerMessageReceived += (_, message) => secondMessages.Writer.TryWrite(message);
    await first.StartAsync(ct);
    if (!(await first.AuthenticateAsync(originalActor, originalKey, ct)).Authenticated)
        throw new InvalidOperationException("Original network identity could not authenticate for peer publication.");
    await first.JoinRoomAsync(room, originalActor, null, ct);
    var original = await ReadViews(firstMessages.Reader, originalActor.ToLowerInvariant(), 1);
    if (original.SnapshotTick != 1 || original.Players != 1)
        throw new InvalidOperationException("First real room seat must retain its original one-player view.");
    await second.StartAsync(ct);
    if (!(await second.AuthenticateAsync(peerActor, peerKey, ct)).Authenticated)
        throw new InvalidOperationException("Second real peer could not authenticate.");
    await second.JoinRoomAsync(room, peerActor, null, ct);
    var refreshedFirst = ReadViews(firstMessages.Reader, originalActor.ToLowerInvariant(), 2);
    var joinedSecond = ReadViews(secondMessages.Reader, peerActor, 2);
    var current = await Task.WhenAll(refreshedFirst, joinedSecond);
    if (current.Any(v => v.SnapshotTick != 2 || v.Players != 2))
        throw new InvalidOperationException("Both real peers must receive their latest two-player private views without requesting snapshots.");
    Console.WriteLine("Native room-seat peer publication passed: first player tick1, both private viewers tick2; no RequestSnapshot or command retry.");
    Console.WriteLine("NATIVE_ROOM_SEAT_PEER_EVIDENCE " + JsonSerializer.Serialize(new
    {
        room, firstPlayerOriginalTick = original.SnapshotTick,
        firstPlayerRefreshedTick = current[0].SnapshotTick, secondPlayerTick = current[1].SnapshotTick,
        privateViewers = 2, explicitSnapshotRequests = 0, commandRetries = 0,
        originalOnePlayerViewRetained = true, bothPrivateViewersRefreshed = true,
        wrongViewerMessages = 0, reconnectTokensAndPlayerKeysAbsentFromPrivateViews = true
    }));

    async Task<(long SnapshotTick, int Players)> ReadViews(ChannelReader<WsServerMessage> messages, string actor, long minimumTick)
    {
        long snapshotTick = -1, promptTick = -1;
        var players = 0;
        while (snapshotTick < minimumTick || promptTick < minimumTick)
        {
            var message = await messages.ReadAsync(ct);
            if (message.Type == MessageType.ERROR)
                throw new InvalidOperationException("Room-seat peer publication produced a real server error.");
            if (message.Type is not (MessageType.SNAPSHOT or MessageType.PROMPT)) continue;
            if (message.RoomId != room || message.PlayerId != actor || message.Payload is not JsonElement payload)
                throw new InvalidOperationException("Private room-seat view crossed room or authenticated viewer boundaries.");
            var text = payload.GetRawText();
            if (text.Contains(originalKey, StringComparison.Ordinal) || text.Contains(peerKey, StringComparison.Ordinal)
                || payload.TryGetProperty("reconnectToken", out _) || payload.TryGetProperty("openingSeed", out _))
                throw new InvalidOperationException("Credentials or private seed appeared in a room-seat view.");
            if (message.Type == MessageType.SNAPSHOT)
            {
                snapshotTick = payload.GetProperty("tick").GetInt64();
                players = payload.GetProperty("players").EnumerateObject().Count();
            }
            else promptTick = payload.GetProperty("snapshotTick").GetInt64();
            if ((message.Type == MessageType.SNAPSHOT ? snapshotTick : promptTick) != message.ServerTick)
                throw new InvalidOperationException("Room-seat private payload and envelope ticks differ.");
        }
        if (snapshotTick != promptTick)
            throw new InvalidOperationException("Room-seat snapshot and prompt must describe the same current tick.");
        return (snapshotTick, players);
    }
}

static async Task VerifyRoomIdentityBoundaryAsync(string url, string room, string actor, string token, CancellationToken ct)
{
    var checkedActions = 0;
    foreach (var mode in new[] { "ANONYMOUS", "FAILED_KEY", "OTHER_HANDLE" })
    {
        await using var untrusted = new RiftboundGameHubClient(url);
        var messages = Channel.CreateUnbounded<WsServerMessage>();
        untrusted.ServerMessageReceived += (_, message) => messages.Writer.TryWrite(message);
        await untrusted.StartAsync(ct);
        if (mode == "FAILED_KEY")
        {
            if ((await untrusted.AuthenticateAsync(actor, "wrong-secret-" + Guid.NewGuid().ToString("N"), ct)).Authenticated)
                throw new InvalidOperationException("Claimed identity accepted an incorrect key.");
        }
        if (mode == "OTHER_HANDLE")
        {
            var suffix = Guid.NewGuid().ToString("N");
            if (!(await untrusted.AuthenticateAsync("other" + suffix, "other-key-" + suffix, ct)).Authenticated)
                throw new InvalidOperationException("Separate test identity could not authenticate.");
        }
        var expectedCode = mode == "OTHER_HANDLE" ? ErrorCodes.IdentityMismatch : ErrorCodes.AuthenticationRequired;
        foreach (var action in new[] { "JOIN", "SNAPSHOT", "RECONNECT", "READY", "SURRENDER" })
        {
            CommandReceiptDto? receipt = null;
            switch (action)
            {
                case "JOIN": await untrusted.JoinRoomAsync(room, actor, null, ct); break;
                case "SNAPSHOT": await untrusted.RequestSnapshotAsync(room, actor, ct); break;
                case "RECONNECT": await untrusted.ReconnectAsync(room, actor, token, ct); break;
                case "READY": receipt = await untrusted.ReadyAsync(room, actor, "untrusted-ready-" + mode, ct); break;
                case "SURRENDER": receipt = await untrusted.SubmitIntentAsync(room, actor, "untrusted-surrender-" + mode, new { cmdType = "SURRENDER" }, ct); break;
            }
            if (receipt is not null && (receipt.Accepted || receipt.ErrorCode != expectedCode))
                throw new InvalidOperationException("Untrusted command receipt crossed the room identity boundary.");
            var message = await messages.Reader.ReadAsync(ct);
            if (message.Type != MessageType.ERROR || message.Payload is not JsonElement payload
                || payload.GetProperty("code").GetString() != expectedCode || messages.Reader.TryRead(out _))
                throw new InvalidOperationException("Untrusted connection received private state, a token, or an incorrect rejection.");
            checkedActions++;
        }
    }
    Console.WriteLine($"Native wire room identity boundary passed: {checkedActions} rejected actions, three independent connection identities, real-token possession rejected; original authenticated reconnect gates continue.");
    Console.WriteLine("NATIVE_ROOM_IDENTITY_EVIDENCE " + JsonSerializer.Serialize(new {
        room, rejectedActions = checkedActions, identities = new[] { "ANONYMOUS", "FAILED_KEY", "OTHER_HANDLE" },
        leakedPrivateMessages = 0, validReconnectTokenWasSupplied = true
    }));
}

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
