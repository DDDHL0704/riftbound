using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static NetworkReconnectSqlCapture;
using Riftbound.Contracts;

internal static class BattleWinnerPhaseNetworkAcceptance
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
            ?? throw new InvalidOperationException("C10 network evidence requires its real dedicated PostgreSQL connection."));
        Check(settings.Database == "riftbound_acceptance" && settings.Host is "127.0.0.1" or "localhost",
            "C10 network SQL sampling is restricted to the dedicated loopback acceptance database.");
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
            var replies = new QaNetworkReplyAudit(room, players, json);
            var expectedErrorEvidence = new List<ExpectedError>();
            var connections = new Dictionary<string, HubConnection>();
            SqlSample? beforeReconnect = null;
            long finalTick = 0;
            var p2TurnStarted = recover;
            SqlSample? afterReconnect = null, duplicateBefore = null, duplicateAfter = null, finalSql = null, terminalBefore = null, terminalAfter = null;
            string? failure = null;
            try
            {
                beforeReconnect = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
                foreach (var (player, reconnect) in players)
                {
                    var client = new HubConnectionBuilder().WithUrl(url + "/hubs/game").AddJsonProtocol(options =>
                        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter())).Build();
                    connections[player] = client;
                    foreach (var method in new[] { "Joined", "Snapshot", "Prompt", "Events", "Error" })
                        client.On<WsServerMessage>(method, message => {
                            traces.Enqueue(new { recipient = player, method, message });
                            replies.Observe(method, player, message);
                            if (message.Payload is not JsonElement payload) return;
                            if (method == "Snapshot") snapshots[player] = payload.Clone();
                            if (method == "Prompt") prompts[player] = payload.Deserialize<ActionPromptDto>(json)!;
                        });
                    await replies.UntilFaultAsync(client.StartAsync(token), token);
                    var credential = item.GetProperty("credentials").GetProperty(player).GetString()!;
                    var auth = await replies.UntilFaultAsync(client.InvokeAsync<AuthResultDto>("Authenticate", player, credential, token), token);
                    Check(auth.Authenticated, "Dedicated C10 battle-winner identity authentication failed.");
                    await replies.UntilFaultAsync(client.InvokeAsync("Reconnect", room, player, reconnect, token), token);
                    joined[player] = await replies.WaitForJoinedAsync(player, token);
                    await replies.UntilFaultAsync(client.InvokeAsync("RequestSnapshot", room, player, token), token);
                }
                var tick = recover ? previous.GetProperty("tick").GetInt64() : item.GetProperty("seedTick").GetInt64();
                await WaitForViews(tick);
                foreach (var player in players.Keys)
                    Check(joined[player].PlayerId == player && !string.IsNullOrWhiteSpace(joined[player].ReconnectToken)
                        && joined[player].ReconnectToken != players[player], "Each real Joined must supply its own rotated token.");
                afterReconnect = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
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
                            "$priority" or "$focus" or "$choice" or "$hand" => players.Keys.Single(player => prompts[player].Actionable
                                && prompts[player].Candidates?.Any(c => c.Action == action && c.Enabled) == true),
                            _ => throw new InvalidOperationException("Unknown server actor selector.")
                        };
                        var node = JsonNode.Parse(command.GetRawText())!.AsObject();
                        var hasId = step.TryGetProperty("choiceId", out var choiceId) && choiceId.ValueKind == JsonValueKind.String;
                        var hasHint = step.TryGetProperty("choiceHint", out var choiceHint) && choiceHint.ValueKind == JsonValueKind.String;
                        if (hasId || hasHint)
                        {
                            Check(hasId != hasHint, "A C10 decision must use exactly one server-option selector.");
                            var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.PayCost && c.Enabled);
                            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                            var option = metadata.GetProperty("paymentChoices").EnumerateArray().Single(choice => hasId
                                ? choice.GetProperty("id").GetString() == choiceId.GetString()
                                : choice.GetProperty("id").GetString()!.Contains(choiceHint.GetString()!, StringComparison.Ordinal));
                            node["paymentId"] = metadata.GetProperty("paymentId").GetString();
                            node["paymentWindow"] = metadata.GetProperty("paymentWindow").GetString();
                            node["paymentChoiceIds"] = new JsonArray(JsonValue.Create(option.GetProperty("id").GetString()));
                        }
                        if (step.TryGetProperty("handId", out var handId) && handId.ValueKind == JsonValueKind.String) {
                            Check(action == CommandTypes.ChooseHandCards && !hasId && !hasHint, "Use one actual private hand-choice selector.");
                            var candidate = prompts[actor].Candidates!.Single(c => c.Action == CommandTypes.ChooseHandCards && c.Enabled);
                            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                            Check(metadata.GetProperty("legalObjectIds").EnumerateArray().Any(id => id.GetString() == handId.GetString()),
                                "Choose only an actual legal card exposed to its own controller.");
                            node["choiceId"] = metadata.GetProperty("choiceId").GetString(); node["choiceWindow"] = metadata.GetProperty("choiceWindow").GetString();
                            node["chosenObjectIds"] = new JsonArray(JsonValue.Create(handId.GetString()));
                        }
                        node["promptId"] = prompts[actor].PromptId;
                        node["snapshotTick"] = prompts[actor].SnapshotTick;
                        var raw = JsonSerializer.SerializeToElement(node);
                        var intent = "c10-network-" + kind + "-" + index++;
                        if (forbidden is null)
                        {
                            var peer = players.Keys.Single(player => player != actor);
                            var expected = new ExpectedError(peer, actor, ErrorCodes.IdentityMismatch);
                            replies.ExpectError(peer, actor, ErrorCodes.IdentityMismatch); expectedErrorEvidence.Add(expected);
                            forbidden = await replies.UntilFaultAsync(connections[peer].InvokeAsync<CommandReceiptDto>("SubmitIntent", room,
                                actor, "c10-wrong-authenticated-actor", raw, token), token);
                            await replies.WaitForErrorAsync(peer, actor, ErrorCodes.IdentityMismatch, token);
                            Check(!forbidden.Accepted && forbidden.ErrorCode == ErrorCodes.IdentityMismatch,
                                "An authenticated peer must not submit the other player's intent.");
                        }
                        var receipt = await replies.UntilFaultAsync(connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor, intent, raw, token), token);
                        Check(receipt.Accepted, kind + " step " + (index - 1) + ": " + receipt.Message);
                        tick = receipt.ServerTick;
                        if (action == CommandTypes.EndTurn) p2TurnStarted = true;
                        await WaitForViews(tick);
                        lastRaw = raw; lastActor = actor; lastIntent = intent;
                    }
                    Check(lastActor is not null && lastIntent is not null, "The network case must execute real commands.");
                    CheckBoundary(snapshots, prompts, p1, p2, kind, item.GetProperty("pending").GetBoolean());
                    var expectedStale = new ExpectedError(lastActor!, lastActor!, ErrorCodes.PromptExpired);
                    replies.ExpectError(lastActor!, lastActor!, ErrorCodes.PromptExpired); expectedErrorEvidence.Add(expectedStale);
                    var stale = await replies.UntilFaultAsync(connections[lastActor!].InvokeAsync<CommandReceiptDto>("SubmitIntent", room,
                        lastActor, "c10-network-stale-" + kind, lastRaw, token), token);
                    await replies.WaitForErrorAsync(lastActor!, lastActor!, ErrorCodes.PromptExpired, token);
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
                    CheckBoundary(snapshots, prompts, p1, p2, kind, item.GetProperty("pending").GetBoolean());
                    CompareSavedViews();
                    duplicateBefore = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
                    var actor = previous.GetProperty("lastActor").GetString()!;
                    var duplicate = await replies.UntilFaultAsync(connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room, actor,
                        previous.GetProperty("lastIntent").GetString()!, previous.GetProperty("raw"), token), token);
                    Check(duplicate.Accepted && duplicate.ServerTick == tick,
                        "After the actual API process restart, the original successful intent must remain idempotent.");
                    snapshots.Clear(); prompts.Clear();
                    foreach (var player in players.Keys)
                        await replies.UntilFaultAsync(connections[player].InvokeAsync("RequestSnapshot", room, player, token), token);
                    await WaitForViews(tick);
                    CompareSavedViews();
                    duplicateAfter = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
                    Check(SameSql(afterReconnect, duplicateBefore) && SameSql(duplicateBefore, duplicateAfter),
                        "Snapshot requests and the actual duplicate must each preserve the post-reconnect seven-table rows exactly.");
                    resumes.Add(new { kind, room, tick, lastActor = actor, lastIntent = previous.GetProperty("lastIntent").GetString(),
                        raw = previous.GetProperty("raw").Clone(), tokens = joined.ToDictionary(p => p.Key, p => p.Value.ReconnectToken),
                        snapshots = snapshots.ToDictionary(p => p.Key, p => p.Value.Clone()),
                        prompts = prompts.ToDictionary(p => p.Key, p => p.Value) });
                }
                if (kind is "final-conquest" or "result-burnout") {
                    var actor = kind == "final-conquest" ? p2 : p1;
                    terminalBefore = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
                    var terminalRaw = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.EndTurn,
                        promptId = prompts[actor].PromptId, snapshotTick = tick }, json);
                    replies.ExpectError(actor, actor, ErrorCodes.MatchFinished); expectedErrorEvidence.Add(new(actor, actor, ErrorCodes.MatchFinished));
                    var rejected = await replies.UntilFaultAsync(connections[actor].InvokeAsync<CommandReceiptDto>("SubmitIntent", room,
                        actor, "c10-fresh-terminal-intent-" + (recover ? "after-" : "before-") + kind, terminalRaw, token), token);
                    await replies.WaitForErrorAsync(actor, actor, ErrorCodes.MatchFinished, token);
                    Check(!rejected.Accepted && rejected.ErrorCode == ErrorCodes.MatchFinished, "Fresh terminal intent must be rejected with MATCH_FINISHED.");
                    await WaitForViews(tick);
                    terminalAfter = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
                    Check(SameSql(terminalBefore, terminalAfter), "Terminal rejection must preserve all seven SQL tables exactly.");
                }
                finalSql = await replies.UntilFaultAsync(CaptureSql(source, room, token), token);
                replies.CheckHealthy(); finalTick = tick;

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
                    replies.CheckHealthy();
                    while (players.Keys.Any(player => !joined.ContainsKey(player) || !snapshots.TryGetValue(player, out var snapshot)
                        || !prompts.TryGetValue(player, out var prompt) || snapshot.GetProperty("tick").GetInt64() != expectedTick
                        || prompt.SnapshotTick != expectedTick)) {
                        replies.CheckHealthy();
                        await replies.UntilFaultAsync(Task.Delay(20, token), token);
                    }
                    replies.CheckHealthy();
                    foreach (var player in players.Keys)
                    {
                        CheckPrivateProjection(snapshots[player], prompts[player], p2TurnStarted);

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
                var teardownFailures = new List<string>();
                foreach (var connection in connections.Values) {
                    try { await connection.DisposeAsync(); }
                    catch (Exception error) { teardownFailures.Add(error.ToString()); }
                }
                // All callbacks through completed disposal must be included in
                // the final audit and trace, before any case can report PASS.
                try { replies.AssertComplete(); }
                catch (Exception error) { teardownFailures.Add(error.ToString()); }
                var originalFailure = failure;
                failure ??= teardownFailures.Count == 0 ? null : string.Join("\n", teardownFailures);
                await File.WriteAllTextAsync(Path.Combine(output, kind + (recover ? "-network-after-restart.json" : "-network-before-restart.json")),
                    JsonSerializer.Serialize(new { room, kind, recover, failure, teardownFailures, snapshots, prompts, joined,
                        expectedErrors = expectedErrorEvidence, consumedTokens = players, beforeReconnect, afterReconnect, duplicateBefore, duplicateAfter, terminalBefore, terminalAfter, finalSql,
                        traces = traces.ToArray() }, json));
                if (originalFailure is null && teardownFailures.Count != 0)
                    throw new InvalidOperationException("C10 reply/disposal audit failed: " + failure);
            }
            reports.Add(new { kind, room, tick = finalTick, authenticatedPlayers = players.Keys.ToArray(),
                boundaryPreserved = true, originalIntentAfterActualRestart = recover });
        }
        await File.WriteAllTextAsync(Path.Combine(output, recover ? "network-resume-after-restart.json" : "network-resume.json"),
            JsonSerializer.Serialize(new { cases = resumes }, json));
        await File.WriteAllTextAsync(Path.Combine(output, recover ? "network-after-restart.json" : "network-before-restart.json"),
            JsonSerializer.Serialize(new { status = "REAL_DUAL_PLAYER_C10_NETWORK_PASS", recover, cases = reports,
                scope = "Five recorded finite C10 QA rooms: plain/skipped winners, an original pending control hand choice, normal winning conquest and result-draw burnout across actual API restart. Real Joined token rotation and terminal rejection invariance are required; SQL separately audits original birth, exact rows and terminal forgeries before legitimate control completion." }, json));
        Console.WriteLine(recover ? "NETWORK_BATTLE_WINNER_PHASE_COLD_RECOVER_PASS" : "NETWORK_BATTLE_WINNER_PHASE_PLAY_PASS");
    }

    private sealed record ExpectedError(string Recipient, string PlayerId, string Code);

    private static void CheckBoundary(IReadOnlyDictionary<string, JsonElement> snapshots,
        IReadOnlyDictionary<string, ActionPromptDto> prompts, string p1, string p2, string kind, bool pending)
    {
        var view = snapshots[p1]; var timing = view.GetProperty("timing");
        JsonElement Object(string id) => view.GetProperty("players").EnumerateObject()
            .Select(player => player.Value.GetProperty("objects")).Single(objects => objects.TryGetProperty(id, out _)).GetProperty(id);
        var terminal = kind is "final-conquest" or "result-burnout"; var burnout = kind == "result-burnout";
        var guarded = terminal || kind == "control-observer";
        Check(timing.GetProperty("roomStatus").GetString() == (terminal ? "FINISHED" : "IN_PROGRESS")
            && timing.GetProperty("winnerPlayerId").GetString() == (terminal ? burnout ? p1 : p2 : null),
            "Preserve the real normal or burnout winner through the network projection.");
        Check(view.GetProperty("players").GetProperty(p1).GetProperty("score").GetInt32() == (burnout ? 8 : 0)
            && view.GetProperty("players").GetProperty(p2).GetProperty("score").GetInt32() == (kind == "final-conquest" ? 8 : burnout ? 0 : 1),
            "Network projections retain exact final-point and burnout scores.");
        Check(Object("HOME").GetProperty("controllerId").GetString() == (burnout ? p1 : p2)
            && Object("ATTACKER").GetProperty("damage").GetInt32() == 0
            && timing.GetProperty("battle").GetProperty("isActive").GetBoolean() == (terminal || pending),
            "Keep cleanup, control and the precise pre-close cutoff visible after restart.");
        Check(Object("HOST").GetProperty("location").GetProperty("zone").GetString() == (guarded ? "BASE" : "GRAVEYARD"),
            "The actual host was recalled by its source or destroyed on the unprotected path.");
        if (guarded) Check(Object("ANGEL-2").GetProperty("location").GetProperty("zone").GetString() == "GRAVEYARD"
            && Object("ANGEL").GetProperty("attachedToObjectId").GetString() == "HOST"
            && Object("ANGEL").GetProperty("location").GetProperty("zone").GetString() == "BASE"
            && Object("HOST").GetProperty("isExhausted").GetBoolean() && Object("HOST").GetProperty("damage").GetInt32() == 0,
            "Preserve the real selected Guardian and surviving attached group at either phase cutoff.");
        if (pending) {
            var candidate = prompts[p2].Candidates!.Single(c => c.Action == CommandTypes.ChooseHandCards && c.Enabled);
            var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Check(prompts[p2].Actionable && !prompts[p1].Actionable && metadata.GetProperty("requiredCount").GetInt32() == 1
                && metadata.GetProperty("legalObjectIds").EnumerateArray().Any(id => id.GetString() == "P2-DRAW-1"),
                "Keep the exact original controller's private uncommitted control-skill hand choice.");
            Check(!snapshots[p1].GetProperty("timing").GetProperty("pendingHandChoice").TryGetProperty("legalObjectIds", out _),
                "The waiting peer must not receive the private legal hand identities.");
        }
        if (terminal) Check(prompts.Values.All(prompt => !prompt.Actionable), "A terminal snapshot must have no actionable prompt.");
        Check(!prompts.Values.SelectMany(prompt => prompt.Candidates ?? []).Any(c => c.Action == CommandTypes.PayCost && c.Enabled),
            "The completed replacement must not request another Guardian for the same inherited event.");
    }
    private static void CheckPrivateProjection(JsonElement snapshot, ActionPromptDto prompt, bool p2TurnStarted)
    {
        var view = JsonSerializer.Serialize(new { snapshot, prompt }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // Both P2 runes in this finite fixture are normally channeled by the
        // one real EndTurn. Their ids become public only after that receipt.
        var privateIds = p2TurnStarted ? new[] { "PRIVATE-P1", "PRIVATE-P2", "PRIVATE-P1-RUNE-TAIL" }
            : new[] { "PRIVATE-P1", "PRIVATE-P2", "PRIVATE-P1-RUNE-TAIL", "PRIVATE-P2-RUNE-TAIL" };
        Check(privateIds.All(id => !view.Contains(JsonSerializer.Serialize(id), StringComparison.Ordinal))
            && !view.Contains("reconnectToken", StringComparison.Ordinal) && !view.Contains("openingSeed", StringComparison.Ordinal),
            "Still-private finite QA deck identities or server secrets escaped through the real network projection.");
    }
    private static void Check(bool value, string? message)
    { if (!value) throw new InvalidOperationException(message); }
}
