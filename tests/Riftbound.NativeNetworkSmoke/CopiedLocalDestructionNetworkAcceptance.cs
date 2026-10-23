using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static NetworkReconnectSqlCapture;
using Riftbound.Contracts;

internal static class CopiedLocalDestructionNetworkAcceptance
{
    // The external runner starts the dedicated API, calls play, really restarts
    // that process, then calls recover. This class never manages a shared API.
    public static async Task RunAsync(string url, string output, bool recover)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        Check(fixture.RootElement.GetProperty("kind").GetString() == "FUTURE_CLIENT_INTENTS_NOT_ACCEPTED_HISTORY",
            "Network play requires fresh logged QA positions and future intents, not imported accepted rows.");
        using var saved = recover
            ? JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-resume.json"))) : null;
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
            ?? throw new InvalidOperationException("C8 network evidence requires its real dedicated PostgreSQL connection."));
        Check(settings.Database == "riftbound_acceptance" && settings.Host is "127.0.0.1" or "localhost",
            "C8 network SQL sampling is restricted to the dedicated loopback acceptance database.");
        await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
        var resumes = new List<object>();
        var reports = new List<object>();
        foreach (var item in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var token = deadline.Token;
            var kind = item.GetProperty("kind").GetString()!;
            var room = item.GetProperty("room").GetString()!;
            var p1 = item.GetProperty("p1").GetString()!;
            var p2 = item.GetProperty("p2").GetString()!;
            var previous = recover ? saved!.RootElement.GetProperty("cases").EnumerateArray()
                .Single(c => c.GetProperty("room").GetString() == room) : default;
            var players = (recover ? previous : item).GetProperty("tokens").EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString()!);
            Check(players.Count == 2 && players.ContainsKey(p1) && players.ContainsKey(p2), "Each case needs its own two real identities.");
            var traces = new ConcurrentQueue<object>();
            var snapshots = new ConcurrentDictionary<string, JsonElement>();
            var prompts = new ConcurrentDictionary<string, ActionPromptDto>();
            var joined = new ConcurrentDictionary<string, PlayerSessionDto>();
            var serverErrors = new ConcurrentQueue<string>();
            var expectedErrors = new ConcurrentQueue<ExpectedError>();
            var expectedErrorEvidence = new List<ExpectedError>();
            var connections = new Dictionary<string, HubConnection>();
            var beforeReconnect = await CaptureSql(source, room, token);
            SqlSample? afterReconnect = null, duplicateBefore = null, duplicateAfter = null, finalSql = null;
            string? failure = null;
            try
            {
                foreach (var (player, reconnect) in players)
                {
                    var client = new HubConnectionBuilder().WithUrl(url + "/hubs/game").AddJsonProtocol(options =>
                        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build();
                    connections[player] = client;
                    foreach (var method in new[] { "Joined", "Snapshot", "Prompt", "Events", "Error" })
                        client.On<WsServerMessage>(method, message => {
                            traces.Enqueue(new { recipient = player, method, message });
                            if (message.Payload is not JsonElement payload) {
                                if (method is "Joined" or "Error") serverErrors.Enqueue("Missing structured Joined/Error payload.");
                                return;
                            }
                            if (method == "Joined") {
                                if (message.RoomId != room || message.PlayerId != player || message.Type != MessageType.RECONNECT
                                    || !joined.TryAdd(player, payload.Deserialize<PlayerSessionDto>(json)!))
                                    serverErrors.Enqueue("Unexpected or repeated Joined identity/room/type.");
                            }
                            if (method == "Error") {
                                var actual = new ExpectedError(player, message.PlayerId, payload.GetProperty("code").GetString()!);
                                if (message.RoomId != room || message.Type != MessageType.ERROR
                                    || !expectedErrors.TryDequeue(out var expected) || actual != expected)
                                    serverErrors.Enqueue(payload.GetRawText());
                            }
                            if (method == "Snapshot") snapshots[player] = payload.Clone();
                            if (method == "Prompt") prompts[player] = payload.Deserialize<ActionPromptDto>(json)!;
                        });
                    await client.StartAsync(token);
                    var credential = item.GetProperty("credentials").GetProperty(player).GetString()!;
                    var auth = await client.InvokeAsync<AuthResultDto>("Authenticate", player, credential, token);
                    Check(auth.Authenticated, "Dedicated C8 target-replacement identity authentication failed.");
                    await client.InvokeAsync("Reconnect", room, player, reconnect, token);
                    await client.InvokeAsync("RequestSnapshot", room, player, token);
                }
                var tick = recover ? previous.GetProperty("tick").GetInt64() : item.GetProperty("seedTick").GetInt64();
                await WaitForViews(tick);
                foreach (var player in players.Keys)
                    Check(joined[player].PlayerId == player && !string.IsNullOrWhiteSpace(joined[player].ReconnectToken)
                        && joined[player].ReconnectToken != players[player], "Each real Joined must supply its own rotated token.");
                afterReconnect = await CaptureSql(source, room, token);
                if (!recover)
                {
                    JsonElement lastRaw = default;
                    string? lastActor = null, lastIntent = null;
                    CommandReceiptDto? forbidden = null;
                    var index = 0;
                    foreach (var step in item.GetProperty("steps").EnumerateArray())
                    {
                        var command = step.GetProperty("command");
                        var action = command.GetProperty("cmdType").GetString()!;
                        var selector = step.GetProperty("actor").GetString()!;
                        var actor = selector switch
                        {
                            "p1" => p1, "p2" => p2,
                            "$priority" or "$focus" or "$choice" => players.Keys.Single(player => prompts[player].Actionable
                                && prompts[player].Candidates?.Any(c => c.Action == action && c.Enabled) == true),
                            _ => throw new InvalidOperationException("Unknown server actor selector.")
                        };
                        var node = JsonNode.Parse(command.GetRawText())!.AsObject();
                        var hasId = step.TryGetProperty("choiceId", out var choiceId) && choiceId.ValueKind == JsonValueKind.String;
                        var hasHint = step.TryGetProperty("choiceHint", out var choiceHint) && choiceHint.ValueKind == JsonValueKind.String;
                        if (hasId || hasHint)
                        {
                            Check(hasId != hasHint, "A C8 decision must use exactly one server-option selector.");
                            var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PayCost && c.Enabled);
                            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                            var option = metadata.GetProperty("paymentChoices").EnumerateArray().Single(choice => hasId
                                ? choice.GetProperty("id").GetString() == choiceId.GetString()
                                : choice.GetProperty("id").GetString()!.Contains(choiceHint.GetString()!, StringComparison.Ordinal));
                            node["paymentId"] = metadata.GetProperty("paymentId").GetString();
                            node["paymentWindow"] = metadata.GetProperty("paymentWindow").GetString();
                            node["paymentChoiceIds"] = new JsonArray(JsonValue.Create(option.GetProperty("id").GetString()));
                        }
                        node["promptId"] = prompts[actor].PromptId;
                        node["snapshotTick"] = prompts[actor].SnapshotTick;
                        var raw = JsonSerializer.SerializeToElement(node);
                        var intent = "c8-network-" + kind + "-" + index++;
                        if (forbidden is null)
                        {
                            var peer = players.Keys.Single(player => player != actor);
                            var expected = new ExpectedError(peer, actor, ErrorCodes.IdentityMismatch);
                            expectedErrors.Enqueue(expected); expectedErrorEvidence.Add(expected);
                            forbidden = await connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room,
                                actor, "c8-wrong-authenticated-actor", raw, token);
                            Check(!forbidden.Accepted && forbidden.ErrorCode == ErrorCodes.IdentityMismatch,
                                "An authenticated peer must not submit the other player's intent.");
                        }
                        var receipt = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, intent, raw, token);
                        Check(receipt.Accepted, kind + " step " + (index - 1) + ": " + receipt.Message);
                        tick = receipt.ServerTick;
                        await WaitForViews(tick);
                        lastRaw = raw; lastActor = actor; lastIntent = intent;
                    }
                    Check(lastActor is not null && lastIntent is not null, "The network case must execute real commands.");
                    CheckBoundary(snapshots, prompts, p1, p2, item.GetProperty("copies").GetInt32(), item.GetProperty("pending").GetBoolean());
                    var expectedStale = new ExpectedError(lastActor!, lastActor!, ErrorCodes.PromptExpired);
                    expectedErrors.Enqueue(expectedStale); expectedErrorEvidence.Add(expectedStale);
                    var stale = await connections[lastActor!].InvokeAsync<CommandReceiptDto>("SubmitIntent", room,
                        lastActor, "c8-network-stale-" + kind, lastRaw, token);
                    Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.ServerTick == tick,
                        "A fresh intent carrying the previous prompt must be rejected without advancing state.");
                    await WaitForViews(tick);
                    resumes.Add(new { kind, room, tick, lastActor, lastIntent, raw = lastRaw,
                        tokens = joined.ToDictionary(p => p.Key, p => p.Value.ReconnectToken),
                        snapshots = snapshots.ToDictionary(p => p.Key, p => p.Value.Clone()),
                        prompts = prompts.ToDictionary(p => p.Key, p => p.Value), forbidden, stale });
                }
                else
                {
                    CheckBoundary(snapshots, prompts, p1, p2, item.GetProperty("copies").GetInt32(), item.GetProperty("pending").GetBoolean());
                    CompareSavedViews();
                    duplicateBefore = await CaptureSql(source, room, token);
                    var actor = previous.GetProperty("lastActor").GetString()!;
                    var duplicate = await connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                        previous.GetProperty("lastIntent").GetString()!, previous.GetProperty("raw"), token);
                    Check(duplicate.Accepted && duplicate.ServerTick == tick,
                        "After the actual API process restart, the original successful intent must remain idempotent.");
                    snapshots.Clear(); prompts.Clear();
                    foreach (var player in players.Keys)
                        await connections[player].InvokeAsync("RequestSnapshot", room, player, token);
                    await WaitForViews(tick);
                    CompareSavedViews();
                    duplicateAfter = await CaptureSql(source, room, token);
                    Check(SameSql(afterReconnect, duplicateBefore) && SameSql(duplicateBefore, duplicateAfter),
                        "Snapshot requests and the actual duplicate must each preserve the post-reconnect seven-table rows exactly.");
                    resumes.Add(new { kind, room, tick, lastActor = actor, lastIntent = previous.GetProperty("lastIntent").GetString(),
                        raw = previous.GetProperty("raw").Clone(), tokens = joined.ToDictionary(p => p.Key, p => p.Value.ReconnectToken),
                        snapshots = snapshots.ToDictionary(p => p.Key, p => p.Value.Clone()),
                        prompts = prompts.ToDictionary(p => p.Key, p => p.Value) });
                }
                finalSql = await CaptureSql(source, room, token);
                Check(serverErrors.IsEmpty && expectedErrors.IsEmpty, "No unexpected or missing network Error may reach a pass receipt.");
                reports.Add(new { kind, room, tick, authenticatedPlayers = players.Keys.ToArray(),
                    boundaryPreserved = true, originalIntentAfterActualRestart = recover });

                void CompareSavedViews()
                {
                    foreach (var player in players.Keys)
                    {
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("snapshots").GetProperty(player).GetRawText()),
                            JsonNode.Parse(snapshots[player].GetRawText())), "Actual API restart or duplicate changed a private snapshot.");
                        Check(JsonNode.DeepEquals(JsonNode.Parse(previous.GetProperty("prompts").GetProperty(player).GetRawText()),
                            JsonSerializer.SerializeToNode(prompts[player], json)), "Actual API restart or duplicate changed an authoritative prompt.");
                    }
                }

                async Task WaitForViews(long expectedTick)
                {
                    while (!expectedErrors.IsEmpty || players.Keys.Any(player => !joined.ContainsKey(player) || !snapshots.TryGetValue(player, out var snapshot)
                        || !prompts.TryGetValue(player, out var prompt) || snapshot.GetProperty("tick").GetInt64() != expectedTick
                        || prompt.SnapshotTick != expectedTick)) {
                        Check(serverErrors.IsEmpty, "Actual C8 reconnect/view failed: " + string.Join("; ", serverErrors));
                        await Task.Delay(20, token);
                    }
                    Check(serverErrors.IsEmpty, "Actual C8 reconnect/view failed: " + string.Join("; ", serverErrors));
                    foreach (var player in players.Keys)
                    {
                        var view = JsonSerializer.Serialize(new { snapshot = snapshots[player], prompt = prompts[player] }, json);
                        Check(!view.Contains("PRIVATE-P1", StringComparison.Ordinal) && !view.Contains("PRIVATE-P2", StringComparison.Ordinal)
                            && !view.Contains("reconnectToken", StringComparison.Ordinal) && !view.Contains("openingSeed", StringComparison.Ordinal),
                            "Never-public deck identities or server secrets escaped through the real network projection.");

                    }
                }
            }
            catch (Exception error)
            {
                failure = error.ToString();
                throw;
            }
            finally
            {
                await File.WriteAllTextAsync(Path.Combine(output, kind + (recover ? "-network-after-restart.json" : "-network-before-restart.json")),
                    JsonSerializer.Serialize(new { room, kind, recover, failure, snapshots, prompts, joined,
                        expectedErrors = expectedErrorEvidence, consumedTokens = players, beforeReconnect, afterReconnect, duplicateBefore, duplicateAfter, finalSql,
                        traces = traces.ToArray() }, json));
                foreach (var connection in connections.Values) await connection.DisposeAsync();
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, recover ? "network-resume-after-restart.json" : "network-resume.json"),
            JsonSerializer.Serialize(new { cases = resumes }, json));
        await File.WriteAllTextAsync(Path.Combine(output, recover ? "network-after-restart.json" : "network-before-restart.json"),
            JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_C8_NETWORK_PASS", recover, cases = reports,
                scope = "Two recorded finite C8 QA rooms: completed single copy and an original pending double-copy decision across actual API restart. Real Joined token rotation is required; SQL separately audits original birth, exact rows and pending forgeries before legitimate completion." }, json));
        Console.WriteLine(recover ? "NETWORK_COPIED_LOCAL_DESTRUCTION_COLD_RECOVER_PASS" : "NETWORK_COPIED_LOCAL_DESTRUCTION_PLAY_PASS");
    }

    private sealed record ExpectedError(string Recipient, string PlayerId, string Code);

    private static void CheckBoundary(IReadOnlyDictionary<string, JsonElement> snapshots,
        IReadOnlyDictionary<string, ActionPromptDto> prompts, string p1, string p2, int copies, bool pending)
    {
        Check(copies is 1 or 2, "Only the finite one/two Song positions are supported.");
        var objects = snapshots[p1].GetProperty("players").GetProperty(p1).GetProperty("objects");
        Check(objects.GetProperty("S").GetProperty("effectivePower").GetInt32() == 4, "Actual Soraka keeps literal four Might.");
        if (pending) {
            var candidate = prompts[p1].Candidates!.Single(c => c.Action == CommandTypes.PayCost && c.Enabled);
            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var options = metadata.GetProperty("paymentChoices").EnumerateArray().Select(choice => choice.GetProperty("id").GetString()!).ToArray();
            Check(prompts[p1].Actionable && !prompts[p2].Actionable && options.Length == 2 * copies,
                "Keep the host controller's actual copied group decision pending across restart.");
            foreach (var target in new[] { "BASE-1", "BASE-2" })
                foreach (var gear in copies == 1 ? new[] { "SONG" } : new[] { "SONG", "SONG-2" })
                    Check(options.Count(id => id.StartsWith($"LOCAL_RECALL:{target}:S:ATTACHED:{gear}:", StringComparison.Ordinal)) == 1,
                        "Each actual unused Song has exactly one option per base target, without restarting printed text.");
            Check(options.All(id => id.Contains(":ATTACHED:", StringComparison.Ordinal)), "Only unstarted copy instances remain.");
            return;
        }
        foreach (var id in new[] { "S", "FIELD-1", "FIELD-2", "BASE-1", "BASE-2" }) {
            var unit = objects.GetProperty(id);
            Check(unit.GetProperty("location").GetProperty("zone").GetString() == "BASE" && unit.GetProperty("isExhausted").GetBoolean()
                && unit.GetProperty("damage").GetInt32() == 0 && unit.GetProperty("controllerId").GetString() == p1,
                "Every literal unit must be recalled exhausted to its original controller's base through the real network.");
        }
        Check(objects.GetProperty("G1").GetProperty("location").GetProperty("zone").GetString() == "GRAVEYARD", "The real Hourglass must be destroyed.");
        Check(!prompts.Values.SelectMany(prompt => prompt.Candidates ?? []).Any(c => c.Action == CommandTypes.PayCost && c.Enabled),
            "A completed copied replacement group must not ask to apply the other instance again.");
    }
    private static void Check(bool value, string? message)
    { if (!value) throw new InvalidOperationException(message); }
}
