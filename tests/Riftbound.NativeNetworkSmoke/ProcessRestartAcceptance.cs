using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Riftbound.Contracts;
using Riftbound.GodotClient;

internal static class ProcessRestartAcceptance
{
    private sealed record Peer(string Actor, string Key, string Token, JsonElement Snapshot);
    private sealed record Checkpoint(string Room, Peer[] Peers);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static void RequireSameSnapshot(JsonElement expected, JsonElement actual)
    {
        if (!JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonNode.Parse(actual.GetRawText())))
            throw new InvalidOperationException("Cold process recovery changed the original private snapshot.");
    }

    public static async Task RunAsync(string[] args)
    {
        if (args.SequenceEqual(new[] { "restart-json-self-test" }))
        {
            using var wire = JsonDocument.Parse("{\"tick\":2,\"players\":{\"a\":{},\"b\":{}}}");
            using var stored = JsonDocument.Parse(JsonSerializer.Serialize(wire.RootElement, Json));
            RequireSameSnapshot(stored.RootElement, wire.RootElement);
            foreach (var changed in new[] { "{\"tick\":3,\"players\":{\"a\":{},\"b\":{}}}", "{\"tick\":2,\"players\":{\"a\":{}}}" })
            {
                using var invalid = JsonDocument.Parse(changed);
                var rejected = false;
                try { RequireSameSnapshot(stored.RootElement, invalid.RootElement); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Changed tick or private players must fail snapshot parity.");
            }
            Console.WriteLine("Restart JSON contract passed: retained indented checkpoint, compact wire, changed tick/player negatives.");
            return;
        }
        if (args.Length != 2 || args[0] is not ("restart-prepare" or "restart-recover"))
            throw new ArgumentException("Expected restart-prepare/restart-recover and a fresh evidence folder.");
        var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL")
            ?? throw new InvalidOperationException("A dedicated local API is required.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback)
            throw new InvalidOperationException("Restart acceptance requires a dedicated loopback API.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        var folder = args[1];
        Directory.CreateDirectory(folder);
        var input = Path.Combine(folder, "restart-input.json");
        var recover = args[0] == "restart-recover";
        var suffix = Guid.NewGuid().ToString("N");
        var checkpoint = recover
            ? JsonSerializer.Deserialize<Checkpoint>(await File.ReadAllTextAsync(input, ct), Json)!
            : new Checkpoint("restart-" + suffix, new[] { "a", "b" }.Select(seat =>
                new Peer("restart-" + seat + suffix[..12], "restart-key-" + seat + suffix, "", default)).ToArray());
        if (checkpoint.Peers.Length != 2 || checkpoint.Peers.Select(p => p.Actor).Distinct().Count() != 2)
            throw new InvalidOperationException("Two distinct original authenticated players are required.");

        var clients = new List<RiftboundGameHubClient>();
        var channels = new List<Channel<(string Kind, WsServerMessage Message)>>();
        var tokens = new List<string>();
        try
        {
            foreach (var peer in checkpoint.Peers)
            {
                var client = new RiftboundGameHubClient(url);
                clients.Add(client);
                var messages = Channel.CreateUnbounded<(string, WsServerMessage)>();
                channels.Add(messages);
                client.ServerMessageReceived += (kind, message) => messages.Writer.TryWrite((kind, message));
                await client.StartAsync(ct);
                var auth = await client.AuthenticateAsync(peer.Actor, peer.Key, ct);
                if (!auth.Authenticated || auth.Handle != peer.Actor)
                    throw new InvalidOperationException("The original player could not authenticate.");
                if (recover) await client.ReconnectAsync(checkpoint.Room, peer.Actor, peer.Token, ct);
                else await client.JoinRoomAsync(checkpoint.Room, peer.Actor, null, ct);
                while (true)
                {
                    var (kind, message) = await messages.Reader.ReadAsync(ct);
                    if (kind == "Error") throw new InvalidOperationException("Actual join/reconnect rejected.");
                    if (kind != "Joined") continue;
                    var expected = recover ? MessageType.RECONNECT : MessageType.JOIN;
                    if (message.Type != expected || message.RoomId != checkpoint.Room || message.PlayerId != peer.Actor
                        || message.Payload is not JsonElement payload || payload.GetProperty("playerId").GetString() != peer.Actor)
                        throw new InvalidOperationException("Joined identity, room or phase differs from the original session.");
                    var token = payload.GetProperty("reconnectToken").GetString();
                    if (string.IsNullOrWhiteSpace(token) || recover && token == peer.Token)
                        throw new InvalidOperationException("Successful reconnect must rotate the original receipt.");
                    tokens.Add(token);
                    break;
                }
            }
            var peers = new List<Peer>();
            for (var i = 0; i < clients.Count; i++)
            {
                var peer = checkpoint.Peers[i];
                while (channels[i].Reader.TryRead(out _)) { }
                await clients[i].RequestSnapshotAsync(checkpoint.Room, peer.Actor, ct);
                while (true)
                {
                    var (kind, message) = await channels[i].Reader.ReadAsync(ct);
                    if (kind == "Error") throw new InvalidOperationException("Actual restored snapshot rejected.");
                    if (kind != "Snapshot") continue;
                    if (message.RoomId != checkpoint.Room || message.PlayerId != peer.Actor
                        || message.Payload is not JsonElement view)
                        throw new InvalidOperationException("Private snapshot crossed an authenticated identity boundary.");
                    if (view.GetProperty("tick").GetInt64() < 2) continue;
                    var text = view.GetRawText();
                    await File.WriteAllTextAsync(Path.Combine(folder, args[0] + $"-player-{i}-actual-view.json"),
                        JsonSerializer.Serialize(view, Json), ct);
                    if (view.GetProperty("players").EnumerateObject().Count() != 2
                        || checkpoint.Peers.Any(p => text.Contains(p.Key, StringComparison.Ordinal)
                            || p.Token.Length != 0 && text.Contains(p.Token, StringComparison.Ordinal))
                        || tokens.Any(t => text.Contains(t, StringComparison.Ordinal)))
                        throw new InvalidOperationException("Both private players and credential redaction are required.");
                    if (recover) RequireSameSnapshot(peer.Snapshot, view);
                    peers.Add(peer with { Token = tokens[i], Snapshot = view.Clone() });
                    break;
                }
            }
            if (!recover)
            {
                using var stream = new FileStream(input, FileMode.CreateNew);
                await JsonSerializer.SerializeAsync(stream, checkpoint with { Peers = peers.ToArray() }, Json, ct);
            }
            var receipt = new { status = "PASSED", phase = args[0], room = checkpoint.Room, actualJoinedPlayers = 2,
                originalPrivateViewsEqual = recover, originalReconnectReceiptsRotated = recover,
                hiddenCredentialsExposed = false, wholeBackendCertified = false };
            using var result = new FileStream(Path.Combine(folder, args[0] + ".json"), FileMode.CreateNew);
            await JsonSerializer.SerializeAsync(result, receipt, Json, ct);
            Console.WriteLine(JsonSerializer.Serialize(receipt, Json));
        }
        finally
        {
            foreach (var client in clients) await client.DisposeAsync();
        }
    }
}
