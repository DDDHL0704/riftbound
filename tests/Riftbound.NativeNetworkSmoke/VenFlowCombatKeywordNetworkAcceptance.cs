using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

internal static class VenFlowCombatKeywordNetworkAcceptance
{
    public static async Task RunAsync(string url, string output, bool recover)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3)); var token = deadline.Token;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        await using var sql = VenEmpoweredSelfSqlAudit.DedicatedSource();
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-flow-network-fixtures.json"), token));
        foreach (var fixture in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = fixture.GetProperty("room").GetString()!; var actor = fixture.GetProperty("actor").GetString()!;
            var flow = fixture.GetProperty("flow").GetBoolean(); var no = "VEN·012"; var prefix = flow ? "flow" : "normal";
            var peer = fixture.GetProperty("tokens").EnumerateObject().Single(p => p.Name != actor).Name;
            var dir = Path.Combine(output, flow ? "flow" : "normal"); Directory.CreateDirectory(dir);
            using var resume = recover ? JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(dir, "ven-flow-network-resume.json"), token)) : null;
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
                    Check(auth.Authenticated, "Dedicated per-room N3 identity authentication failed.");
                    await client.InvokeAsync("Reconnect", room, player, reconnect, token);
                    players[player] = (await joinedReply.Task.WaitAsync(token)).ReconnectToken;
                    await client.InvokeAsync("RequestSnapshot", room, player, token);
                }
                await WaitForViews(); CheckPrivacy(); CheckServerErrors();
                QaReconnectAssertions.CheckJoined(players.Keys, joined);
                var afterReconnect = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                VenEmpoweredSelfSqlAudit.CheckReconnect(room, beforeReconnect, afterReconnect, priorTokens, players);
                if (!recover) {
                    var unitPlay = fixture.GetProperty("command").Deserialize<PlayCardCommand>(json)!;
                    var sourceId = unitPlay.SourceObjectId; var rawPlay = Stamp(unitPlay, actor);
                    if (!flow) Check(!ContainsIdentity(snapshots[peer], sourceId)
                        && !ContainsIdentity(JsonSerializer.SerializeToElement(prompts[peer], json), sourceId), "Opponent received the private normal source identity.");
                    var requirement = JsonSerializer.SerializeToElement(prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PlayCard).Metadata, json)
                        .GetProperty("sourceRequirements").EnumerateArray().Single(s => s.GetProperty("sourceObjectId").GetString() == sourceId);
                    Check(requirement.GetProperty("sourceZone").GetString() == (flow ? "GRAVEYARD" : "HAND"), "Real normal/Flow source zone is missing.");
                    var quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                        new PlayCostPreviewRequestDto("n3-api-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, unitPlay), token);
                    Check(quote.CanPay && quote.Cost is { Mana: 3 } cost && cost.PowerByTrait.GetValueOrDefault("red") == 1,
                        "Actual API normal/Flow quote must charge exactly three mana and one red power.");
                    forbiddenBefore = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                    var forbidden = await connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "n3-forbidden-peer", rawPlay, token);
                    Check(!forbidden.Accepted && forbidden.ErrorCode == ErrorCodes.IdentityMismatch, "Authenticated peer must not submit another player's command.");
                    expectedErrors[(peer, actor, ErrorCodes.IdentityMismatch)] = 1; CheckServerErrors();
                    forbiddenAfter = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                    VenEmpoweredSelfSqlAudit.CheckExact(forbiddenBefore, forbiddenAfter);
                    await Send(actor, "n3-network-play", unitPlay);
                    for (var index = 0; index < 2; index++) await Send(Priority(), "n3-network-resolve-" + index, new PassPriorityCommand());
                    CheckPrefix();
                    var stale = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "n3-network-stale", rawPlay, token);
                    Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == last!.ServerTick,
                        "Old play prompt must not spend or grant again.");
                    expectedErrors[(actor, actor, ErrorCodes.PromptExpired)] = 1; CheckServerErrors();
                    await WaitForViews(last!.ServerTick); CheckPrefix(); CheckPrivacy();
                    await Duplicate(lastActor!, lastIntent!, lastRaw, last.ServerTick);
                    await File.WriteAllTextAsync(Path.Combine(dir, "ven-flow-network-resume.json"), JsonSerializer.Serialize(new {
                        room, actor, cardNo = no, flow, prefix, acceptedIntents, lastActor, lastIntent, lastRaw, tick = last.ServerTick, reconnectTokens = players,
                        snapshots = snapshots.ToDictionary(p => p.Key, p => p.Value.Clone()), prompts = prompts.ToDictionary(), quote, stale, forbidden
                    }, json), token);
                } else {
                    var previous = resume!.RootElement; var tick = previous.GetProperty("tick").GetInt64(); await WaitForViews(tick);
                    foreach (var player in players.Keys) {
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("snapshots").GetProperty(player).GetRawText()), JsonNode.Parse(snapshots[player].GetRawText())),
                            "Same-binary process restart changed a player normal/Flow projection.");
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
                await File.WriteAllTextAsync(Path.Combine(dir, recover ? "ven-flow-network-after-restart.json" : "ven-flow-network-before-restart.json"),
                    JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_N3_NORMAL_OR_FLOW_PASS", room, cardNo = no, flow, prefix, recover,
                        snapshots, prompts, reconnectTokens = players, traces = traces.ToArray(), completeGameOrCardCertification = false,
                        scope = "Fresh logged QA setup; actual normal/Flow play and both priority passes, private identities, stale reject and same-binary restart/reconnect/duplicate." }, json), token);
            } catch (Exception error) {
                await File.WriteAllTextAsync(Path.Combine(dir, recover ? "network-after-restart-failure.json" : "network-before-restart-failure.json"),
                    JsonSerializer.Serialize(new { room, cardNo = no, flow, prefix, recover, error = error.ToString(), snapshots, prompts, traces = traces.ToArray() }, json));
                throw;
            } finally { foreach (var connection in connections.Values) await connection.DisposeAsync(); }

            JsonElement Stamp(GameCommand command, string player) {
                var node = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                node["promptId"] = prompts[player].PromptId; node["snapshotTick"] = prompts[player].SnapshotTick;
                return JsonSerializer.SerializeToElement(node, json);
            }
            async Task Send(string player, string intent, GameCommand command) {
                var raw = Stamp(command, player);
                var result = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, intent, raw, token);
                Check(result.Accepted, result.Message); acceptedIntents.Add(intent); lastActor = player; lastIntent = intent; lastRaw = raw; last = result;
                await WaitForViews(result.ServerTick); CheckPrivacy();
            }
            async Task Duplicate(string player, string intent, JsonElement raw, long tick) {
                duplicateBefore = await VenEmpoweredSelfSqlAudit.CaptureAsync(sql, room, token);
                var duplicate = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, intent, raw, token);
                Check(duplicate.Accepted && duplicate.ServerTick == tick, "Original API duplicate must retain the resolved normal/Flow result.");
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
            void CheckPrefix() {
                foreach (var snapshot in snapshots.Values) {
                    var pool = snapshot.GetProperty("players").GetProperty(actor).GetProperty("runePool");
                    Check(pool.GetProperty("mana").GetInt32() == 17 && pool.GetProperty("powerByTrait").GetProperty("red").GetInt32() == 9,
                        "Actual API payment must retain three mana and one red power.");
                    var unit = snapshot.GetProperty("players").GetProperty(actor).GetProperty("objects").GetProperty("N3-ALLY");
                    Check(unit.GetProperty("power").GetInt32() == 4 && !unit.GetProperty("isExhausted").GetBoolean()
                        && unit.GetProperty("untilEndOfTurnEffects").EnumerateArray().Count(e => e.GetString()!.StartsWith("COMBAT_KEYWORD:强攻3:", StringComparison.Ordinal)) == 1,
                        "Both actual API projections must retain the readied unit and one temporary Assault3 grant.");
                }
            }
            void CheckPrivacy() {
                foreach (var player in players.Keys) {
                    var value = snapshots[player].GetRawText() + JsonSerializer.Serialize(prompts[player], json);
                    foreach (var id in fixture.GetProperty("hiddenFrom").GetProperty(player).EnumerateArray())
                        Check(!value.Contains(id.GetString()!, StringComparison.Ordinal), "Unrevealed opponent hand or deck identity escaped through API views/prompts.");
                    Check(!value.Contains("reconnectToken", StringComparison.Ordinal) && !value.Contains("openingSeed", StringComparison.Ordinal), "Server identity data escaped a player projection.");
                }
            }
            void CheckServerErrors() => QaReconnectAssertions.CheckErrors(serverErrors, room, expectedErrors);
        }
        Console.WriteLine(recover ? "NETWORK_VEN_FLOW_COMBAT_KEYWORD_RESTART_PASS" : "NETWORK_VEN_FLOW_COMBAT_KEYWORD_PLAY_PASS");
    }

    internal static PlayerSessionDto ReadJoinedPlayer(JsonElement payload, string player, string priorToken, JsonSerializerOptions json)
        => QaReconnectAssertions.ReadJoined(payload, player, priorToken, json);

    internal static bool ContainsIdentity(JsonElement node, string id) => node.ValueKind switch {
        JsonValueKind.String => node.GetString() == id,
        JsonValueKind.Array => node.EnumerateArray().Any(item => ContainsIdentity(item, id)),
        JsonValueKind.Object => node.EnumerateObject().Any(p => p.Name == id || ContainsIdentity(p.Value, id)),
        _ => false
    };

    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message ?? "N3 network invariant failed."); }
}
