using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
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
            await SealedSeriesNetworkAcceptance.VerifyRefreshContractAsync();
            Console.WriteLine("Restart JSON contract passed: retained indented checkpoint, compact wire, changed tick/player negatives.");
            return;
        }
        if (args.Length != 2 || args[0] is not ("restart-prepare" or "restart-recover" or "restart-sealed-resume"))
            throw new ArgumentException("Expected restart-prepare/restart-recover/restart-sealed-resume and an evidence folder.");
        var url = Environment.GetEnvironmentVariable("RIFTBOUND_SERVER_URL")
            ?? throw new InvalidOperationException("A dedicated local API is required.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !uri.IsLoopback)
            throw new InvalidOperationException("Restart acceptance requires a dedicated loopback API.");
        if (args[0] == "restart-sealed-resume")
        {
            await SealedSeriesNetworkAcceptance.RunAsync(url, Path.Combine(args[1], "sealed"), "resume");
            return;
        }
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
            await SealedSeriesNetworkAcceptance.RunAsync(url, Path.Combine(folder, "sealed"), recover ? "recover" : "prepare");
        }
        finally
        {
            foreach (var client in clients) await client.DisposeAsync();
        }
    }
}

internal static class SealedSeriesNetworkAcceptance
{
    private const string Blank = "SEALED·BLANK-BATTLEFIELD";
    private static readonly HashSet<string> AdmittedSetCodes = ["OGN", "SFD", "UNL", "VEN", "RAD"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() }
    };
    private sealed record Peer(string Actor, string Key, string Token, JsonElement Snapshot, JsonElement Prompt);
    private sealed record Duplicate(string Actor, string Id, JsonElement Raw, long Tick);
    private sealed record Checkpoint(string Room, string? SetCode, IReadOnlyList<SealedPackChoiceDto>? Packs,
        int BestOf, Peer[] Peers, Duplicate Submission);

    public static async Task RunAsync(string url, string output, string mode)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct = timeout.Token;
        Directory.CreateDirectory(output);
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        var sets = await http.GetFromJsonAsync<SealedSetDto[]>("/sealed/sets", ct);
        Check(sets is not null && sets.Length == AdmittedSetCodes.Count
            && sets.Select(s => s.SetCode).ToHashSet(StringComparer.Ordinal).SetEquals(AdmittedSetCodes),
            "The real series directory must expose exactly the admitted OGN, SFD, UNL, VEN and RAD sets.");
        await RunCase("OGN", "OGN", 1);
        await RunCase("SFD", "SFD", 3);
        await RunCase("MIXED", null, 1, [new("OGN", 3), new("SFD", 3)]);
        await File.WriteAllTextAsync(Path.Combine(output, mode + ".json"), JsonSerializer.Serialize(new
        {
            status = "PASSED_REAL_SEALED_SIGNALR", mode, cases = 3,
            actualProcessRestartDrivenExternally = true, completeScoringGameCoverage = false
        }, Json), ct);

        async Task RunCase(string caseName, string? setCode, int bestOf, IReadOnlyList<SealedPackChoiceDto>? packs = null)
        {
            var folder = Path.Combine(output, caseName);
            Directory.CreateDirectory(folder);
            var inputPath = Path.Combine(folder, mode == "resume" ? "recovered-input.json" : "input.json");
            var suffix = Guid.NewGuid().ToString("N");
            var input = mode == "prepare" ? null
                : JsonSerializer.Deserialize<Checkpoint>(await File.ReadAllTextAsync(inputPath, ct), Json)!;
            Check(input is null || input.SetCode == setCode && input.BestOf == bestOf
                && (input.Packs ?? []).SequenceEqual(packs ?? []), "The saved restart input changed its sealed room options.");
            var peers = input?.Peers ?? new[] { "a", "b" }.Select(seat =>
                new Peer("sealed-" + seat + suffix[..12], "sealed-key-" + seat + suffix, "", default, default)).ToArray();
            var room = input?.Room ?? "";
            var clients = new Dictionary<string, HubConnection>(StringComparer.Ordinal);
            var messages = new Dictionary<string, Channel<WsServerMessage>>(StringComparer.Ordinal);
            var views = new Dictionary<string, (JsonElement Snapshot, JsonElement Prompt)>(StringComparer.Ordinal);
            var issuedTokens = new Dictionary<string, string>(StringComparer.Ordinal);
            var pools = input?.Peers.ToDictionary(p => p.Actor, p => p.Snapshot.GetProperty("sealedPool"), StringComparer.Ordinal)
                ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var intent = 0;
            try
            {
                foreach (var peer in peers)
                {
                    var client = new HubConnectionBuilder().WithUrl(url + "/hubs/game").AddJsonProtocol(options =>
                        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build();
                    clients.Add(peer.Actor, client);
                    var channel = Channel.CreateUnbounded<WsServerMessage>();
                    messages.Add(peer.Actor, channel);
                    foreach (var method in new[] { "Joined", "Snapshot", "Prompt", "Events", "Error" })
                        client.On<WsServerMessage>(method, message => { channel.Writer.TryWrite(message); });
                    await client.StartAsync(ct);
                    var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", peer.Actor, peer.Key, ct);
                    Check(auth.Authenticated && auth.Handle == peer.Actor, "Sealed wire authentication failed.");
                    if (mode != "prepare")
                        await client.InvokeAsync("Reconnect", room, peer.Actor, peer.Token, ct);
                    else if (peer == peers[0])
                    {
                        var created = await client.InvokeAsync<CreatePublicMatchResultDto?>("CreatePublicMatchWithOptions",
                            peer.Actor, new CreatePublicMatchOptionsDto("sealed", setCode, bestOf, packs), ct);
                        Check(created is not null && created.Match.Format == "sealed" && created.Match.SetCode == setCode
                            && created.Match.BestOf == bestOf
                            && (created.Match.SealedPacks ?? []).SequenceEqual(packs ?? []),
                            "Wire room creation lost its explicit format, set, composition or bestOf.");
                        room = created!.Match.RoomId;
                        var directory = await http.GetFromJsonAsync<PublicMatchDto[]>("/matches", ct);
                        Check(directory!.Any(m => m.RoomId == room && m.Format == "sealed" && m.SetCode == setCode
                            && m.BestOf == bestOf && (m.SealedPacks ?? []).SequenceEqual(packs ?? [])),
                            "The public HTTP room directory lost the sealed options.");
                    }
                    else await client.InvokeAsync("JoinRoom", room, peer.Actor, (string?)null, ct);
                    while (true)
                    {
                        var message = await channel.Reader.ReadAsync(ct);
                        Check(message.Type != MessageType.ERROR, "Sealed room join or reconnect failed.");
                        if (message.Type is not (MessageType.JOIN or MessageType.RECONNECT)) continue;
                        Check(message.Type == (mode == "prepare" ? MessageType.JOIN : MessageType.RECONNECT)
                            && message.RoomId == room && message.PlayerId == peer.Actor && message.Payload is JsonElement,
                            "A sealed reconnect crossed the original authenticated viewer boundary.");
                        var token = ((JsonElement)message.Payload!).GetProperty("reconnectToken").GetString()!;
                        Check(!string.IsNullOrWhiteSpace(token) && (mode == "prepare" || token != peer.Token),
                            "An actual cold reconnect must rotate the original token.");
                        issuedTokens.Add(peer.Actor, token);
                        break;
                    }
                }
                var checkpointTick = input?.Peers[0].Snapshot.GetProperty("tick").GetInt64() ?? 2;
                await Refresh(checkpointTick);
                Duplicate submission;
                if (mode == "prepare")
                {
                    await Choose();
                    submission = await SubmitDeck(peers[0].Actor);
                    if (bestOf == 3)
                    {
                        await SubmitDeck(peers[1].Actor);
                        await Act(peers[0].Actor, new { cmdType = "READY" });
                        await Act(peers[1].Actor, new { cmdType = "READY" });
                        await MulliganAndSurrender();
                        Check(Series().GetProperty("gameNumber").GetInt32() == 2
                            && Series().GetProperty("playOrderChooserPlayerId").GetString() == peers[1].Actor,
                            "After game one the previous loser must choose the next play order.");
                        Check((await History()).Length == 0, "Game one of a series wrote public match history.");
                        await Choose();
                        submission = await SubmitDeck(peers[0].Actor);
                    }
                    await Act(peers[0].Actor, new { cmdType = "READY" });
                    Check(Series().GetProperty("phase").GetString() == "submitting_decks"
                        && views[peers[0].Actor].Snapshot.GetProperty("timing").GetProperty("readyPlayerIds").GetArrayLength() == 1,
                        "The restart checkpoint must preserve one private submission and one ready player.");
                    await Save(inputPath, Current(submission));
                }
                else
                {
                    submission = input!.Submission;
                    foreach (var peer in peers)
                    {
                        Same(peer.Snapshot, views[peer.Actor].Snapshot, "API restart changed the original sealed private snapshot.");
                        Same(peer.Prompt, views[peer.Actor].Prompt, "API restart changed the original sealed prompt.");
                    }
                    var duplicate = await clients[submission.Actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room,
                        submission.Actor, submission.Id, submission.Raw, ct);
                    Check(duplicate.Accepted && duplicate.ServerTick == submission.Tick,
                        "Cold duplicate deck submission must return its original receipt.");
                    await Refresh(checkpointTick);
                    foreach (var peer in peers) Same(peer.Snapshot, views[peer.Actor].Snapshot,
                        "A repeated private submission changed the current state after restart.");
                    if (mode == "recover") await Save(Path.Combine(folder, "recovered-input.json"), Current(submission));
                    else
                    {
                        // Resume only after the external driver has captured and
                        // compared the two original SQL histories.
                        await SubmitDeck(peers[1].Actor);
                        await Act(peers[1].Actor, new { cmdType = "READY" });
                        Check(views[peers[0].Actor].Snapshot.GetProperty("timing").GetProperty("phase").GetString() == "MULLIGAN",
                            "The original ready submission was lost across the actual API restart.");
                        await MulliganAndSurrender();
                        Check(Series().GetProperty("phase").GetString() == "finished", "Resumed sealed series did not finish.");
                        var history = await History();
                        Check(history.Length == 1 && history[0].WinnerPlayerId == peers[0].Actor
                            && history[0].Players.Single(p => p.PlayerId == peers[0].Actor).Score == (bestOf == 1 ? 1 : 2)
                            && history[0].Players.Single(p => p.PlayerId == peers[1].Actor).Score == 0,
                            "The real public history must contain one series result with game wins.");
                    }
                }
                await Save(Path.Combine(folder, mode + ".json"), new
                {
                    status = "PASSED_REAL_SEALED_SIGNALR", room, caseName, setCode, packs, bestOf, mode, actualJoinedPlayers = 2,
                    gameNumber = Series().GetProperty("gameNumber").GetInt32(), originalSubmissionId = submission.Id,
                    originalPoolsUnchanged = true, privateViewsEqual = mode != "prepare", resumedAfterHistoryCapture = mode == "resume"
                });
            }
            finally { foreach (var client in clients.Values) await client.DisposeAsync(); }

            Checkpoint Current(Duplicate duplicate) => new(room, setCode, packs?.ToArray(), bestOf, peers.Select(peer => peer with
            {
                Token = issuedTokens[peer.Actor], Snapshot = views[peer.Actor].Snapshot, Prompt = views[peer.Actor].Prompt
            }).ToArray(), duplicate);

            JsonElement Series() => views[peers[0].Actor].Snapshot.GetProperty("series");
            async Task<PlayerMatchDto[]> History() => (await http.GetFromJsonAsync<PlayerMatchDto[]>(
                "/players/" + peers[0].Actor + "/matches", ct))!.Where(m => m.RoomId == room).ToArray();
            async Task Save(string path, object value) => await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json), ct);

            async Task Choose()
            {
                var chooser = Series().GetProperty("playOrderChooserPlayerId").GetString()!;
                foreach (var peer in peers)
                {
                    var prompt = views[peer.Actor].Prompt.Deserialize<ActionPromptDto>(Json)!;
                    Check(prompt.Candidates!.Any(c => c.Action == "CHOOSE_PLAY_ORDER" && c.Enabled) == (chooser == peer.Actor),
                        "Only the designated actor may receive an enabled play-order action.");
                }
                await Act(chooser, new { cmdType = "CHOOSE_PLAY_ORDER", goFirst = true });
            }

            async Task<Duplicate> SubmitDeck(string actor)
            {
                var receipt = await Act(actor, Deck(pools[actor], bestOf));
                var expected = pools[actor].GetProperty("pool").EnumerateArray().Sum(card => card.GetProperty("count").GetInt32()) - 25;
                foreach (var view in views.Values)
                    Check(view.Snapshot.GetProperty("sideboardCounts").GetProperty(actor).GetInt32() == expected,
                        "Only the number of unused pool cards may be published after private submission.");
                return receipt;
            }
            async Task<Duplicate> Act(string actor, object command)
            {
                var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
                raw["promptId"] = views[actor].Prompt.GetProperty("promptId").GetString();
                raw["snapshotTick"] = views[actor].Prompt.GetProperty("snapshotTick").GetInt64();
                var element = JsonSerializer.SerializeToElement(raw, Json);
                var id = "sealed-wire-" + mode + "-" + ++intent;
                var receipt = await clients[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, id, element, ct);
                Check(receipt.Accepted, receipt.Message);
                await Refresh(receipt.ServerTick);
                return new(actor, id, element, receipt.ServerTick);
            }

            async Task MulliganAndSurrender()
            {
                var first = views[peers[0].Actor].Snapshot.GetProperty("activePlayerId").GetString()!;
                var second = peers.Single(p => p.Actor != first).Actor;
                await Act(first, new { cmdType = "MULLIGAN", handObjectIds = Array.Empty<string>() });
                await Act(second, new { cmdType = "MULLIGAN", handObjectIds = Array.Empty<string>() });
                await Act(peers[1].Actor, new { cmdType = "SURRENDER" });
            }

            async Task Refresh(long? expectedTick = null)
            {
                foreach (var peer in peers)
                {
                    JsonElement snapshot = default, prompt = default;
                    await ReadCurrentView(messages[peer.Actor].Reader,
                        () => clients[peer.Actor].InvokeAsync("RequestSnapshot", room, peer.Actor, ct),
                        message =>
                    {
                        Check(message.Type != MessageType.ERROR, "A sealed wire input produced an unexpected server error.");
                        if (message.Payload is not JsonElement payload) return;
                        if (message.Type == MessageType.EVENTS)
                        {
                            Check(CountProperty(payload, "sealedPool") == 0 && !ContainsPrivateState(payload),
                                "Shared wire events exposed a private sealed pool or server state.");
                            foreach (var item in payload.EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "DECK_SUBMITTED"))
                                Check(item.GetProperty("payload").EnumerateObject().All(p => p.Name is "playerId" or "mainDeckCount" or "runeDeckCount" or "battlefieldCount"),
                                    "Shared submission events exposed private deck identities.");
                        }
                        if (message.Type is not (MessageType.SNAPSHOT or MessageType.PROMPT)) return;
                        Check(message.RoomId == room && message.PlayerId == peer.Actor, "A sealed private view crossed wire recipients.");
                        Check(CountProperty(payload, "sealedPool") == (message.Type == MessageType.SNAPSHOT ? 1 : 0)
                            && (message.Type == MessageType.SNAPSHOT ? payload.GetProperty("tick").GetInt64()
                                : payload.GetProperty("snapshotTick").GetInt64()) == message.ServerTick,
                            "A wire payload contained an extra private pool or disagreed with its envelope tick.");
                        Check(!ContainsPrivateState(payload) && !peers.Any(p => payload.GetRawText().Contains(p.Key, StringComparison.Ordinal)
                            || p.Token.Length > 0 && payload.GetRawText().Contains(p.Token, StringComparison.Ordinal))
                            && !issuedTokens.Values.Any(t => payload.GetRawText().Contains(t, StringComparison.Ordinal)),
                            "A sealed public projection exposed server-only state or credentials.");
                        if (message.Type == MessageType.SNAPSHOT) snapshot = payload.Clone();
                        else prompt = payload.Clone();
                    }, () => snapshot.ValueKind != JsonValueKind.Undefined && prompt.ValueKind != JsonValueKind.Undefined
                        && snapshot.GetProperty("tick").GetInt64() == prompt.GetProperty("snapshotTick").GetInt64()
                        && (expectedTick is null || snapshot.GetProperty("tick").GetInt64() == expectedTick), ct);
                    var projectedSetCode = snapshot.TryGetProperty("setCode", out var setCodeValue)
                        && setCodeValue.ValueKind == JsonValueKind.String ? setCodeValue.GetString() : null;
                    var projectedPacks = snapshot.TryGetProperty("sealedPacks", out var packsValue)
                        && packsValue.ValueKind == JsonValueKind.Array
                            ? packsValue.Deserialize<SealedPackChoiceDto[]>(Json) ?? [] : [];
                    Check(snapshot.GetProperty("format").GetString() == "sealed" && projectedSetCode == setCode
                        && snapshot.GetProperty("bestOf").GetInt32() == bestOf
                        && projectedPacks.SequenceEqual(packs ?? []), "A real sealed snapshot lost room options.");
                    var pool = snapshot.GetProperty("sealedPool");
                    var expectedPackSetCodes = packs is null
                        ? Enumerable.Repeat(setCode!, 6).ToArray()
                        : packs.SelectMany(choice => Enumerable.Repeat(choice.SetCode, choice.Count)).ToArray();
                    var actualPacks = pool.GetProperty("packs");
                    Check(pool.GetProperty("setCode").GetString() == (setCode ?? string.Empty)
                        && actualPacks.GetArrayLength() == expectedPackSetCodes.Length
                        && actualPacks.EnumerateArray().Select(pack => pack.GetProperty("setCode").GetString())
                            .SequenceEqual(expectedPackSetCodes)
                        && actualPacks.EnumerateArray().All(pack => pack.GetProperty("cards").GetArrayLength() == 14
                            && pack.GetProperty("cards").EnumerateArray().All(card => card.GetProperty("slot").ValueKind == JsonValueKind.String
                                && card.GetProperty("foil").ValueKind is JsonValueKind.True or JsonValueKind.False)
                            && pack.GetProperty("cards").EnumerateArray().All(card =>
                                card.GetProperty("cardNo").GetString()!.StartsWith(pack.GetProperty("setCode").GetString() + "·", StringComparison.Ordinal))),
                        "The actual private opening must contain the ordered configured packs with series, slot and foil metadata.");
                    if (pools.TryGetValue(peer.Actor, out var original)) Same(original, pool, "Wire reconnect or intergame transition regenerated the private pool.");
                    else pools.Add(peer.Actor, pool.Clone());
                    views[peer.Actor] = (snapshot, prompt);
                }
            }
        }
    }

    private static async Task ReadCurrentView(ChannelReader<WsServerMessage> reader, Func<Task> request,
        Action<WsServerMessage> receive, Func<bool> complete, CancellationToken ct)
    {
        await request();
        // RequestSnapshot sends both views before its invocation completes.
        // Consume that whole queue: READY may change a view without advancing tick.
        do { receive(await reader.ReadAsync(ct)); }
        while (reader.TryPeek(out _) || !complete());
    }

    public static async Task VerifyRefreshContractAsync()
    {
        foreach (var currentTick in new long[] { 7, 8 })
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var channel = Channel.CreateUnbounded<WsServerMessage>();
            JsonElement snapshot = default, prompt = default;
            void Queue(long tick, bool ready)
            {
                channel.Writer.TryWrite(new(MessageType.SNAPSHOT, "fixture", "alice", tick,
                    JsonSerializer.SerializeToElement(new { tick, timing = new { readyPlayerIds = ready ? new[] { "alice" } : Array.Empty<string>() } })));
                channel.Writer.TryWrite(new(MessageType.PROMPT, "fixture", "alice", tick,
                    JsonSerializer.SerializeToElement(new { snapshotTick = tick, promptId = ready ? "after-ready" : "before-ready" })));
            }
            Queue(7, false);
            await ReadCurrentView(channel.Reader, () =>
            {
                Queue(currentTick, true);
                return Task.CompletedTask;
            }, message =>
            {
                if (message.Type == MessageType.SNAPSHOT) snapshot = (JsonElement)message.Payload!;
                else if (message.Type == MessageType.PROMPT) prompt = (JsonElement)message.Payload!;
            }, () => snapshot.ValueKind != JsonValueKind.Undefined && prompt.ValueKind != JsonValueKind.Undefined
                && snapshot.GetProperty("tick").GetInt64() == currentTick
                && prompt.GetProperty("snapshotTick").GetInt64() == currentTick, timeout.Token);
            var ready = snapshot.GetProperty("timing").GetProperty("readyPlayerIds");
            Check(ready.GetArrayLength() == 1 && ready[0].GetString() == "alice"
                && prompt.GetProperty("promptId").GetString() == "after-ready",
                "Snapshot refresh reused a stale same-tick view before the accepted READY command.");
        }
        Console.WriteLine("Sealed refresh contract passed: queued old views, same-tick READY, advanced ticks and current prompts.");
    }

    private static bool ContainsPrivateState(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Any(p => p.Name is "playerSealedPools" or "openingSeed" or "seed" or "deckBlueprints"
            or "playerDecklists" or "configurationSha256" || ContainsPrivateState(p.Value)),
        JsonValueKind.Array => value.EnumerateArray().Any(ContainsPrivateState),
        _ => false
    };

    private static int CountProperty(JsonElement value, string name) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Sum(p => (p.Name == name ? 1 : 0) + CountProperty(p.Value, name)),
        JsonValueKind.Array => value.EnumerateArray().Sum(v => CountProperty(v, name)),
        _ => 0
    };

    private static object Deck(JsonElement pool, int bestOf)
    {
        // Test-side construction consumes only the actual player's wire pool.
        // The server independently validates every print and trait in SUBMIT_DECK.
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine("data", "official", "card-catalog.zh-CN.json")));
        var cards = source.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardNo").GetString()!, StringComparer.Ordinal);
        var traits = new[] { "red", "green", "blue", "orange", "purple", "yellow" };
        for (var a = 0; a < traits.Length; a++)
        for (var b = a + 1; b < traits.Length; b++)
        for (var c = b + 1; c < traits.Length; c++)
        {
            var allowed = new[] { traits[a], traits[b], traits[c] };
            bool Fits(JsonElement card) => card.GetProperty("cardColorList").EnumerateArray().All(t => t.GetString() == "colorless" || allowed.Contains(t.GetString()));
            var main = pool.GetProperty("pool").EnumerateArray().OrderBy(p => p.GetProperty("cardNo").GetString(), StringComparer.Ordinal)
                .Where(p => cards.TryGetValue(p.GetProperty("cardNo").GetString()!, out var card) && Fits(card)
                    && card.GetProperty("cardCategoryName").GetString() is "单位" or "英雄单位" or "法术" or "专属法术" or "装备")
                .SelectMany(p => Enumerable.Repeat(p.GetProperty("cardNo").GetString()!, p.GetProperty("count").GetInt32())).Take(25).ToArray();
            if (main.Length < 25) continue;
            var rune = cards.Values.First(card => card.GetProperty("cardCategoryName").GetString() == "符文" && Fits(card)
                && card.GetProperty("cardName").GetString() is "炽烈符文" or "翠意符文" or "灵光符文" or "摧破符文" or "混沌符文" or "序理符文"
                && string.IsNullOrWhiteSpace(card.GetProperty("cardEffect").GetString())).GetProperty("cardNo").GetString()!;
            return new { cmdType = "SUBMIT_DECK", legendCardNo = (string?)null, championCardNo = (string?)null,
                mainDeck = main, runeDeck = Enumerable.Repeat(rune, 12).ToArray(), battlefields = Enumerable.Repeat(Blank, bestOf == 1 ? 3 : 1).ToArray() };
        }
        throw new InvalidOperationException("The actual six-pack pool did not yield this fixture's legal 25-card configuration.");
    }

    private static void Same(JsonElement expected, JsonElement actual, string message) =>
        Check(JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonNode.Parse(actual.GetRawText())), message);
    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
