using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class CardPlayHistoryCausalSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "card-play-history-current-opening-20261017.json")));
        var room = fixture.RootElement.GetProperty("sourceRoomId").GetString()!;
        // This separately accepted current fixture preserves explicit seed0 and
        // raw prompt identities. The old339c accepted history remains untouched.
        await using var connection = await source.OpenConnectionAsync();
        await using (var exists = new NpgsqlCommand("select exists(select 1 from matches where match_id=@room)", connection))
        {
            exists.Parameters.AddWithValue("room", room);
            Check(!(bool)(await exists.ExecuteScalarAsync())!, "Use a fresh acceptance database; the source match already exists.");
        }
        var initial = MatchReplayInitialStateBuilder.FromSeats(room,
            fixture.RootElement.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        var engine = new CoreRuleEngine();
        var journal = new PostgresMatchJournal(source);
        var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source);
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var checks = new List<object>();
        try
        {
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("causal-" + player), default);
            foreach (var recorded in fixture.RootElement.GetProperty("acceptedCommands").EnumerateArray())
            {
                var player = recorded.GetProperty("playerId").GetString()!;
                var intent = recorded.GetProperty("clientIntentId").GetString()!;
                var raw = recorded.GetProperty("rawCommand");
                var result = recorded.GetProperty("commandType").GetString() switch
                {
                    "SUBMIT_DECK" => await session.SubmitDeckAsync(player, intent, raw.Deserialize<SubmitDeckCommand>(json)!, raw, default),
                    "READY" => await session.ReadyAsync(player, intent, raw, default),
                    "MULLIGAN" => await session.SubmitAsync(player, intent, raw.Deserialize<MulliganCommand>(json)!, raw, default),
                    "TAP_RUNE" => await session.SubmitAsync(player, intent, raw.Deserialize<TapRuneCommand>(json)!, raw, default),
                    "PLAY_CARD" => await session.SubmitAsync(player, intent, raw.Deserialize<PlayCardCommand>(json)!, raw, default),
                    _ => throw new InvalidOperationException("Unexpected actual opening command.")
                };
                Check(result.Accepted, result.ErrorMessage);
            }
            var original = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing real SQL frame.");
            Check(original.IsConsistent && original.Commands.Count == 9, "Actual opening must persist all nine accepted commands.");
            Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(original, engine, default)).Count == 0,
                "Untampered original-opening replay must pass.");
            var state = original.AuthoritativeState!;
            var history = state.CardPlays;
            var entry = history.Resolved.Single();
            Check(entry.PlayerId == "paired-339c-b" && entry.CardNo == "SFD·065/221" && entry.Tick == 8
                && entry.Types == 1 && entry.Source.Generation == 1 && history.Finalized.Single() == entry,
                "Independent actual-play history assertions failed.");
            var logBefore = JsonSerializer.Serialize(new { original.Commands, original.Events }, json);
            await using var read = new NpgsqlCommand(
                "select payload::text from state_snapshots where match_id=@room and state_tick=@tick and last_event_sequence=@seq", connection);
            read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("tick", state.Tick);
            read.Parameters.AddWithValue("seq", original.LastEventSequence);
            var saved = (string)(await read.ExecuteScalarAsync())!;
            var mutations = new Dictionary<string, TurnCardPlayHistory>
            {
                ["valid-finalizer"] = history with { Finalized = [entry with { PlayerId = "paired-339c-a" }] },
                ["valid-resolver"] = history with { Resolved = [entry with { PlayerId = "paired-339c-a" }] },
                ["valid-both-actors"] = history with { Finalized = [entry with { PlayerId = "paired-339c-a" }], Resolved = [entry with { PlayerId = "paired-339c-a" }] },
                ["delete-finalized"] = history with { Finalized = [] },
                ["delete-resolved"] = history with { Resolved = [] },
                ["delete-both"] = history with { Finalized = [], Resolved = [] },
                ["fabricated-second-play"] = history with {
                    Finalized = [entry, entry with { PlayId = "STACK-1-" + entry.Source.ObjectId }],
                    Resolved = [entry, entry with { PlayId = "STACK-1-" + entry.Source.ObjectId }] },
                ["earlier-tick-and-id"] = history with {
                    Finalized = [entry with { Tick = 7, PlayId = "STACK-7-" + entry.Source.ObjectId }],
                    Resolved = [entry with { Tick = 7, PlayId = "STACK-7-" + entry.Source.ObjectId }] }
            };
            foreach (var (name, forgedHistory) in mutations)
            {
                try
                {
                    await using var forge = new NpgsqlCommand(
                        "update state_snapshots set payload=jsonb_set(payload,'{cardPlays}',@history::jsonb) where match_id=@room and state_tick=@tick and last_event_sequence=@seq", connection);
                    forge.Parameters.AddWithValue("history", JsonSerializer.Serialize(forgedHistory, json));
                    forge.Parameters.AddWithValue("room", room); forge.Parameters.AddWithValue("tick", state.Tick);
                    forge.Parameters.AddWithValue("seq", original.LastEventSequence);
                    Check(await forge.ExecuteNonQueryAsync() == 1, "Exactly one dedicated snapshot must be mutated.");
                    var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing forged SQL frame.");
                    Check(bad.IsConsistent, "This bounded forgery must reach the causal guard: " + string.Join("; ", bad.ValidationErrors));
                    Check(logBefore == JsonSerializer.Serialize(new { bad.Commands, bad.Events }, json), "Accepted commands/events must remain unchanged.");
                    var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                    try
                    {
                        await registry.GetOrCreateAsync(room, default);
                        throw new InvalidOperationException("Causal SQL forgery was accepted: " + name);
                    }
                    catch (MatchSessionException error)
                    {
                        Check(error.Code == ErrorCodes.RecoveryInconsistent
                            && error.Message.Contains("action-log audit failed", StringComparison.Ordinal)
                            && error.Message.Contains("replayed final state hash", StringComparison.Ordinal), name + ": " + error.Message);
                    }
                    Check(await registry.FindAsync(room, default) is null, "Rejected forged state must not be cached.");
                    checks.Add(new { name, structuralValidationPassed = true, productionRegistryCausalRejection = true });
                }
                finally
                {
                    await using var restore = new NpgsqlCommand(
                        "update state_snapshots set payload=@saved::jsonb where match_id=@room and state_tick=@tick and last_event_sequence=@seq", connection);
                    restore.Parameters.AddWithValue("saved", saved); restore.Parameters.AddWithValue("room", room);
                    restore.Parameters.AddWithValue("tick", state.Tick); restore.Parameters.AddWithValue("seq", original.LastEventSequence);
                    Check(await restore.ExecuteNonQueryAsync() == 1, "Exact original SQL snapshot must be restored.");
                }
                var clean = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Restored SQL frame missing.");
                Check(clean.IsConsistent && MatchStateHasher.Hash(clean.AuthoritativeState!) == MatchStateHasher.Hash(state)
                    && logBefore == JsonSerializer.Serialize(new { clean.Commands, clean.Events }, json), "Original state/log must survive every mutation.");
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                Check(restored.SnapshotFor(entry.PlayerId).Tick == state.Tick, "Clean SQL recovery must succeed after every rejection.");
            }
            await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_POSTGRES_TYPED_PLAY_HISTORY_CAUSAL_REGISTRY_PASS", room,
                originalCommands = original.Commands.Count, events = original.Events.Count,
                causalTamperRejections = checks.Count, cleanRegistryRecoveries = checks.Count, checks,
                finalHash = MatchStateHasher.Hash(state), ruleInputs = state.RuleInputs,
                scope = "Nine exact actual-opening raw commands executed in a fresh SQL database. Eight snapshot-only causal forgeries pass structural validation but production registry rejects via complete original-opening replay. Coordinated replacement of the accepted command/event history is outside this bounded proof."
            }, json));
            Console.WriteLine($"POSTGRES_TYPED_PLAY_HISTORY_CAUSAL_PASS: {original.Commands.Count} commands, {original.Events.Count} events, {checks.Count} causal rejections/clean recoveries");
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
