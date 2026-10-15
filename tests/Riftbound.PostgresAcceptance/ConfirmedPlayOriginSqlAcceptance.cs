using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class ConfirmedPlayOriginSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        foreach (var fromHand in new[] { true, false })
            await RunBranch(source, Path.Combine(output, fromHand ? "hand" : "standby"), fromHand);
    }

    private static async Task RunBranch(NpgsqlDataSource source, string output, bool fromHand)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures",
            "confirmed-play-origin-" + (fromHand ? "hand" : "standby") + "-cn16.json");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
        var root = fixture.RootElement;
        var room = root.GetProperty("sourceRoomId").GetString()!;
        var sourceId = root.GetProperty("expectedSourceObjectId").GetString()!;
        Check(root.GetProperty("expectedPlayedFromHand").GetBoolean() == fromHand,
            "The independent fixture expectation must match this branch.");
        await using var connection = await source.OpenConnectionAsync();
        await using (var exists = new NpgsqlCommand("select exists(select 1 from matches where match_id=@room)", connection))
        {
            exists.Parameters.AddWithValue("room", room);
            Check(!(bool)(await exists.ExecuteScalarAsync())!, "Use a fresh dedicated acceptance database.");
        }
        // Preserve room-dependent randomness, but construct a new match using
        // current inputs. No stored snapshot or old match is imported.
        var initial = MatchReplayInitialStateBuilder.FromSeats(room,
            root.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0,
            "The SQL proof must start at empty seating.");
        var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var checks = new List<object>();
        try
        {
            foreach (var actor in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, actor, initial.Seats[actor],
                    ReconnectTokenHasher.Hash("origin-sql-" + actor), default);
            foreach (var recorded in root.GetProperty("acceptedCommands").EnumerateArray())
            {
                var actor = recorded.GetProperty("playerId").GetString()!;
                var intent = recorded.GetProperty("clientIntentId").GetString()!;
                var raw = recorded.GetProperty("rawCommand");
                var result = recorded.GetProperty("commandType").GetString() switch
                {
                    "SUBMIT_DECK" => await session.SubmitDeckAsync(actor, intent, raw.Deserialize<SubmitDeckCommand>(json)!, raw, default),
                    "READY" => await session.ReadyAsync(actor, intent, raw, default),
                    "MULLIGAN" => await session.SubmitAsync(actor, intent, raw.Deserialize<MulliganCommand>(json)!, raw, default),
                    "END_TURN" => await session.SubmitAsync(actor, intent, raw.Deserialize<EndTurnCommand>(json)!, raw, default),
                    "TAP_RUNE" => await session.SubmitAsync(actor, intent, raw.Deserialize<TapRuneCommand>(json)!, raw, default),
                    "RECYCLE_RUNE" => await session.SubmitAsync(actor, intent, raw.Deserialize<RecycleRuneCommand>(json)!, raw, default),
                    "MOVE_UNIT" => await session.SubmitAsync(actor, intent, raw.Deserialize<MoveUnitCommand>(json)!, raw, default),
                    "PASS_FOCUS" => await session.SubmitAsync(actor, intent, raw.Deserialize<PassFocusCommand>(json)!, raw, default),
                    "HIDE_CARD" => await session.SubmitAsync(actor, intent, raw.Deserialize<HideCardCommand>(json)!, raw, default),
                    "PLAY_CARD" => await session.SubmitAsync(actor, intent, raw.Deserialize<PlayCardCommand>(json)!, raw, default),
                    _ => throw new InvalidOperationException("Unexpected fresh normal-opening command.")
                };
                Check(result.Accepted, result.ErrorMessage);
            }
            var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("SQL frame missing.");
            var state = original.AuthoritativeState!;
            var played = state.StackItems.Single(item => item.SourceObjectId == sourceId);
            Check(original.IsConsistent && original.Commands.Count == root.GetProperty("acceptedCommands").GetArrayLength()
                && original.ReplayInitialState is { Tick: 0, Seed: 0 } opening && opening.CardObjects.Count == 0,
                "Actual SQL must retain the complete original opening.");
            Check(played.PlayedFromHand == fromHand && (played.PlayedFromStandby is not null) == !fromHand
                && state.CardObjects[sourceId].CardNo == "UNL-042/219", "Actual persisted printing/origin is incorrect.");
            Check(RuleInputIdentity.IsCurrent(state.RuleInputs), "SQL must retain the actual current CN16 input identity.");
            var logBefore = JsonSerializer.Serialize(new { original.Commands, original.Events }, json);
            var acceptedHistorySha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(logBefore))).ToLowerInvariant();
            await using var read = new NpgsqlCommand(
                "select payload::text from state_snapshots where match_id=@room and state_tick=@tick and last_event_sequence=@seq", connection);
            read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("tick", state.Tick);
            read.Parameters.AddWithValue("seq", original.LastEventSequence);
            var saved = (string)(await read.ExecuteScalarAsync())!;
            foreach (var mutation in new[] { "flipped-origin", "missing-origin" })
            {
                var node = JsonNode.Parse(saved)!;
                var item = node["stackItems"]!.AsArray().Single(value => value!["sourceObjectId"]!.GetValue<string>() == sourceId)!.AsObject();
                Check(item["playedFromHand"]!.GetValue<bool>() == fromHand, "SQL origin must be explicitly serialized.");
                if (mutation == "flipped-origin") item["playedFromHand"] = !fromHand;
                else Check(item.Remove("playedFromHand"), "The missing-origin mutation must remove a real field.");
                try
                {
                    await Replace(node.ToJsonString());
                    var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Forged SQL frame missing.");
                    Check(logBefore == JsonSerializer.Serialize(new { bad.Commands, bad.Events }, json),
                        "Snapshot mutations must never replace accepted commands/events.");
                    if (mutation == "flipped-origin")
                        Check(bad.IsConsistent, "The false but well-formed fact must reach the independent causal guard.");
                    else Check(!bad.IsConsistent && bad.AuthoritativeState is null,
                        "A missing required fact must fail deserialization without inventing a default.");
                    var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                    try
                    {
                        await registry.GetOrCreateAsync(room, default);
                        throw new InvalidOperationException("Forged SQL origin accepted: " + mutation);
                    }
                    catch (MatchSessionException error)
                    {
                        Check(error.Code == ErrorCodes.RecoveryInconsistent, error.Message);
                        if (mutation == "flipped-origin") Check(error.Message.Contains("replayed final state hash", StringComparison.Ordinal),
                            "A flipped origin must be rejected by complete original-action replay: " + error.Message);
                    }
                    Check(await registry.FindAsync(room, default) is null, "Rejected state must not enter the live registry.");
                    checks.Add(new { mutation, structuralValidationPassed = bad.IsConsistent, productionRegistryRejected = true });
                }
                finally { await Replace(saved); }
                Check((string)(await read.ExecuteScalarAsync())! == saved, "Restore the exact original SQL JSON after every mutation.");
            }
            var clean = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Restored SQL frame missing.");
            Check(clean.IsConsistent && logBefore == JsonSerializer.Serialize(new { clean.Commands, clean.Events }, json)
                && MatchStateHasher.Hash(clean.AuthoritativeState!) == MatchStateHasher.Hash(state),
                "Accepted history and original state must survive every negative test.");
            session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
            var handBefore = state.PlayerZones["P1"].Hand.Count;
            var drawEvents = 0;
            for (var pass = 0; state.StackItems.Count > 0 && pass < 4; pass++)
            {
                var command = new PassPriorityCommand();
                var result = await session.SubmitAsync(state.PriorityPlayerId!, "origin-sql-resolve-" + pass,
                    command, JsonSerializer.SerializeToElement(command, json), default);
                Check(result.Accepted, result.ErrorMessage); state = result.State;
                drawEvents += result.Events.Count(ev => ev.Kind == "CARD_DRAWN");
            }
            Check(state.StackItems.Count == 0 && state.PlayerZones["P1"].Hand.Count - handBefore == (fromHand ? 1 : 0)
                && drawEvents == (fromHand ? 1 : 0), "Cold SQL recovery must preserve actual conditional drawing.");
            var final = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Resolved SQL frame missing.");
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, final.Commands, state, engine, default, final.Events);
            Check(final.IsConsistent && replay.IsMatch, string.Join("; ", replay.Errors));
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_CONFIRMED_PLAY_ORIGIN_CAUSAL_RECOVERY_REPLAY_PASS", room,
                fromHand, cardNo = "UNL-042/219", sourceId, originalCommands = original.Commands.Count,
                originalEvents = original.Events.Count, finalCommands = final.Commands.Count, finalEvents = final.Events.Count,
                acceptedHistorySha256, checks, drawEvents, finalHash = MatchStateHasher.Hash(state), ruleInputs = state.RuleInputs,
                fixtureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fixturePath))).ToLowerInvariant(),
                scope = "Fresh empty-opening hand/standby commands in actual SQL; snapshot-only flipped/missing source rejection, unchanged accepted history, exact restoration, cold resolution and full command/event/state replay. Other origins/control branches and whole-card certification remain separate."
            }, json));
            Console.WriteLine($"POSTGRES_CONFIRMED_PLAY_ORIGIN_PASS: hand={fromHand}, {final.Commands.Count} commands, {final.Events.Count} events, 2 tamper rejections, {drawEvents} draws");

            async Task Replace(string payload)
            {
                await using var replace = new NpgsqlCommand(
                    "update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=@tick and last_event_sequence=@seq", connection);
                replace.Parameters.AddWithValue("payload", payload); replace.Parameters.AddWithValue("room", room);
                replace.Parameters.AddWithValue("tick", original.CurrentTick); replace.Parameters.AddWithValue("seq", original.LastEventSequence);
                Check(await replace.ExecuteNonQueryAsync() == 1, "Mutate only this dedicated final snapshot.");
            }
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
            cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
