using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class OptionalSacrificeSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource data, string output)
    {
        foreach (var name in new[] { "hand", "haste" })
        {
            var ready = name == "haste"; var mana = ready ? 8 : 7; var power = ready ? 4 : 3;
            const string card = "UNL-170/219";
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
                WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "current-opening-20261017", "optional-sacrifice-" + name + ".json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
            var fixture = document.RootElement; var room = fixture.GetProperty("sourceRoomId").GetString()!;
            var source = fixture.GetProperty("expectedSourceObjectId").GetString()!;
            var victim = fixture.GetProperty("expectedSacrificeObjectId").GetString()!;
            Check(fixture.GetProperty("expectedCardNo").GetString() == card
                && fixture.GetProperty("expectedSacrificeCardNo").GetString() == "SFD·029/221"
                && fixture.GetProperty("expectedMana").GetInt32() == mana
                && fixture.GetProperty("expectedPower").GetInt32() == power
                && fixture.GetProperty("expectedReady").GetBoolean() == ready, "Independent exact printing and cost expectations required.");
            await using var connection = await data.OpenConnectionAsync();
            await using (var exists = new NpgsqlCommand("select exists(select 1 from matches where match_id=@room)", connection))
            { exists.Parameters.AddWithValue("room", room); Check(!(bool)(await exists.ExecuteScalarAsync())!, "Use a fresh dedicated SQL room."); }
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial is { Tick: 0, Seed: 0, Status: "SEATING" } && initial.CardObjects.Count == 0, "New empty opening required.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(data);
            var players = new PostgresMatchPlayerStore(data); var store = new PostgresMatchRecoveryStore(data);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var checks = new List<object>();
            try
            {
                foreach (var actor in initial.Seats.Keys)
                    await players.SavePlayerSessionAsync(room, actor, initial.Seats[actor], ReconnectTokenHasher.Hash("optional-sacrifice-sql-" + actor), default);
                foreach (var row in fixture.GetProperty("acceptedCommands").EnumerateArray())
                {
                    var actor = row.GetProperty("playerId").GetString()!; var intent = row.GetProperty("clientIntentId").GetString()!;
                    var raw = row.GetProperty("rawCommand");
                    var result = row.GetProperty("commandType").GetString() switch {
                        "SUBMIT_DECK" => await session.SubmitDeckAsync(actor, intent, raw.Deserialize<SubmitDeckCommand>(json)!, raw, default),
                        "READY" => await session.ReadyAsync(actor, intent, raw, default),
                        "SURRENDER" => throw new InvalidOperationException("No surrender in this fixture."),
                        var kind when kind!.StartsWith("DEV_", StringComparison.Ordinal) => throw new InvalidOperationException("No developer state command."),
                        _ => await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(raw), raw, default)
                    };
                    Check(result.Accepted, result.ErrorMessage);
                }
                var prefix = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL prefix.");
                Check(prefix.IsConsistent && prefix.ReplayInitialState is { Tick: 0, Seed: 0 } opening
                    && opening.CardObjects.Count == 0 && prefix.Commands.Count == fixture.GetProperty("acceptedCommands").GetArrayLength(),
                    "Retain the real initial state and every original intent.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                var before = prefix.AuthoritativeState!;
                Check(before.CardObjects[victim].CardNo == "SFD·029/221" && before.CardObjects[victim].ControllerId == "P1"
                    && before.ObjectLocations[victim].Zone is "BASE" or "BATTLEFIELD"
                    && before.ObjectLocations[source].Zone == (ready ? "STACK" : "HAND"), "Actual current sacrifice and source zones required.");
                var costs = new[] { "DESTROY_FRIENDLY_UNIT:" + victim }
                    .Concat(ready ? [HasteOptionalCostNames.HasteReady] : Array.Empty<string>()).ToArray();
                var command = new PlayCardCommand(source, card, [], OptionalCosts: costs);
                var quote = engine.PreviewPlayCard(before, "P1", new("sql-sacrifice-quote", session.PromptFor("P1").PromptId!, before.Tick, command));
                Check(quote.CanPay && quote.Cost!.Mana == mana && quote.Cost.GenericPower == 0
                    && quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Yellow) == power, "Independent quote must be7/3 or8/4, not derived from production helpers.");
                var accepted = await session.SubmitAsync("P1", "sql-optional-sacrifice-confirm", command, JsonSerializer.SerializeToElement(command, json), default);
                Check(accepted.Accepted, accepted.ErrorMessage); var state = accepted.State;
                Check(state.PlayerZones["P1"].Graveyard.Contains(victim) && state.PlayerZones["P1"].Base.Contains(source)
                    && state.CardObjects[source].IsExhausted == !ready
                    && state.RunePools["P1"].Mana == before.RunePools["P1"].Mana - mana
                    && state.RunePools["P1"].TotalPower == before.RunePools["P1"].TotalPower - power,
                    "Real payment, destruction and paid entry must agree with independent expectations.");
                var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing paid SQL frame.");
                var commands = JsonSerializer.Serialize(original.Commands, json); var events = JsonSerializer.Serialize(original.Events, json);
                var hash = MatchStateHasher.Hash(state);
                foreach (var mutation in new[] { "mana", "power", "entry-exhaustion", "selected-haste" })
                {
                    var kind = mutation is "mana" or "power" ? "COST_PAID" : "UNIT_PLAYED_TO_BASE";
                    await using var read = new NpgsqlCommand("select event_sequence,payload::text from game_events where match_id=@room and event_type=@kind order by event_sequence desc limit 1", connection);
                    read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("kind", kind);
                    long sequence; string saved;
                    await using (var reader = await read.ExecuteReaderAsync())
                    { Check(await reader.ReadAsync(), "Actual persisted cost/entry event required."); sequence = reader.GetInt64(0); saved = reader.GetString(1); }
                    var node = JsonNode.Parse(saved)!; var payload = node["payload"]!;
                    Check(payload["sourceObjectId"]!.GetValue<string>() == source, "Mutate this exact actual source only.");
                    if (mutation == "mana") { Check(payload["mana"]!.GetValue<int>() == mana, "Persist the true mana."); payload["mana"] = mana + 1; }
                    else if (mutation == "power") { Check(payload["power"]!.GetValue<int>() == power, "Persist the true power."); payload["power"] = power - 1; payload["powerByTrait"]![RuneTrait.Yellow] = power - 1; }
                    else if (mutation == "entry-exhaustion") { Check(payload["isExhausted"]!.GetValue<bool>() == !ready, "Persist the true entry status."); payload["isExhausted"] = ready; }
                    else payload["hasteReadyOptionalCostPaid"] = !ready;
                    try
                    {
                        await Replace(node.ToJsonString());
                        var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing mutated frame.");
                        Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == hash && JsonSerializer.Serialize(bad.Commands, json) == commands,
                            "Only the actual event changes; original commands and state remain untouched.");
                        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, bad.Commands, state, engine, default, bad.Events);
                        Check(!replay.IsMatch && replay.Errors.Any(e => e.Contains("payload hash", StringComparison.Ordinal)), "Original event replay must reject tampering.");
                        var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                        try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged cost/entry accepted."); }
                        catch (MatchSessionException error) { Check(error.Code == ErrorCodes.RecoveryInconsistent, error.Message); }
                        Check(await registry.FindAsync(room, default) is null, "Never cache the rejected frame.");
                        checks.Add(new { mutation, eventSequence = sequence, originalReplayRejected = true, productionRegistryRejected = true });
                    }
                    finally { await Replace(saved); }
                    var restored = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing restored frame.");
                    Check(JsonSerializer.Serialize(restored.Commands, json) == commands && JsonSerializer.Serialize(restored.Events, json) == events
                        && MatchStateHasher.Hash(restored.AuthoritativeState!) == hash, "Restore the exact original history after every negative test.");
                    async Task Replace(string payloadText)
                    {
                        await using var write = new NpgsqlCommand("update game_events set payload=@payload::jsonb where match_id=@room and event_sequence=@sequence", connection);
                        write.Parameters.AddWithValue("payload", payloadText); write.Parameters.AddWithValue("room", room); write.Parameters.AddWithValue("sequence", sequence);
                        Check(await write.ExecuteNonQueryAsync() == 1, "Update one actual event in this dedicated scenario.");
                    }
                }
                var final = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing final frame.");
                Check(final.IsConsistent && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default)).Count == 0,
                    "Full original action/event/state replay must pass after exact restoration.");
                await File.WriteAllTextAsync(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_POSTGRES_OPTIONAL_SACRIFICE_PRINTED_COST_COLD_RESTORE_AND_EVENT_REJECTION_PASS",
                    room, cardNo = card, sourceObjectId = source, sacrificeObjectId = victim,
                    expectedReady = ready, expectedMana = mana, expectedPower = power, quoteMana = quote.Cost!.Mana,
                    originalOpeningTick = initial.Tick, originalOpeningSeed = initial.Seed, originalOpeningCards = initial.CardObjects.Count,
                    prefixCommands = prefix.Commands.Count, prefixEvents = prefix.Events.Count,
                    finalCommands = final.Commands.Count, finalEvents = final.Events.Count, checks,
                    finalHash = hash, ruleInputs = state.RuleInputs, originalHistoryRestoredExactly = true,
                    fullOriginalActionEventStateReplayMatched = true,
                    fixtureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fixturePath))).ToLowerInvariant(),
                    scope = "Actual UNL170 with SFD029 sacrificed from legal empty openings, hand7/3 and outside-hand Haste8/4. Attack destruction, other consumers, visible product and Windows remain separate."
                }, json));
                Console.WriteLine($"POSTGRES_OPTIONAL_SACRIFICE_PASS: {name}, {final.Commands.Count}commands/{final.Events.Count}events,4 actual event rejections");
            }
            finally
            {
                await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
                cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
            }
        }
    }
    private static void Check(bool value, string? reason) { if (!value) throw new InvalidOperationException(reason); }
}
