using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class GrantedOriginHasteSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        await Branch("unit", "SFD·010/221", 2);
        await Branch("sacrifice", "OGN·208/298", 5);
        async Task Branch(string name, string cardNo, int mana)
        {
            var directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "granted-origin-haste-" + name + ".json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
            var fixture = document.RootElement; var room = fixture.GetProperty("sourceRoomId").GetString()!;
            var id = fixture.GetProperty("expectedSourceObjectId").GetString()!;
            Check(fixture.GetProperty("expectedCardNo").GetString() == cardNo
                && fixture.GetProperty("expectedMana").GetInt32() == mana
                && fixture.GetProperty("expectedPower").GetInt32() == 1, "Independent printing/cost expectation required.");
            await using var connection = await source.OpenConnectionAsync();
            await using (var exists = new NpgsqlCommand("select exists(select 1 from matches where match_id=@room)", connection))
            { exists.Parameters.AddWithValue("room", room); Check(!(bool)(await exists.ExecuteScalarAsync())!, "Fresh dedicated SQL fixture required."); }
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.Status == "SEATING" && initial.CardObjects.Count == 0,
                "Use the actual new empty opening, never import a snapshot or migrate an old match.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var checks = new List<object>();
            try
            {
                foreach (var actor in initial.Seats.Keys)
                    await players.SavePlayerSessionAsync(room, actor, initial.Seats[actor], ReconnectTokenHasher.Hash("cost-sql-" + actor), default);
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
                    && prefix.ReplayInitialState is { Tick: 0, Seed: 0 } start && start.CardObjects.Count == 0,
                    "Full ordinary original commands must be retained.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                var state = prefix.AuthoritativeState!;
                Check(RuleInputIdentity.IsCurrent(state.RuleInputs) && state.CardObjects[id].CardNo == cardNo
                    && state.ObjectLocations[id].Zone == "STACK" && !state.PlayerZones["P1"].Hand.Contains(id), "Real nonhand pending source required.");
                var grant = fixture.GetProperty("expectedGrantObjectId").GetString()!;
                Check(state.CardObjects[grant].CardNo == fixture.GetProperty("expectedGrantCardNo").GetString()
                    && state.PlayerZones["P1"].Battlefields.Contains(grant), "Actual friendly in-play grant required before payment.");
                var costs = name == "sacrifice" ? new[] { "DESTROY_FRIENDLY_UNIT:" + grant, HasteOptionalCostNames.HasteReady }
                    : [HasteOptionalCostNames.HasteReady];
                var play = new PlayCardCommand(id, cardNo, [], OptionalCosts: costs);
                var beforeMana = state.RunePools["P1"].Mana; var beforePower = state.RunePools["P1"].TotalPower;
                var quote = engine.PreviewPlayCard(state, "P1", new("sql-quote", session.PromptFor("P1").PromptId!, state.Tick, play));
                Check(quote.CanPay && quote.Cost!.Mana == mana, "Recovered quote must use the independent expected reduction.");
                var accepted = await session.SubmitAsync("P1", "granted-haste-sql-confirm", play, JsonSerializer.SerializeToElement(play, json), default);
                Check(accepted.Accepted, accepted.ErrorMessage); state = accepted.State;
                Check(state.RunePools["P1"].Mana == beforeMana - mana
                    && Equals(accepted.Events.Single(e => e.Kind == "COST_PAID").Payload["mana"], mana), "Actual SQL confirmation must pay the independently expected granted-Haste mana.");
                Check(state.RunePools["P1"].TotalPower == beforePower - 1
                    && state.PlayerZones["P1"].Base.Contains(id) && !state.CardObjects[id].IsExhausted
                    && state.PlayerZones["P1"].Graveyard.Contains(grant) == (name == "sacrifice"),
                    "Actual payment must enter ready even after destroying its grant.");
                var paid = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Paid SQL frame missing.");
                var originalCommands = JsonSerializer.Serialize(paid.Commands, json);
                var originalEvents = JsonSerializer.Serialize(paid.Events, json);
                var paidStateHash = MatchStateHasher.Hash(state);
                await using var read = new NpgsqlCommand("select event_sequence,payload::text from game_events where match_id=@room and event_type='UNIT_PLAYED_TO_BASE' order by event_sequence desc limit 1", connection);
                read.Parameters.AddWithValue("room", room); long sequence; string saved;
                await using (var reader = await read.ExecuteReaderAsync())
                { Check(await reader.ReadAsync(), "Actual persisted cost event required."); sequence = reader.GetInt64(0); saved = reader.GetString(1); }
                Check(JsonNode.Parse(saved)!["payload"]!["hasteReadyOptionalCostPaid"]!.GetValue<bool>()
                    && JsonNode.Parse(saved)!["payload"]!["sourceObjectId"]!.GetValue<string>() == id,
                    "Mutate the real ready-entry event for this played source only.");
                foreach (var mutation in new[] { "flipped-paid-haste", "missing-paid-haste" })
                {
                    var node = JsonNode.Parse(saved)!;
                    if (mutation == "flipped-paid-haste") node["payload"]!["hasteReadyOptionalCostPaid"] = false;
                    else Check(node["payload"]!.AsObject().Remove("hasteReadyOptionalCostPaid"), "Remove an actual field.");
                    try
                    {
                        await Replace(node.ToJsonString());
                        var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Forged SQL frame missing.");
                        Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == paidStateHash
                            && JsonSerializer.Serialize(bad.Commands, json) == originalCommands, "Event tamper must not alter state or commands.");
                        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, bad.Commands, state, engine, default, bad.Events);
                        Check(!replay.IsMatch && replay.Errors.Any(e => e.Contains("payload hash", StringComparison.Ordinal)), "Independent original event replay must reject false cost.");
                        var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                        try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged cost accepted."); }
                        catch (MatchSessionException error) { Check(error.Code == ErrorCodes.RecoveryInconsistent, error.Message); }
                        Check(await registry.FindAsync(room, default) is null, "Rejected state cannot enter live cache.");
                        checks.Add(new { mutation, eventSequence = sequence, originalReplayRejected = true, productionRegistryRejected = true });
                    }
                    finally { await Replace(saved); }
                    var restored = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Restored frame missing.");
                    Check(JsonSerializer.Serialize(restored.Commands, json) == originalCommands
                        && JsonSerializer.Serialize(restored.Events, json) == originalEvents
                        && MatchStateHasher.Hash(restored.AuthoritativeState!) == paidStateHash, "Restore exact original history and state after every negative test.");
                }
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                for (var pass = 0; state.StackItems.Count > 0 && pass < 12; pass++)
                {
                    var command = new PassPriorityCommand(); var result = await session.SubmitAsync(state.PriorityPlayerId!,
                        "granted-haste-sql-resolve-" + pass, command, JsonSerializer.SerializeToElement(command, json), default);
                    Check(result.Accepted, result.ErrorMessage); state = result.State;
                }
                Check(state.PlayerZones["P1"].Base.Contains(id) && !state.CardObjects[id].IsExhausted
                    && state.PlayerZones["P1"].Graveyard.Contains(grant) == (name == "sacrifice"),
                    "Actual cold continuation must retain the ready entry and actual grant departure.");
                var final = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Final SQL frame missing.");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default);
                Check(final.IsConsistent && errors.Count == 0, string.Join("; ", errors));
                await File.WriteAllTextAsync(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_POSTGRES_GRANTED_HASTE_ORIGINAL_OPENING_COLD_ENTRY_AND_EVENT_REJECTION_PASS",
                    room, cardNo, grant, grantCardNo = fixture.GetProperty("expectedGrantCardNo").GetString(),
                    expectedMana = mana, quoteMana = quote.Cost!.Mana, expectedPower = 1, beforeMana, beforePower, sacrificeGrant = name == "sacrifice",
                    originalOpeningTick = initial.Tick, originalOpeningSeed = initial.Seed, originalOpeningCards = initial.CardObjects.Count,
                    prefixCommands = prefix.Commands.Count, prefixEvents = prefix.Events.Count,
                    finalCommands = final.Commands.Count, finalEvents = final.Events.Count, checks,
                    finalHash = MatchStateHasher.Hash(state), ruleInputs = state.RuleInputs,
                    fixtureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fixturePath))).ToLowerInvariant(),
                    originalHistoryRestoredExactly = true, fullOriginalActionEventStateReplayMatched = true,
                    scope = "Actual CN Reksai SFD029 grant with VoidDrone and Aristocrat from a new empty SQL opening. Delayed paid entry persists after the Aristocrat sacrifice. Other printings/permissions, exact Atakhan, tokens, visible product and Windows remain separate."
                }, json));
                Console.WriteLine($"POSTGRES_GRANTED_ORIGIN_HASTE_PASS: {cardNo}, mana{mana}, {final.Commands.Count}commands/{final.Events.Count}events,2 ready-entry-event rejections");
                async Task Replace(string payload)
                {
                    await using var write = new NpgsqlCommand("update game_events set payload=@saved::jsonb where match_id=@room and event_sequence=@sequence", connection);
                    write.Parameters.AddWithValue("saved", payload); write.Parameters.AddWithValue("room", room); write.Parameters.AddWithValue("sequence", sequence);
                    Check(await write.ExecuteNonQueryAsync() == 1, "Mutate only one event in this fresh disposable SQL scenario.");
                }
            }
            finally
            { await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync(); }
        }
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
