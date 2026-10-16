using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class OpponentBattlefieldControlSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource data, string output)
    {
        foreach (var name in new[] { "neutral", "swapped" })
        {
            var ready = name == "swapped";
            var directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "opponent-battlefield-control-" + name + ".json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var fixture = document.RootElement; var room = fixture.GetProperty("sourceRoomId").GetString()!;
            var id = fixture.GetProperty("expectedSourceObjectId").GetString()!;
            const string card = "OGN·035/298";
            Check(fixture.GetProperty("expectedCardNo").GetString() == card
                && fixture.GetProperty("expectedReady").GetBoolean() == ready
                && fixture.GetProperty("expectedMana").GetInt32() == 4, "Independent printing/control expectation required.");
            await using var connection = await data.OpenConnectionAsync();
            await using (var exists = new NpgsqlCommand("select exists(select 1 from matches where match_id=@room)", connection))
            { exists.Parameters.AddWithValue("room", room); Check(!(bool)(await exists.ExecuteScalarAsync())!, "Fresh dedicated SQL opening required."); }
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial is { Tick: 0, Seed: 0, Status: "SEATING" } && initial.CardObjects.Count == 0,
                "Start a new empty room; do not import or migrate a development match.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(data);
            var players = new PostgresMatchPlayerStore(data); var store = new PostgresMatchRecoveryStore(data);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var checks = new List<object>();
            try
            {
                foreach (var actor in initial.Seats.Keys)
                    await players.SavePlayerSessionAsync(room, actor, initial.Seats[actor], ReconnectTokenHasher.Hash("control-sql-" + actor), default);
                foreach (var row in fixture.GetProperty("acceptedCommands").EnumerateArray())
                {
                    var actor = row.GetProperty("playerId").GetString()!; var intent = row.GetProperty("clientIntentId").GetString()!;
                    var raw = row.GetProperty("rawCommand");
                    var result = row.GetProperty("commandType").GetString() switch {
                        "SUBMIT_DECK" => await session.SubmitDeckAsync(actor, intent, raw.Deserialize<SubmitDeckCommand>(json)!, raw, default),
                        "READY" => await session.ReadyAsync(actor, intent, raw, default),
                        "SURRENDER" => throw new InvalidOperationException("No surrender in this normal fixture."),
                        var kind when kind!.StartsWith("DEV_", StringComparison.Ordinal) => throw new InvalidOperationException("No developer state command."),
                        _ => await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(raw), raw, default)
                    };
                    Check(result.Accepted, result.ErrorMessage);
                }
                var prefix = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("SQL prefix missing.");
                Check(prefix.IsConsistent && prefix.Commands.Count == fixture.GetProperty("acceptedCommands").GetArrayLength()
                    && prefix.ReplayInitialState is { Tick: 0, Seed: 0 } opening && opening.CardObjects.Count == 0,
                    "Original empty opening and every ordinary intent must be retained.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                var before = prefix.AuthoritativeState!;
                var ownField = fixture.GetProperty("expectedOwnBattlefieldObjectId").GetString()!;
                var otherField = fixture.GetProperty("expectedOtherBattlefieldObjectId").GetString()!;
                Check(before.CardObjects[ownField].OwnerId == "P1" && before.CardObjects[otherField].OwnerId == "P2"
                    && before.CardObjects[ownField].ControllerId == (ready ? "P2" : null)
                    && before.CardObjects[otherField].ControllerId == (ready ? "P1" : null)
                    && before.PlayerZones["P1"].Hand.Contains(id), "Actual neutral/swapped control and own-hand source required.");
                var command = new PlayCardCommand(id, card, [], OptionalCosts: []);
                var quote = engine.PreviewPlayCard(before, "P1", new("sql-control-quote", session.PromptFor("P1").PromptId!, before.Tick, command));
                Check(quote.CanPay && quote.Cost!.Mana == 4, "Control does not change the independent printed mana cost.");
                var accepted = await session.SubmitAsync("P1", "sql-current-control-entry", command, JsonSerializer.SerializeToElement(command, json), default);
                Check(accepted.Accepted, accepted.ErrorMessage); var state = accepted.State;
                Check(state.PlayerZones["P1"].Base.Contains(id) && state.CardObjects[id].IsExhausted == !ready
                    && state.RunePools["P1"].Mana == before.RunePools["P1"].Mana - 4
                    && state.RunePools["P1"].TotalPower == before.RunePools["P1"].TotalPower - 1,
                    "Actual SQL entry must match the independently specified control branch and printed cost.");
                var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Entry SQL frame missing.");
                var commands = JsonSerializer.Serialize(original.Commands, json); var events = JsonSerializer.Serialize(original.Events, json);
                var hash = MatchStateHasher.Hash(state);
                await using var read = new NpgsqlCommand("select event_sequence,payload::text from game_events where match_id=@room and event_type='UNIT_PLAYED_TO_BASE' order by event_sequence desc limit 1", connection);
                read.Parameters.AddWithValue("room", room); long sequence; string saved;
                await using (var reader = await read.ExecuteReaderAsync())
                { Check(await reader.ReadAsync(), "Actual persisted Vayne entry required."); sequence = reader.GetInt64(0); saved = reader.GetString(1); }
                var actual = JsonNode.Parse(saved)!["payload"]!;
                Check(actual["sourceObjectId"]!.GetValue<string>() == id && actual["isExhausted"]!.GetValue<bool>() == !ready
                    && actual.AsObject().ContainsKey("entryStaticAbilityKind") == ready, "Persist the exact source and control-derived entry metadata.");
                foreach (var mutation in new[] { "flipped-entry-exhaustion", ready ? "missing-entry-ability" : "forged-entry-ability" })
                {
                    var node = JsonNode.Parse(saved)!; var payload = node["payload"]!;
                    if (mutation == "flipped-entry-exhaustion") payload["isExhausted"] = ready;
                    else if (ready)
                    {
                        payload.AsObject().Remove("entryStaticAbilityKind"); payload.AsObject().Remove("entryStaticAbilitySourceObjectId");
                        payload.AsObject().Remove("entryStaticAbilitySourceCardNo");
                    }
                    else
                    {
                        payload["entryStaticAbilityKind"] = StaticAbilityKinds.SourceUnitEnterReady;
                        payload["entryStaticAbilitySourceObjectId"] = id; payload["entryStaticAbilitySourceCardNo"] = card;
                    }
                    try
                    {
                        await Replace(node.ToJsonString());
                        var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Mutated frame missing.");
                        Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == hash && JsonSerializer.Serialize(bad.Commands, json) == commands,
                            "Event mutation must leave authoritative state and original commands untouched.");
                        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, bad.Commands, state, engine, default, bad.Events);
                        Check(!replay.IsMatch && replay.Errors.Any(e => e.Contains("payload hash", StringComparison.Ordinal)), "Original event replay must reject the false entry.");
                        var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                        try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged entry accepted."); }
                        catch (MatchSessionException error) { Check(error.Code == ErrorCodes.RecoveryInconsistent, error.Message); }
                        Check(await registry.FindAsync(room, default) is null, "Rejected frame cannot enter live cache.");
                        checks.Add(new { mutation, eventSequence = sequence, originalReplayRejected = true, productionRegistryRejected = true });
                    }
                    finally { await Replace(saved); }
                    var restored = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Restored frame missing.");
                    Check(JsonSerializer.Serialize(restored.Commands, json) == commands && JsonSerializer.Serialize(restored.Events, json) == events
                        && MatchStateHasher.Hash(restored.AuthoritativeState!) == hash, "Restore exact original state, commands and events after each rejection.");
                }
                var final = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Final frame missing.");
                Check(final.IsConsistent && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default)).Count == 0,
                    "Full original action/event/state replay required after restoration.");
                await File.WriteAllTextAsync(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_POSTGRES_CURRENT_BATTLEFIELD_CONTROLLER_EMPTY_OPENING_COLD_ENTRY_EVENT_REJECTION_PASS",
                    room, cardNo = card, sourceObjectId = id, ownField, otherField, expectedReady = ready,
                    expectedMana = 4, quoteMana = quote.Cost!.Mana, expectedPower = 1,
                    originalOpeningTick = initial.Tick, originalOpeningSeed = initial.Seed, originalOpeningCards = initial.CardObjects.Count,
                    prefixCommands = prefix.Commands.Count, prefixEvents = prefix.Events.Count,
                    finalCommands = final.Commands.Count, finalEvents = final.Events.Count, checks,
                    finalHash = hash, ruleInputs = state.RuleInputs, originalHistoryRestoredExactly = true,
                    fullOriginalActionEventStateReplayMatched = true,
                    fixtureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant(),
                    scope = "Actual OGN035 neutral and swapped owner/controller branches from fresh legal empty openings; no migration of old native matches. Other printing semantics, new visible candidate and Windows remain separate."
                }, json));
                Console.WriteLine($"POSTGRES_OPPONENT_BATTLEFIELD_CONTROL_PASS: {name}, ready={ready}, {final.Commands.Count}commands/{final.Events.Count}events,2 entry-event rejections");
                async Task Replace(string payload)
                {
                    await using var update = new NpgsqlCommand("update game_events set payload=@payload::jsonb where match_id=@room and event_sequence=@sequence", connection);
                    update.Parameters.AddWithValue("payload", payload); update.Parameters.AddWithValue("room", room); update.Parameters.AddWithValue("sequence", sequence);
                    Check(await update.ExecuteNonQueryAsync() == 1, "Mutate exactly one event in this dedicated disposable scenario.");
                }
            }
            finally
            {
                await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
                cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
