using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

internal static class VenFlowCombatKeywordNetworkAcceptance
{
    // Prepare in PostgreSQL, run play against the dedicated API, restart that
    // same frozen API binary, then run recover against the same two rooms.
    public static async Task RunAsync(string url, string output, bool recover)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var token = deadline.Token;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "ven-flow-network-fixtures.json"), token));
        foreach (var fixture in manifest.RootElement.GetProperty("cases").EnumerateArray()) {
            var room = fixture.GetProperty("room").GetString()!; var actor = fixture.GetProperty("actor").GetString()!;
            var flow = fixture.GetProperty("flow").GetBoolean(); var dir = Path.Combine(output, flow ? "flow" : "normal"); Directory.CreateDirectory(dir);
            var players = fixture.GetProperty("tokens").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            var apiKeys = fixture.GetProperty("apiKeys");
            var connections = new Dictionary<string, HubConnection>(); var snapshots = new ConcurrentDictionary<string, JsonElement>();
            var prompts = new ConcurrentDictionary<string, ActionPromptDto>(); var traces = new ConcurrentQueue<object>();
            try {
                foreach (var (player, reconnect) in players) {
                    var client = new HubConnectionBuilder().WithUrl(url.TrimEnd('/') + "/hubs/game").AddJsonProtocol(options =>
                        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build();
                    connections[player] = client;
                    foreach (var method in new[] { "Joined", "Snapshot", "Prompt", "Events", "Error" })
                        client.On<WsServerMessage>(method, message => {
                            traces.Enqueue(new { recipient = player, method, message });
                            if (message.Payload is not JsonElement payload) return;
                            if (method == "Snapshot") snapshots[player] = payload.Clone();
                            if (method == "Prompt") prompts[player] = payload.Deserialize<ActionPromptDto>(json)!;
                        });
                    await client.StartAsync(token);
                    var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, apiKeys.GetProperty(player).GetString()!, token);
                    Check(auth.Authenticated, "Dedicated per-room N3 audit identity authentication failed.");
                    await client.InvokeAsync("Reconnect", room, player, reconnect, token); await client.InvokeAsync("RequestSnapshot", room, player, token);
                }
                await WaitForViews(); CheckPrivacy();
                if (!recover) {
                    var opponent = players.Keys.Single(player => player != actor);
                    var sourceId = fixture.GetProperty("command").GetProperty("sourceObjectId").GetString()!;
                    if (!flow) Check(!snapshots[opponent].GetRawText().Contains(sourceId, StringComparison.Ordinal)
                        && !JsonSerializer.Serialize(prompts[opponent], json).Contains(sourceId, StringComparison.Ordinal), "Opponent received a private normal source identity.");
                    var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
                    var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                    var requirement = metadata.GetProperty("sourceRequirements").EnumerateArray()
                        .Single(r => r.GetProperty("sourceObjectId").GetString() == sourceId);
                    Check(requirement.GetProperty("sourceZone").GetString() == (flow ? "GRAVEYARD" : "HAND"), "The real API prompt must offer the actual source zone.");
                    var rawPlay = Stamp(fixture.GetProperty("command"), actor);
                    var command = rawPlay.Deserialize<PlayCardCommand>(json)!;
                    var quote = await connections[actor].InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", room,
                        new PlayCostPreviewRequestDto("n3-api-quote", prompts[actor].PromptId!, prompts[actor].SnapshotTick!.Value, command), token);
                    Check(quote.CanPay && quote.Cost is { Mana: 3 } cost && cost.PowerByTrait.GetValueOrDefault("red") == 1,
                        "Real API normal/Flow quote must charge exactly three mana and one red power.");
                    var forbidden = await connections[opponent].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "n3-forbidden-peer", rawPlay, token);
                    Check(!forbidden.Accepted, "An authenticated peer cannot submit another actor's play.");
                    var play = await Send(actor, "n3-network-play", rawPlay);
                    string lastActor = actor; string lastIntent = "n3-network-play"; JsonElement lastRaw = rawPlay;
                    CommandReceiptDto last = play;
                    for (var i = 0; i < 2; i++) {
                        lastActor = players.Keys.Single(player => prompts[player].Candidates?.Any(c => c.Action == CommandTypes.PassPriority) == true);
                        lastIntent = "n3-network-resolve-" + i;
                        lastRaw = Stamp(JsonSerializer.SerializeToElement(new PassPriorityCommand(), json), lastActor);
                        last = await Send(lastActor, lastIntent, lastRaw);
                    }
                    CheckGrant();
                    var stale = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, "n3-network-stale", rawPlay, token);
                    Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == last.ServerTick,
                        "The old source prompt must not spend or grant a second time.");
                    await WaitForViews(last.ServerTick); CheckPrivacy();
                    await File.WriteAllTextAsync(Path.Combine(dir, "ven-flow-network-resume.json"), JsonSerializer.Serialize(new {
                        room, actor, flow, lastActor, lastIntent, lastRaw, tick = last.ServerTick,
                        snapshots = snapshots.ToDictionary(p => p.Key, p => p.Value.Clone()), prompts = prompts.ToDictionary(), quote, play, stale, forbidden
                    }, json), token);
                } else {
                    using var resume = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(dir, "ven-flow-network-resume.json"), token));
                    var previous = resume.RootElement; var tick = previous.GetProperty("tick").GetInt64(); await WaitForViews(tick);
                    foreach (var player in players.Keys) {
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("snapshots").GetProperty(player).GetRawText()), JsonNode.Parse(snapshots[player].GetRawText())),
                            "Actual API process restart changed a player's authoritative snapshot.");
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("prompts").GetProperty(player).GetRawText()), JsonSerializer.SerializeToNode(prompts[player], json)),
                            "Actual API process restart changed a player's authoritative prompt.");
                    }
                    CheckGrant(); CheckPrivacy();
                    var lastActor = previous.GetProperty("lastActor").GetString()!;
                    var duplicate = await connections[lastActor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, lastActor,
                        previous.GetProperty("lastIntent").GetString()!, previous.GetProperty("lastRaw"), token);
                    Check(duplicate.Accepted && duplicate.ServerTick == tick, "Cold duplicate must retain the last original accepted result.");
                    foreach (var player in players.Keys) await connections[player].InvokeAsync("RequestSnapshot", room, player, token);
                    await WaitForViews(tick); CheckGrant(); CheckPrivacy();
                }
                await File.WriteAllTextAsync(Path.Combine(dir, recover ? "ven-flow-network-after-restart.json" : "ven-flow-network-before-restart.json"),
                    JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_N3_NORMAL_OR_FLOW_PASS", room, flow, recover,
                        snapshots, prompts, traces = traces.ToArray(), scope = "Fresh logged current QA midgame; real quote/play/PASS, per-player privacy, stale rejection and actual same-binary process restart/reconnect/duplicate. No full-opening or card certification claim." }, json), token);
            } finally { foreach (var connection in connections.Values) await connection.DisposeAsync(); }

            JsonElement Stamp(JsonElement command, string player) {
                var node = JsonNode.Parse(command.GetRawText())!.AsObject();
                node["promptId"] = prompts[player].PromptId; node["snapshotTick"] = prompts[player].SnapshotTick;
                return JsonSerializer.SerializeToElement(node, json);
            }
            async Task<CommandReceiptDto> Send(string player, string intent, JsonElement raw) {
                var result = await connections[player].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, player, intent, raw, token);
                Check(result.Accepted, result.Message); await WaitForViews(result.ServerTick); CheckPrivacy(); return result;
            }
            async Task WaitForViews(long? tick = null) {
                while (players.Keys.Any(player => !snapshots.TryGetValue(player, out var snapshot) || !prompts.TryGetValue(player, out var prompt)
                    || tick is not null && (snapshot.GetProperty("tick").GetInt64() != tick || prompt.SnapshotTick != tick))) await Task.Delay(20, token);
            }
            void CheckGrant() {
                foreach (var snapshot in snapshots.Values) {
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
        }
        Console.WriteLine(recover ? "NETWORK_VEN_FLOW_COMBAT_KEYWORD_RESTART_PASS" : "NETWORK_VEN_FLOW_COMBAT_KEYWORD_PLAY_PASS");
    }

    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message ?? "N3 network invariant failed."); }
}
