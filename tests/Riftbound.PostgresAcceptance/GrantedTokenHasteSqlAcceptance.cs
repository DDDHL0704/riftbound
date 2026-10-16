using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class GrantedTokenHasteSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource data, string output)
    {
        foreach (var name in new[] { "hand", "standby" })
        {
            var standby = name == "standby";
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
                WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var directory = Path.Combine(output, name); Directory.CreateDirectory(directory);
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "token-haste-" + name + ".json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(fixturePath));
            var fixture = document.RootElement; var room = fixture.GetProperty("sourceRoomId").GetString()!;
            var source = fixture.GetProperty("expectedSourceObjectId").GetString()!;
            Check(fixture.GetProperty("expectedCardNo").GetString() == "SFD·154/221"
                && fixture.GetProperty("expectedTokenCardNo").GetString() == "SFD·T02"
                && fixture.GetProperty("expectedHasteMana").GetInt32() == 1
                && fixture.GetProperty("expectedHastePower").GetInt32() == 1
                && fixture.GetProperty("expectedReady").GetBoolean(), "Independent token identity and1/1 Haste expectations required.");
            await using var connection = await data.OpenConnectionAsync();
            await using (var exists = new NpgsqlCommand("select exists(select 1 from matches where match_id=@room)", connection))
            { exists.Parameters.AddWithValue("room", room); Check(!(bool)(await exists.ExecuteScalarAsync())!, "Fresh dedicated SQL room required."); }
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, fixture.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial is { Tick: 0, Seed: 0, Status: "SEATING" } && initial.CardObjects.Count == 0, "Real empty opening required.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(data);
            var players = new PostgresMatchPlayerStore(data); var store = new PostgresMatchRecoveryStore(data);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var checks = new List<object>();
            var requiredHidePlacementPromptsChecked = 0;
            try
            {
                foreach (var actor in initial.Seats.Keys)
                    await players.SavePlayerSessionAsync(room, actor, initial.Seats[actor], ReconnectTokenHasher.Hash("token-haste-sql-" + actor), default);
                foreach (var row in fixture.GetProperty("acceptedCommands").EnumerateArray())
                {
                    var actor = row.GetProperty("playerId").GetString()!; var intent = row.GetProperty("clientIntentId").GetString()!;
                    var raw = row.GetProperty("rawCommand");
                    if (row.GetProperty("commandType").GetString() == CommandTypes.HideCard)
                    {
                        var hide = session.PromptFor(actor).Candidates!.Single(c => c.Action == CommandTypes.HideCard);
                        Check(hide.CommandTemplate!.Bindings.Single(b => b.Field == "destination").Required
                            && hide.CommandTemplate.Bindings.Single(b => b.Field == "optionalCosts").Required
                            && hide.SelectionSteps!.Single(s => s.Role == "destination").Required
                            && hide.SelectionSteps!.Single(s => s.Role == "optionalCost").Required,
                            "Actual empty-opening HIDE must require both location and its authorized placement cost.");
                        requiredHidePlacementPromptsChecked++;
                    }
                    var result = row.GetProperty("commandType").GetString() switch {
                        "SUBMIT_DECK" => await session.SubmitDeckAsync(actor, intent, raw.Deserialize<SubmitDeckCommand>(json)!, raw, default),
                        "READY" => await session.ReadyAsync(actor, intent, raw, default),
                        "SURRENDER" => throw new InvalidOperationException("No surrender in this scenario."),
                        var kind when kind!.StartsWith("DEV_", StringComparison.Ordinal) => throw new InvalidOperationException("No developer command."),
                        _ => await session.SubmitAsync(actor, intent, GameCommandJsonMapper.Map(raw), raw, default)
                    };
                    Check(result.Accepted, result.ErrorMessage);
                }
                var prefix = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL prefix.");
                Check(prefix.IsConsistent && prefix.ReplayInitialState is { Tick: 0, Seed: 0 } opening && opening.CardObjects.Count == 0
                    && prefix.Commands.Count == fixture.GetProperty("acceptedCommands").GetArrayLength(), "Every original empty-opening command must be retained.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                var before = prefix.AuthoritativeState!;
                var pending = before.PendingRuleChoice ?? throw new InvalidOperationException("Missing token Haste checkpoint.");
                Check(pending.Request.Kind == "TOKEN_HASTE_PAYMENT" && pending.Request.PlayerId == "P1"
                    && !before.CardObjects.Values.Any(c => c.Tags.Contains(CardObjectTags.SandSoldier)), "Payment occurs before any token entry.");
                var offered = session.PromptFor("P1").Candidates!.Single(c => c.Action == CommandTypes.PayCost);
                var offeredCost = JsonSerializer.SerializeToElement(offered.Metadata).GetProperty("cost");
                Check(offered.Label == "指示物急速支付" && offeredCost.GetProperty("mana").GetInt32() == 1
                    && offeredCost.GetProperty("power").GetInt32() == 1, "Persisted checkpoint projects the true1/1 cost and token-specific action label.");
                var atomic = offered.SelectionSteps!.Single();
                Check(atomic.Required && atomic.Role == "target"
                    && atomic.Choices.Select(c => c.Id).Order().SequenceEqual(pending.Request.Options.Select(o => o.Id).Order()),
                    "Every authoritative atomic decision, including rune recycling, must survive the actual SQL cold-recovered prompt.");
                Check(requiredHidePlacementPromptsChecked == (standby ? 1 : 0), "Retain the original hand/standby opening paths.");
                var payment = pending.Request.Options.First(o => o.Id.StartsWith("PAY:TRAIT:", StringComparison.Ordinal)).Id;
                var trait = payment[10..];
                var command = new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [payment]);
                var accepted = await session.SubmitAsync("P1", "sql-token-haste-confirm", command, JsonSerializer.SerializeToElement(command, json), default);
                Check(accepted.Accepted, accepted.ErrorMessage); var state = accepted.State;
                var token = state.CardObjects.Values.Single(c => c.Tags.Contains(CardObjectTags.SandSoldier));
                Check(token.CardNo == "SFD·T02" && !token.IsExhausted
                    && state.ObjectLocations[token.ObjectId].Zone == (standby ? "BATTLEFIELD" : "BASE")
                    && state.RunePools["P1"].Mana == before.RunePools["P1"].Mana - 1
                    && state.RunePools["P1"].TotalPower == before.RunePools["P1"].TotalPower - 1,
                    "Independent1/1 payment and ready token entry must match actual persisted state.");
                var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing paid frame.");
                var commands = JsonSerializer.Serialize(original.Commands, json); var events = JsonSerializer.Serialize(original.Events, json);
                var hash = MatchStateHasher.Hash(state);
                foreach (var mutation in new[] { "mana", "power", "entry-exhaustion", "selected-haste" })
                {
                    var kind = mutation is "mana" or "power" ? "COST_PAID" : "UNIT_TOKEN_CREATED";
                    await using var read = new NpgsqlCommand("select event_sequence,payload::text from game_events where match_id=@room and event_type=@kind order by event_sequence desc limit 1", connection);
                    read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("kind", kind);
                    long sequence; string saved;
                    await using (var reader = await read.ExecuteReaderAsync())
                    { Check(await reader.ReadAsync(), "Actual persisted token payment or entry event required."); sequence = reader.GetInt64(0); saved = reader.GetString(1); }
                    var node = JsonNode.Parse(saved)!; var payload = node["payload"]!;
                    if (kind == "COST_PAID") Check(payload["sourceObjectId"]!.GetValue<string>() == token.ObjectId
                        && payload["parentSourceObjectId"]!.GetValue<string>() == source, "Cost belongs to this token, independently of the parent spell.");
                    else Check(payload["tokenObjectId"]!.GetValue<string>() == token.ObjectId
                        && payload["sourceObjectId"]!.GetValue<string>() == source, "Mutate the exact newly played token only.");
                    if (mutation == "mana") { Check(payload["mana"]!.GetValue<int>() == 1, "Persist true Haste mana1."); payload["mana"] = 0; }
                    else if (mutation == "power") { Check(payload["power"]!.GetValue<int>() == 1, "Persist true Haste power1."); payload["power"] = 0; payload["powerByTrait"]![trait] = 0; }
                    else if (mutation == "entry-exhaustion") { Check(!payload["isExhausted"]!.GetValue<bool>(), "Persist ready entry."); payload["isExhausted"] = true; }
                    else { Check(payload["hasteReadyOptionalCostPaid"]!.GetValue<bool>(), "Persist selected and paid Haste."); payload["hasteReadyOptionalCostPaid"] = false; }
                    try
                    {
                        await Replace(node.ToJsonString());
                        var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing mutated frame.");
                        Check(MatchStateHasher.Hash(bad.AuthoritativeState!) == hash && JsonSerializer.Serialize(bad.Commands, json) == commands,
                            "Only this event changes; commands and authoritative state remain original.");
                        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, bad.Commands, state, engine, default, bad.Events);
                        Check(!replay.IsMatch && replay.Errors.Any(e => e.Contains("payload hash", StringComparison.Ordinal)), "Original event replay must reject the mutation.");
                        var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                        try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged token Haste accepted."); }
                        catch (MatchSessionException error) { Check(error.Code == ErrorCodes.RecoveryInconsistent, error.Message); }
                        Check(await registry.FindAsync(room, default) is null, "Never cache a rejected frame.");
                        checks.Add(new { mutation, eventSequence = sequence, originalReplayRejected = true, productionRegistryRejected = true });
                    }
                    finally { await Replace(saved); }
                    var restored = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing restored frame.");
                    Check(JsonSerializer.Serialize(restored.Commands, json) == commands && JsonSerializer.Serialize(restored.Events, json) == events
                        && MatchStateHasher.Hash(restored.AuthoritativeState!) == hash, "Restore exact original history after each negative test.");
                    async Task Replace(string payloadText)
                    {
                        await using var write = new NpgsqlCommand("update game_events set payload=@payload::jsonb where match_id=@room and event_sequence=@sequence", connection);
                        write.Parameters.AddWithValue("payload", payloadText); write.Parameters.AddWithValue("room", room); write.Parameters.AddWithValue("sequence", sequence);
                        Check(await write.ExecuteNonQueryAsync() == 1, "Update exactly one real event.");
                    }
                }
                var final = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing final SQL frame.");
                Check(final.IsConsistent && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default)).Count == 0,
                    "Full original action/event/state replay must pass after exact restoration.");
                await File.WriteAllTextAsync(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_POSTGRES_GRANTED_TOKEN_HASTE_COLD_RESTORE_AND_EVENT_REJECTION_PASS",
                    room, parentCardNo = "SFD·154/221", sourceObjectId = source, tokenObjectId = token.ObjectId, tokenCardNo = token.CardNo,
                    expectedMana = 1, expectedPower = 1, expectedReady = true, expectedDestination = standby ? "BATTLEFIELD" : "BASE",
                    originalOpeningTick = initial.Tick, originalOpeningSeed = initial.Seed, originalOpeningCards = initial.CardObjects.Count,
                    prefixCommands = prefix.Commands.Count, prefixEvents = prefix.Events.Count,
                    finalCommands = final.Commands.Count, finalEvents = final.Events.Count, checks,
                    finalHash = hash, ruleInputs = state.RuleInputs, originalHistoryRestoredExactly = true,
                    fullOriginalActionEventStateReplayMatched = true,
                    allAtomicRuleChoicesProjected = true, requiredHidePlacementPromptsChecked,
                    fixtureSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fixturePath))).ToLowerInvariant(),
                    scope = "Only granted pre-entry token Haste from a hand or standby parent. SFD154 printed post-entry Yellow instruction, legacy consumers, visible product and Windows remain OPEN."
                }, json));
                Console.WriteLine($"POSTGRES_TOKEN_HASTE_PASS: {name}, {final.Commands.Count}commands/{final.Events.Count}events,4 actual event rejections");
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
