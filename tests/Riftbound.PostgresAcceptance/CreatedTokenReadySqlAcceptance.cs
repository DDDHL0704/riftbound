using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class CreatedTokenReadySqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource data, string output)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        foreach (var name in new[] { "protect-pay", "protect-decline", "rise-0", "rise-2" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "embedded-ready-" + name + ".json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path)); var fixture = document.RootElement;
            var room = fixture.GetProperty("sourceRoomId").GetString()!;
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>(json)!);
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0 && initial.Status == MatchStatuses.Seating, "Actual empty opening required.");
            var source = fixture.GetProperty("expectedSourceObjectId").GetString()!;
            var card = fixture.GetProperty("expectedCardNo").GetString()!; var protect = card == "SFD·154/221";
            Check(protect || card == "SFD·198/221", "Only the two exact current consumers belong to this proof.");
            var count = protect ? 1 : fixture.GetProperty("expectedEquipment").GetInt32();
            var expectedTokens = Enumerable.Range(1, count).Select(i => source + "-TOKEN-" + i.ToString("D3")).ToArray();
            Check(expectedTokens.SequenceEqual(fixture.GetProperty("expectedTokenObjectIds").EnumerateArray().Select(x => x.GetString())), "Independently derive the expected token identities.");
            var pay = fixture.GetProperty("expectedPayYellow").GetBoolean();
            var expectedReady = protect ? pay ? expectedTokens : [] : expectedTokens.Take(2).ToArray();
            Check(expectedReady.SequenceEqual(fixture.GetProperty("expectedReadyObjectIds").EnumerateArray().Select(x => x.GetString())), "Independent ready recipients required.");
            await using var connection = await data.OpenConnectionAsync();
            await using (var exists = new NpgsqlCommand("select count(*) from matches where match_id=@room", connection))
            { exists.Parameters.AddWithValue("room", room); Check((long)(await exists.ExecuteScalarAsync())! == 0, "Never overwrite a previous match."); }
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(data); var store = new PostgresMatchRecoveryStore(data);
            var players = new PostgresMatchPlayerStore(data); IMatchSession session = new MatchSession(initial, engine, journal, players);
            var checks = new List<object>(); var state = initial; var next = 0;
            try
            {
                foreach (var actor in initial.Seats.Keys)
                    await players.SavePlayerSessionAsync(room, actor, initial.Seats[actor], ReconnectTokenHasher.Hash("created-ready-sql-" + actor), default);
                foreach (var row in fixture.GetProperty("acceptedCommands").EnumerateArray())
                {
                    var raw = row.GetProperty("rawCommand"); var kind = row.GetProperty("commandType").GetString();
                    Check(kind != "SURRENDER" && !kind!.StartsWith("DEV_", StringComparison.Ordinal), "No developer shortcut or surrender.");
                    var actor = row.GetProperty("playerId").GetString()!; var id = row.GetProperty("clientIntentId").GetString()!;
                    var result = kind switch {
                        "SUBMIT_DECK" => await session.SubmitDeckAsync(actor, id, raw.Deserialize<SubmitDeckCommand>(json)!, raw, default),
                        "READY" => await session.ReadyAsync(actor, id, raw, default),
                        _ => await session.SubmitAsync(actor, id, GameCommandJsonMapper.Map(raw), raw, default)
                    };
                    Check(result.Accepted, result.ErrorMessage); state = result.State;
                }
                var prefix = await Cold("unconfirmed-created-token-trigger");
                Check(prefix.Commands.Count == fixture.GetProperty("acceptedCommands").GetArrayLength(), "Original ordinary command prefix retained.");
                var captured = state.StackItems.Single(i => i.CreatedTokenReady is not null).CreatedTokenReady!;
                Check(captured.CardNo == card && captured.Source.ObjectId == source && captured.Units.Select(u => u.ObjectId).SequenceEqual(expectedTokens), "Captured source and newly created tokens match independent expectations.");
                Check(state.PlayerZones["P1"].Graveyard.Contains(source) && expectedTokens.All(id => state.CardObjects[id].IsExhausted), "Parent completed and tokens entered before this confirmation.");
                var mana = state.RunePools["P1"].Mana; var yellow = state.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow);
                if (protect)
                {
                    var p = state.PendingPayment!;
                    Check(p.ManaCost == 0 && p.PowerCost == 1 && p.PowerCostByTrait.Count == 1 && p.PowerCostByTrait[RuneTrait.Yellow] == 1 && yellow >= 1,
                        "Independent affordable post-entry Yellow fee, without additional mana.");
                    await Act("P1", new PayCostCommand(p.PaymentId, p.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
                    Check(state.RunePools["P1"].Mana == mana && state.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow) == yellow - (pay ? 1 : 0)
                        && expectedTokens.All(id => state.CardObjects[id].IsExhausted), "Cost confirmation does not immediately ready the token.");
                }
                else if (count > 0)
                {
                    var c = state.PendingCardChoice!;
                    Check(c.RequiredCount == 0 && c.MaxCount == 2 && c.LegalObjectIds.SequenceEqual(expectedTokens), "Only these newly created tokens can be selected.");
                    await Act("P1", new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, expectedReady));
                    Check(expectedTokens.All(id => state.CardObjects[id].IsExhausted), "Target confirmation precedes responses and readiness.");
                }
                else Check(state.PendingCardChoice is null && state.StackItems.Any(i => i.CreatedTokenReady is not null) && state.PriorityPlayerId is not null,
                    "Zero equipment still creates the mandatory zero-target embedded ability.");
                await Cold("confirmed-before-responses");
                for (var step = 0; step < 64 && (state.StackItems.Count > 0 || state.TriggerQueue.Count > 0); step++)
                {
                    if (state.PendingCardChoice is { } c)
                    { Check(c.RequiredCount == 0 && c.EffectKind != "CREATED_TOKEN_READY", "Only unrelated optional setup choices remain."); await Act(c.PlayerId, new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, [])); }
                    else if (state.PendingPayment is { } p) await Act(p.PlayerId, new PayCostCommand(p.PaymentId, p.PaymentWindow, ["DECLINE"]));
                    else if (ResolutionResult.BuildPrompts(state).FirstOrDefault(p => p.Value.Actions.Contains(CommandTypes.OrderTriggers)).Key is { } actor)
                        await Act(actor, new OrderTriggersCommand(state.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                    else await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                }
                Check(state.StackItems.Count == 0 && state.TriggerQueue.Count == 0 && expectedTokens.All(id => state.CardObjects[id].IsExhausted == !expectedReady.Contains(id)), "Only the selected bound tokens ready after responses.");
                var final = await Cold("resolved-created-token-trigger");
                var originalHash = MatchStateHasher.Hash(state); var originalCommands = JsonSerializer.Serialize(final.Commands, json); var originalEvents = JsonSerializer.Serialize(final.Events, json);
                foreach (var mutation in new[] { "captured-source-generation", "captured-token-identity" })
                {
                    await using var read = new NpgsqlCommand("select event_sequence,payload::text from game_events where match_id=@room and event_type='TOKEN_READY_TRIGGER_CREATED' order by event_sequence desc limit 1", connection);
                    read.Parameters.AddWithValue("room", room); long sequence; string saved;
                    await using (var reader = await read.ExecuteReaderAsync()) { Check(await reader.ReadAsync(), "Actual captured trigger event required."); sequence = reader.GetInt64(0); saved = reader.GetString(1); }
                    var node = JsonNode.Parse(saved)!; var context = node["payload"]!["createdTokenReady"]!;
                    if (mutation == "captured-source-generation") context["source"]!["generation"] = 999999;
                    else context["units"] = new JsonArray(new JsonObject { ["objectId"] = source + "-TOKEN-999", ["generation"] = 0 });
                    async Task Replace(string value)
                    {
                        await using var update = new NpgsqlCommand("update game_events set payload=@payload::jsonb where match_id=@room and event_sequence=@seq", connection);
                        update.Parameters.AddWithValue("payload", value); update.Parameters.AddWithValue("room", room); update.Parameters.AddWithValue("seq", sequence); Check(await update.ExecuteNonQueryAsync() == 1, "Mutate exactly one event.");
                    }
                    try
                    {
                        await Replace(node.ToJsonString()); var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing mutated SQL frame.");
                        Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == originalHash && JsonSerializer.Serialize(bad.Commands, json) == originalCommands, "Only the exact captured event changed.");
                        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, bad.Commands, state, engine, default, bad.Events);
                        Check(!replay.IsMatch && replay.Errors.Any(e => e.Contains("payload hash", StringComparison.Ordinal)), "Original event replay independently rejects the forged capture.");
                        var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                        try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged embedded-ready event accepted."); }
                        catch (MatchSessionException error) { Check(error.Code == ErrorCodes.RecoveryInconsistent, error.Message); }
                        Check(await registry.FindAsync(room, default) is null, "Never cache rejected history.");
                        checks.Add(new { mutation, eventSequence = sequence, originalReplayRejected = true, productionRegistryRejected = true });
                    }
                    finally { await Replace(saved); }
                }
                var restored = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing restored SQL frame.");
                Check(MatchStateHasher.Hash(restored.AuthoritativeState!) == originalHash && JsonSerializer.Serialize(restored.Commands, json) == originalCommands
                    && JsonSerializer.Serialize(restored.Events, json) == originalEvents, "Original history restored exactly after independent mutations.");
                var dir = Path.Combine(output, name); Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, "acceptance.json"), JsonSerializer.Serialize(new { status = "PASS", name,
                    commands = restored.Commands.Count, events = restored.Events.Count, originalOpeningTick = initial.Tick, originalOpeningCards = initial.CardObjects.Count,
                    prefixCommands = prefix.Commands.Count, coldRestores = 3, expectedTokens, expectedReady, checks, finalHash = originalHash,
                    ruleInputs = state.RuleInputs, fullOriginalActionEventStateReplayMatched = true, originalHistoryRestoredExactly = true,
                    fixtureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))).ToLowerInvariant(),
                    scope = "Current created-token follow-up only; whole cards, legacy consumers, visible paired product and Windows remain OPEN." }, json));
                Console.WriteLine($"POSTGRES_CREATED_TOKEN_READY_PASS: {name}, {restored.Commands.Count}commands/{restored.Events.Count}events,2 actual captured-event rejections");

                async Task Act(string actor, GameCommand command)
                { var result = await session.SubmitAsync(actor, "created-ready-sql-" + next++, command, JsonSerializer.SerializeToElement(command, command.GetType(), json), default); Check(result.Accepted, result.ErrorMessage); state = result.State; }
                async Task<MatchRecoveryFrame> Cold(string label)
                {
                    var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing durable " + label);
                    Check(frame.IsConsistent && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(state), "Exact actual SQL state at " + label);
                    Check(frame.ReplayInitialState is { Tick: 0, Seed: 0 } opening && opening.CardObjects.Count == 0, "Original SEATING opening retained.");
                    Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Full original action/event/state replay at " + label);
                    session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default); return frame;
                }
            }
            finally
            { await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync(); }
        }
    }
    private static void Check(bool value, string? reason) { if (!value) throw new InvalidOperationException(reason); }
}
