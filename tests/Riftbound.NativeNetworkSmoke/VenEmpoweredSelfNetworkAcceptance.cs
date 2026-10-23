using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

internal static class VenEmpoweredSelfNetworkAcceptance
{
    public static async Task RunAsync(string url, string output, bool recover)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        await using var sql = VenEmpoweredSelfSqlAudit.DedicatedSource();
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-empowered-self-network-fixtures.json"), token));
        foreach (var fixture in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = fixture.GetProperty("room").GetString()!; var actor = fixture.GetProperty("actor").GetString()!;
            var peer = fixture.GetProperty("peer").GetString()!; var no = fixture.GetProperty("cardNo").GetString()!;
            var prefix = fixture.GetProperty("prefix").GetString()!; var dir = Path.Combine(output, fixture.GetProperty("key").GetString()!); Directory.CreateDirectory(dir);
            using var resume = recover ? JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(dir, "network-resume.json"), token)) : null;
            var tokenInput = recover ? resume!.RootElement.GetProperty("reconnectTokens") : fixture.GetProperty("tokens");
            var players = tokenInput.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            var priorTokens = players.ToDictionary();
            var beforeReconnect = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
            if (recover) {
                using var previousSql = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(dir, "sql-before-restart.json"), token));
                VenEmpoweredSelfSqlAudit.CheckExact(previousSql.RootElement.GetProperty("sqlSample").Deserialize<VenEmpoweredSelfSqlAuditFrame>(json)!, beforeReconnect);
            }
            var connections = new Dictionary<string, HubConnection>(); var snapshots = new ConcurrentDictionary<string, JsonElement>();
            var prompts = new ConcurrentDictionary<string, ActionPromptDto>(); var traces = new ConcurrentQueue<object>();
            var joined = new ConcurrentDictionary<string, PlayerSessionDto>();
            var serverErrors = new ConcurrentQueue<(string Recipient, WsServerMessage Message)>();
            var expectedErrors = new Dictionary<(string Recipient, string Player, string Code), int>();
            var acceptedIntents = new List<string>(); string? lastActor = null; string? lastIntent = null; JsonElement lastRaw = default;
            CommandReceiptDto? last = null;
            VenEmpoweredSelfSqlAuditFrame? forbiddenBefore = null, forbiddenAfter = null;
            VenEmpoweredSelfSqlAuditFrame? duplicateBefore = null, duplicateAfter = null;
            try {
                foreach (var (player, reconnect) in players.ToArray()) {
                    var joinedReply = new TaskCompletionSource<PlayerSessionDto>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var client = new HubConnectionBuilder().WithUrl(url.TrimEnd('/') + "/hubs/game").AddJsonProtocol(options =>
                        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build();
                    connections[player] = client;
                    foreach (var method in new[] { "Joined", "Snapshot", "Prompt", "Events", "Error" })
                        client.On<WsServerMessage>(method, message => {
                            traces.Enqueue(new { recipient = player, method, message });
                            if (method == "Error") {
                                serverErrors.Enqueue((player, message));
                                joinedReply.TrySetException(new InvalidOperationException("API rejected reconnect: " + JsonSerializer.Serialize(message.Payload, json)));
                            }
                            if (message.Payload is not JsonElement payload) return;
                            if (method == "Joined") {
                                try { joined[player] = ReadJoinedPlayer(payload, player, reconnect, json); joinedReply.TrySetResult(joined[player]); }
                                catch (Exception error) { joinedReply.TrySetException(error); }
                            }
                            if (method == "Snapshot") snapshots[player] = payload.Clone();
                            if (method == "Prompt") prompts[player] = payload.Deserialize<ActionPromptDto>(json)!;
                        });
                    await client.StartAsync(token);
                    var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, fixture.GetProperty("apiKeys").GetProperty(player).GetString()!, token);
                    Check(auth.Authenticated, "Dedicated per-room N9 identity authentication failed.");
                    await client.InvokeAsync("Reconnect", room, player, reconnect, token);
                    players[player] = (await joinedReply.Task.WaitAsync(token)).ReconnectToken;
                    await client.InvokeAsync("RequestSnapshot", room, player, token);
                }
                await WaitForViews(); CheckPrivacy(); CheckServerErrors();
                QaReconnectAssertions.CheckJoined(players.Keys, joined);
                var afterReconnect = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                VenEmpoweredSelfSqlAudit.CheckReconnect(room, beforeReconnect, afterReconnect, priorTokens, players);
                if (!recover) {
                    var unitPlay = new PlayCardCommand("N9-UNIT", no, [], Destination: "BASE");
                    var rawPlay = Stamp(unitPlay, actor);
                    var requirement = JsonSerializer.SerializeToElement(prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PlayCard).Metadata, json)
                        .GetProperty("sourceRequirements").EnumerateArray().Single(s => s.GetProperty("sourceObjectId").GetString() == "N9-UNIT");
                    Check(requirement.GetProperty("sourceZone").GetString() == "HAND", "The source must be offered from its real private hand.");
                    var quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                        new PlayCostPreviewRequestDto("n9-api-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, unitPlay), token);
                    Check(quote.CanPay && quote.Cost?.Mana == (no == "VEN·043" ? 1 : no == "VEN·050" ? 4 : 3)
                        && quote.Cost.PowerByTrait.GetValueOrDefault("orange") == (no == "VEN·070" ? 1 : 0), "Actual API printed cost quote is incorrect.");
                    forbiddenBefore = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                    var forbidden = await connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "n9-forbidden-peer", rawPlay, token);
                    Check(!forbidden.Accepted && forbidden.ErrorCode == ErrorCodes.IdentityMismatch, "Authenticated peer must not submit another player's command.");
                    expectedErrors[(peer, actor, ErrorCodes.IdentityMismatch)] = 1; CheckServerErrors();
                    forbiddenAfter = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                    VenEmpoweredSelfSqlAudit.CheckExact(forbiddenBefore, forbiddenAfter);
                    await PlayToPrefixAsync(no, prefix, actor, peer, Send, Priority);
                    CheckPrefix();
                    var stale = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "n9-network-stale", rawPlay, token);
                    Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == last!.ServerTick,
                        "Old play prompt must not alter the pending ability or spend again.");
                    expectedErrors[(actor, actor, ErrorCodes.PromptExpired)] = 1; CheckServerErrors();
                    await WaitForViews(last!.ServerTick); CheckPrefix(); CheckPrivacy();
                    await Duplicate(lastActor!, lastIntent!, lastRaw, last.ServerTick);
                    await File.WriteAllTextAsync(Path.Combine(dir, "network-resume.json"), JsonSerializer.Serialize(new {
                        room, actor, cardNo = no, prefix, acceptedIntents, lastActor, lastIntent, lastRaw, tick = last.ServerTick, reconnectTokens = players,
                        snapshots = snapshots.ToDictionary(p => p.Key, p => p.Value.Clone()), prompts = prompts.ToDictionary(), quote, stale, forbidden
                    }, json), token);
                } else {
                    var previous = resume!.RootElement; var tick = previous.GetProperty("tick").GetInt64(); await WaitForViews(tick);
                    foreach (var player in players.Keys) {
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("snapshots").GetProperty(player).GetRawText()), JsonNode.Parse(snapshots[player].GetRawText())),
                            "Same-binary process restart changed the pending public/private/revealed source projection.");
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("prompts").GetProperty(player).GetRawText()), JsonSerializer.SerializeToNode(prompts[player], json)),
                            "Same-binary process restart changed the current choice/priority prompt.");
                    }
                    CheckPrefix(); CheckPrivacy();
                    var playerId = previous.GetProperty("lastActor").GetString()!;
                    await Duplicate(playerId, previous.GetProperty("lastIntent").GetString()!, previous.GetProperty("lastRaw"), tick);
                }
                var finalSql = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                VenEmpoweredSelfSqlAudit.CheckExact(duplicateAfter!, finalSql);
                CheckServerErrors();
                await File.WriteAllTextAsync(Path.Combine(dir, recover ? "network-after-restart-sql-audit.json" : "network-before-restart-sql-audit.json"),
                    JsonSerializer.Serialize(new { room, priorTokens, reconnectTokens = players, beforeReconnect, afterReconnect,
                        forbiddenBefore, forbiddenAfter, duplicateBefore, duplicateAfter, finalSql,
                        scope = "Actual read-only SQL samples bracket real API reconnect, recovery and duplicate separately; only exact reconnect token/time columns may change at reconnect." }, json), token);
                await File.WriteAllTextAsync(Path.Combine(dir, recover ? "network-after-restart.json" : "network-before-restart.json"),
                    JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_N9_EMPOWER_SOURCE_PASS", room, cardNo = no, prefix, recover,
                        snapshots, prompts, reconnectTokens = players, traces = traces.ToArray(), completeGameOrCardCertification = false,
                        scope = "Fresh logged QA setup; actual play/activation/return/crab reveal, private identities, stale reject and same-binary restart/reconnect/duplicate." }, json), token);
            } catch (Exception error) {
                await File.WriteAllTextAsync(Path.Combine(dir, recover ? "network-after-restart-failure.json" : "network-before-restart-failure.json"),
                    JsonSerializer.Serialize(new { room, cardNo = no, prefix, recover, error = error.ToString(), snapshots, prompts, traces = traces.ToArray() }, json));
                throw;
            } finally { foreach (var connection in connections.Values) await connection.DisposeAsync(); }

            JsonElement Stamp(GameCommand command, string player) {
                var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                node["promptId"] = prompts[player].PromptId; node["snapshotTick"] = prompts[player].SnapshotTick;
                return JsonSerializer.SerializeToElement(node, json);
            }
            async Task Send(string player, GameCommand command) {
                var intent = "n9-network-step-" + acceptedIntents.Count; var raw = Stamp(command, player);
                var result = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, intent, raw, token);
                Check(result.Accepted, result.Message); acceptedIntents.Add(intent); lastActor = player; lastIntent = intent; lastRaw = raw; last = result;
                await WaitForViews(result.ServerTick); CheckPrivacy();
            }
            async Task Duplicate(string player, string intent, JsonElement raw, long tick) {
                duplicateBefore = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                var duplicate = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, intent, raw, token);
                Check(duplicate.Accepted && duplicate.ServerTick == tick, "Original API duplicate must retain the pending/choice result.");
                foreach (var viewer in players.Keys) await connections[viewer].InvokeAsync("RequestSnapshot", room, viewer, token);
                await WaitForViews(tick); CheckPrefix(); CheckPrivacy();
                duplicateAfter = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                VenEmpoweredSelfSqlAudit.CheckExact(duplicateBefore, duplicateAfter);
            }
            string Priority() => players.Keys.Single(p => prompts[p].Candidates?.Any(c => c.Action == CommandTypes.PassPriority) == true);
            async Task WaitForViews(long? tick = null) {
                while (players.Keys.Any(player => !snapshots.TryGetValue(player, out var snapshot) || !prompts.TryGetValue(player, out var prompt)
                    || tick is not null && (snapshot.GetProperty("tick").GetInt64() != tick || prompt.SnapshotTick != tick))) await Task.Delay(20, token);
            }
            void CheckPrefix() => CheckPrefixViews(no, prefix, actor, peer, snapshots, prompts, json);
            void CheckPrivacy() => CheckPrivateViews(fixture, actor, players.Keys.ToArray(), snapshots, prompts, json);
            void CheckServerErrors() => QaReconnectAssertions.CheckErrors(serverErrors, room, expectedErrors);
        }
        Console.WriteLine(recover ? "NETWORK_VEN_EMPOWERED_SELF_RESTART_PASS" : "NETWORK_VEN_EMPOWERED_SELF_PREFIX_PASS");
    }

    internal static PlayerSessionDto ReadJoinedPlayer(JsonElement payload, string player, string priorToken, JsonSerializerOptions json)
        => QaReconnectAssertions.ReadJoined(payload, player, priorToken, json);

    private static bool Revealed(IReadOnlyDictionary<string, ActionPromptDto> prompts, JsonSerializerOptions json) => prompts.Values.SelectMany(p => p.Candidates ?? []).Where(c => c.Action == CommandTypes.ChooseCards)
        .Select(c => JsonSerializer.SerializeToElement(c.Metadata, json)).Any(m => m.TryGetProperty("viewedCardsPublic", out var flag) && flag.GetBoolean()
            && m.GetProperty("choiceWindow").GetString() == "REVEALED_HAND_EFFECT");

    internal static void CheckPrefixViews(string no, string prefix, string actor, string peer,
        IReadOnlyDictionary<string, JsonElement> snapshots, IReadOnlyDictionary<string, ActionPromptDto> prompts, JsonSerializerOptions json) {
        foreach (var (viewer, snapshot) in snapshots) {
            var row = snapshot.GetProperty("stack").EnumerateArray().Single(s => s.GetProperty("effectKind").GetString() == "EMPOWER_ACTIVATION");
            var hidden = viewer == peer && prefix == "private";
            Check(row.GetProperty("sourceObjectId").GetString() == (hidden ? "HIDDEN" : "N9-UNIT")
                && row.GetProperty("sourceVisibility").GetString() == (hidden ? "HIDDEN" : "VISIBLE")
                && row.GetProperty("cardNo").GetString() == no && !row.TryGetProperty("empowerActivation", out _),
                "API Empower row has incorrect source visibility or leaked its private activation witness.");
            var actorView = snapshot.GetProperty("players").GetProperty(actor);
            Check(actorView.GetProperty("runePool").GetProperty("mana").GetInt32() == 40 - (no == "VEN·043" ? 1 : no == "VEN·050" ? 4 : 3)
                - (no == "VEN·070" ? 3 : 7) - (prefix == "public" ? 0 : 1), "API projection lost actual printed/reduced activation/return payment.");
            if (prefix == "public" || viewer == actor || prefix == "revealed") {
                var unit = actorView.GetProperty("objects").GetProperty("N9-UNIT");
                Check(!unit.TryGetProperty("isEmpowered", out var empowered) || !empowered.GetBoolean(), "Pending old ability must not empower the current incarnation.");
            }
            if (prefix != "public") Check(actorView.GetProperty("zones").GetProperty("hand").EnumerateArray()
                .Any(id => id.GetString() == "N9-UNIT") == (viewer == actor || prefix == "revealed"), "Returned hand identity is visible to the wrong viewer.");
        }
        Check(Revealed(prompts, json) == (prefix == "revealed"), "Only the actual crab-death choice may reveal this private hand.");
    }

    internal static void CheckPrivateViews(JsonElement fixture, string actor, IReadOnlyList<string> players,
        IReadOnlyDictionary<string, JsonElement> snapshots, IReadOnlyDictionary<string, ActionPromptDto> prompts, JsonSerializerOptions json) {
        var currentHands = players.ToDictionary(p => p, p => snapshots[p].GetProperty("players").GetProperty(p).GetProperty("zones").GetProperty("hand")
            .EnumerateArray().Select(id => id.GetString()!).ToArray());
        var publicIds = snapshots[actor].GetProperty("players").EnumerateObject().SelectMany(p => new[] { "base", "battlefields", "graveyard", "banished", "legendZone", "championZone" }
            .SelectMany(zone => p.Value.GetProperty("zones").GetProperty(zone).EnumerateArray().Select(id => id.GetString()!))).ToHashSet(StringComparer.Ordinal);
        var handIds = currentHands.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);
        foreach (var player in players) {
            var material = JsonSerializer.SerializeToElement(new { snapshot = snapshots[player], prompt = prompts[player] }, json);
            var value = material.GetRawText();
            foreach (var id in fixture.GetProperty("hiddenDecks").GetProperty(player).EnumerateArray().Select(id => id.GetString()!))
                if (!publicIds.Contains(id) && !handIds.Contains(id)) Check(!ContainsIdentity(material, id), "Unreleased rune or main-deck identity escaped through API views/prompts.");
            foreach (var (owner, ids) in currentHands)
                if (owner != player && !(owner == actor && Revealed(prompts, json)))
                    foreach (var id in ids) Check(!ContainsIdentity(material, id), "Unrevealed opponent hand identity escaped through API views/prompts.");
            Check(!value.Contains("empowerActivation", StringComparison.OrdinalIgnoreCase) && !value.Contains("openingSeed", StringComparison.Ordinal)
                && !value.Contains("reconnectToken", StringComparison.Ordinal), "Private activation, seed or credentials escaped through API views/prompts.");
        }
    }

    internal static bool ContainsIdentity(JsonElement node, string id) => node.ValueKind switch {
        JsonValueKind.String => node.GetString() == id,
        JsonValueKind.Array => node.EnumerateArray().Any(item => ContainsIdentity(item, id)),
        JsonValueKind.Object => node.EnumerateObject().Any(p => p.Name == id || ContainsIdentity(p.Value, id)),
        _ => false
    };

    // Also run verbatim by the local memory probe; source state is never planted
    // already empowered, returned or revealed by this network command recipe.
    internal static async Task PlayToPrefixAsync(string no, string prefix, string actor, string peer,
        Func<string, GameCommand, Task> send, Func<string> priority)
    {
        Check(prefix is "public" or "private" or "revealed", "Unknown N9 network prefix.");
        await send(actor, new PlayCardCommand("N9-UNIT", no, [], Destination: "BASE"));
        await send(actor, new ActivateAbilityCommand("N9-UNIT", "EMPOWER:0:0", []));
        if (prefix == "public") return;
        await send(actor, new PlayCardCommand("N9-RETURN", "OGN·104/298", ["N9-UNIT"])); await Top();
        if (prefix == "private") return;
        if (priority() == actor) await send(actor, new PassPriorityCommand());
        await send(peer, new PlayCardCommand("N9-REMOVE", "SFD·163/221", ["N9-CRAB", "N9-ENEMY"])); await Top(); await Top();
        async Task Top() { await send(priority(), new PassPriorityCommand()); await send(priority(), new PassPriorityCommand()); }
    }

    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message ?? "N9 network invariant failed."); }
}
