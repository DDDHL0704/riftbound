using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class PaymentResourceSurplusSqlAcceptance
{
    public static async Task VerifyNetworkTamperAsync(NpgsqlDataSource source, string output)
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var room = fixture.RootElement.GetProperty("room").GetString()!;
        var actor = fixture.RootElement.GetProperty("actor").GetString()!;
        await using var connection = await source.OpenConnectionAsync();
        await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
        read.Parameters.AddWithValue("room", room);
        var saved = (string)(await read.ExecuteScalarAsync())!;
        var node = JsonNode.Parse(saved)!.AsObject();
        node["runePools"]![actor]!["powerByTrait"]![RuneTrait.Red] = 2;
        string? error = null;
        try
        {
            await Write(node.ToJsonString());
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source),
                new PostgresMatchRecoveryStore(source), new PostgresMatchPlayerStore(source));
            try { await registry.GetOrCreateAsync(room, default); }
            catch (MatchSessionException exception) when (exception.Code == ErrorCodes.RecoveryInconsistent)
            { error = exception.Message; }
            Check(error?.Contains("action-log audit failed", StringComparison.Ordinal) == true,
                "The real recovery registry must reject a shape-valid extra power through its complete original action-log audit.");
        }
        finally { await Write(saved); }
        await File.WriteAllTextAsync(Path.Combine(output, "registry-forged-surplus-result.json"), JsonSerializer.Serialize(new {
            status = "ACTUAL_POSTGRES_RECOVERY_REGISTRY_REJECTED_FORGED_SURPLUS", room, error,
            originalPayloadRestored = true, originalAcceptedHistoryNeverRewritten = true
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        // Also recheck exact seven-table parity and the original replay after the temporary mutation.
        await VerifyNetworkAsync(source, output, "after-restart");

        async Task Write(string payload)
        {
            await using var update = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
            update.Parameters.AddWithValue("payload", payload); update.Parameters.AddWithValue("room", room);
            await update.ExecuteNonQueryAsync();
        }
    }

    // A fresh ordinary eight-command opening supports the real API restart gate.
    // The historical fixture is read, never rewritten or imported as accepted rows.
    public static async Task PrepareNetworkAsync(NpgsqlDataSource source, string fixturePath, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(fixturePath));
        var entries = fixture.RootElement.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        Check(entries.Length == 9 && fixture.RootElement.GetProperty("openingSeed").GetInt64() == 0,
            "The original nine-command seed0 fixture must remain intact.");
        var room = "payment-surplus-api-" + Guid.NewGuid().ToString("N");
        var seats = fixture.RootElement.GetProperty("seats").Deserialize<Dictionary<string, string>>()!;
        var initial = MatchReplayInitialStateBuilder.FromSeats(room, seats);
        var players = new PostgresMatchPlayerStore(source);
        var journal = new PostgresMatchJournal(source);
        var store = new PostgresMatchRecoveryStore(source);
        var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        var tokens = seats.Keys.ToDictionary(actor => actor, actor => "payment-surplus-token-" + actor);
        foreach (var actor in seats.Keys)
            await players.SavePlayerSessionAsync(room, actor, seats[actor], ReconnectTokenHasher.Hash(tokens[actor]), default, initial.Seed);
        var current = initial;
        foreach (var entry in entries.Take(8))
        {
            var actor = entry.GetProperty("playerId").GetString()!;
            var raw = JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject();
            if (raw.ContainsKey("promptId")) raw["promptId"] = session.PromptFor(actor).PromptId;
            if (raw.ContainsKey("snapshotTick")) raw["snapshotTick"] = current.Tick;
            var payload = JsonSerializer.SerializeToElement(raw);
            var action = GameCommandJsonMapper.Map(payload);
            var id = "network-opening-" + entry.GetProperty("clientIntentId").GetString();
            var result = action switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, id, deck, payload, default),
                ReadyCommand => await session.ReadyAsync(actor, id, payload, default),
                _ => await session.SubmitAsync(actor, id, action, payload, default)
            };
            Check(result.Accepted, result.ErrorMessage); current = result.State;
        }
        var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing network opening.");
        Check(frame.IsConsistent && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0,
            "The real API must be able to independently recover the ordinary opening from its complete original log.");
        var command = JsonNode.Parse(entries[^1].GetProperty("rawCommand").GetRawText())!.AsObject();
        command["optionalCosts"] = new JsonArray(entries.Take(8).Where(e => e.GetProperty("rawCommand").GetProperty("cmdType").GetString() == CommandTypes.TapRune)
            .Reverse().Select(e => JsonValue.Create("RECYCLE_RUNE:" + e.GetProperty("rawCommand").GetProperty("sourceObjectId").GetString())).ToArray());
        await File.WriteAllTextAsync(Path.Combine(output, "network-fixture.json"), JsonSerializer.Serialize(new {
            room, tokens, actor = entries[^1].GetProperty("playerId").GetString(), command,
            initialHash = MatchStateHasher.Hash(initial), beforeHash = MatchStateHasher.Hash(current), openingCommands = 8,
            expectedRemainingPowerByTrait = new Dictionary<string, int> { [RuneTrait.Red] = 1, [RuneTrait.Blue] = 1 }
        }, json));
    }

    public static async Task VerifyNetworkAsync(NpgsqlDataSource source, string output, string stage)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "network-fixture.json")));
        var room = fixture.RootElement.GetProperty("room").GetString()!;
        var actor = fixture.RootElement.GetProperty("actor").GetString()!;
        var store = new PostgresMatchRecoveryStore(source);
        var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing API frame.");
        var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default);
        Check(frame.IsConsistent && errors.Count == 0, string.Join("; ", frame.ValidationErrors.Concat(errors)));
        var state = frame.AuthoritativeState!;
        Check(state.RunePools[actor].Mana == 0 && state.RunePools[actor].Power == 0
            && state.RunePools[actor].PowerByTrait.Count == 2
            && state.RunePools[actor].PowerByTrait.GetValueOrDefault(RuneTrait.Red) == 1
            && state.RunePools[actor].PowerByTrait.GetValueOrDefault(RuneTrait.Blue) == 1,
            "The API must preserve both deliberately acquired power while paying exactly two mana.");
        Check(frame.Commands.Count == 10 && frame.Commands.Count(c => c.Accepted) == 9,
            "Keep eight opening actions, one accepted acquisition/play, one stale rejection and no duplicate writes.");
        var rows = new StringBuilder();
        await using var connection = await source.OpenConnectionAsync();
        foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
        {
            await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text), '') from {table} t where match_id=@room", connection);
            query.Parameters.AddWithValue("room", room);
            rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
        }
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
        if (stage == "after-restart") {
            using var previous = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "sql-before-restart.json")));
            Check(previous.RootElement.GetProperty("sevenTableDigest").GetString() == digest,
                "Actual API restart, reconnect and duplicate submission must not change any row in the seven tables.");
        }
        await File.WriteAllTextAsync(Path.Combine(output, "sql-" + stage + ".json"), JsonSerializer.Serialize(new {
            status = "REAL_API_PAYMENT_SURPLUS_SQL_AND_ORIGINAL_REPLAY_PASS", stage, room, commands = frame.Commands.Count,
            events = frame.Events.Count, finalHash = MatchStateHasher.Hash(state), sevenTableDigest = digest,
            completeOriginalReplayMatched = true, expectedSurplusIndependentlyChecked = true, originalRowsRetained = true
        }, json));
        await File.WriteAllTextAsync(Path.Combine(output, "api-original-commands.json"), JsonSerializer.Serialize(frame.Commands, json));
        await File.WriteAllTextAsync(Path.Combine(output, "api-original-events.json"), JsonSerializer.Serialize(frame.Events, json));
    }

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var branches = new List<object>();
        foreach (var existingRedPower in new[] { 0, 1 })
        {
            var room = "postgres-payment-surplus-" + Guid.NewGuid().ToString("N");
            var directory = Path.Combine(output, "existing-red-" + existingRedPower);
            Directory.CreateDirectory(directory);
            var initial = Position(room, existingRedPower);
            var engine = new CoreRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source);
            var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            foreach (var actor in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("surplus-" + actor), default, initial.Seed);
            var current = initial;
            var checks = new List<object>();
            var command = new PlayCardCommand("CARD", "SFD·006/221", [],
                OptionalCosts: ["RECYCLE_RUNE:RUNE-B", "RECYCLE_RUNE:RUNE-A"]);
            var raw = Raw(command);
            var request = new PlayCostPreviewRequestDto("sql-surplus-quote", session.PromptFor("P1").PromptId!, current.Tick, command);
            var quote = await session.PreviewPlayCardAsync("P1", request, default);
            Check(quote.CanPay && quote.Cost?.RemainingPowerByTrait?.GetValueOrDefault(RuneTrait.Red) == existingRedPower + 1,
                "Quote must include both voluntary acquisitions and only one printed power payment.");

            var invalidCommand = command with { OptionalCosts = ["RECYCLE_RUNE:FOREIGN"] };
            var invalid = await session.SubmitAsync("P1", "foreign-resource", invalidCommand, Raw(invalidCommand), default);
            Check(!invalid.Accepted && invalid.Events.Count == 0 && MatchStateHasher.Hash(invalid.State) == MatchStateHasher.Hash(initial),
                "A foreign rune must not be acquired even when the current pool already pays the cost.");
            await Cold("rejected-foreign-resource");

            var result = await session.SubmitAsync("P1", "surplus-play", command, raw, default);
            Check(result.Accepted, result.ErrorMessage);
            current = result.State;
            Check(current.RunePools["P1"].Mana == 0 && current.RunePools["P1"].PowerByTrait[RuneTrait.Red] == existingRedPower + 1,
                "Exactly three mana and one red power must be consumed; surplus stays in the pool.");
            Check(current.PlayerZones["P1"].RuneDeck.SequenceEqual(new[] { "BOTTOM", "RUNE-B", "RUNE-A" }),
                "The player's explicit rune acquisition order must reach the bottom of the rune deck unchanged.");
            var paid = result.Events.Single(e => e.Kind == "COST_PAID");
            Check(((string[])paid.Payload["paymentResourceActions"]!).SequenceEqual(command.OptionalCosts!)
                && (int)paid.Payload["totalPowerCost"]! == 1, "The durable payment audit must retain the selected acquisitions and exact cost.");
            var frame = await Cold("accepted-surplus-and-original-replay");
            var digest = await SqlDigest();
            var duplicate = await session.SubmitAsync("P1", "surplus-play", command, raw, default);
            Check(duplicate.Accepted && MatchStateHasher.Hash(duplicate.State) == MatchStateHasher.Hash(current)
                && await SqlDigest() == digest, "Cold-restored idempotency must not recycle twice or append any SQL row.");
            await Cold("cold-idempotent-duplicate");

            var stale = await session.SubmitAsync("P1", "stale-surplus-play", command, raw, default);
            Check(!stale.Accepted && stale.ErrorCode == ErrorCodes.PromptExpired && stale.Events.Count == 0
                && MatchStateHasher.Hash(stale.State) == MatchStateHasher.Hash(current), "A stale payment plan must not acquire resources again.");
            frame = await Cold("stale-prompt-rejected");
            Check(frame.Commands.Count == 3 && frame.Commands.Count(c => c.Accepted) == 1,
                "The original foreign rejection, one accepted play and one stale rejection must all remain in the log.");
            await RejectForgedSurplus(frame);
            frame = await Cold("restored-original-sql-after-forgery");

            await File.WriteAllTextAsync(Path.Combine(directory, "explicit-initial.json"), JsonSerializer.Serialize(initial, json));
            await File.WriteAllTextAsync(Path.Combine(directory, "commands.json"), JsonSerializer.Serialize(frame.Commands, json));
            await File.WriteAllTextAsync(Path.Combine(directory, "events.json"), JsonSerializer.Serialize(frame.Events, json));
            branches.Add(new { room, existingRedPower, checkpoints = checks, acceptedCommands = 1,
                commands = frame.Commands.Count, events = frame.Events.Count, finalHash = MatchStateHasher.Hash(current),
                finalSqlDigest = await SqlDigest(), originalSqlRowsRetained = true, originalReplayMatched = true,
                coldDuplicateUnchangedAllSevenTables = true, forgedSurplusRejected = true });

            async Task<MatchRecoveryFrame> Cold(string label)
            {
                var before = await SqlDigest();
                var recovered = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
                Check(recovered.IsConsistent && MatchStateHasher.Hash(recovered.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "SQL recovery mismatch: " + string.Join("; ", recovered.ValidationErrors));
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, recovered.Commands,
                    recovered.AuthoritativeState!, engine, default, recovered.Events);
                Check(replay.IsMatch, string.Join("; ", replay.Errors));
                session = MatchSession.Restore(recovered, engine, journal, players);
                foreach (var actor in initial.Seats.Keys)
                {
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(actor), json),
                        JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(current)[actor], json)), "Cold player projection mismatch.");
                    var view = JsonSerializer.Serialize(new { snapshot = session.SnapshotFor(actor), prompt = session.PromptFor(actor) }, json);
                    Check(!view.Contains("BOTTOM", StringComparison.Ordinal), "An unrevealed rune deck identity escaped in a projection or prompt.");
                }
                Check(await SqlDigest() == before, "Recovery must never rewrite an accepted SQL row.");
                checks.Add(new { label, current.Tick, commands = recovered.Commands.Count, events = recovered.Events.Count,
                    originalActionEventReplayMatched = true, bothPlayerProjectionsMatched = true, sqlRowsUnchanged = true });
                return recovered;
            }

            async Task RejectForgedSurplus(MatchRecoveryFrame original)
            {
                await using var connection = await source.OpenConnectionAsync();
                await using var query = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
                query.Parameters.AddWithValue("room", room);
                var saved = (string)(await query.ExecuteScalarAsync())!;
                var node = JsonNode.Parse(saved)!.AsObject();
                node["runePools"]!["P1"]!["powerByTrait"]![RuneTrait.Red] = existingRedPower + 2;
                try
                {
                    await Write(node.ToJsonString());
                    var forged = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing forged frame.");
                    var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, original.Commands,
                        forged.AuthoritativeState!, engine, default, original.Events);
                    await File.WriteAllTextAsync(Path.Combine(directory, "forged-surplus-result.json"), JsonSerializer.Serialize(new {
                        structurallyConsistent = forged.IsConsistent, forged.ValidationErrors, replay.IsMatch, replay.Errors,
                        expectedPower = existingRedPower + 1, actualPower = forged.AuthoritativeState!.RunePools["P1"].PowerByTrait[RuneTrait.Red]
                    }, json));
                    Check(!replay.IsMatch, "Shape-valid extra surplus must fail independent original action replay.");
                }
                finally { await Write(saved); }

                async Task Write(string payload)
                {
                    await using var update = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
                    update.Parameters.AddWithValue("payload", payload); update.Parameters.AddWithValue("room", room);
                    await update.ExecuteNonQueryAsync();
                }
            }

            JsonElement Raw(GameCommand action)
            {
                var node = JsonSerializer.SerializeToNode(action, action.GetType(), json)!.AsObject();
                node["promptId"] = session.PromptFor("P1").PromptId; node["snapshotTick"] = current.Tick;
                return JsonSerializer.SerializeToElement(node);
            }

            async Task<string> SqlDigest()
            {
                var rows = new StringBuilder();
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text), '') from {table} t where match_id=@room", connection);
                    query.Parameters.AddWithValue("room", room);
                    rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
                }
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_PAYMENT_SURPLUS_COLD_ORIGINAL_REPLAY_AND_SECURITY_PASS", executedAtUtc = DateTimeOffset.UtcNow,
            branches, scope = "Explicit current-rule fixture, printed-cost acquisition, surplus, order, quote, hidden rune identities, rejection, cold idempotency, seven SQL tables and independent original replay. No whole-card certification."
        }, json));
        Console.WriteLine("POSTGRES_PAYMENT_SURPLUS_PASS: 2 actual SQL rooms; surplus/order/quote, foreign/stale rejection, cold idempotency, hidden information and forged-surplus rejection.");
        await MightyPaymentResourceSurplusSqlAcceptance.RunAsync(source, Path.Combine(output, "mighty"));
    }

    private static MatchState Position(string room, int redPower)
    {
        var cards = new Dictionary<string, CardObjectState> {
            ["CARD"] = new("CARD", cardNo: "SFD·006/221", power: 3, manaCost: 3,
                tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["FOREIGN"] = new("FOREIGN", cardNo: "UNL-R01", tags: [CardObjectTags.RuneCard, "COLOR:red"], ownerId: "P2", controllerId: "P2") };
        foreach (var id in new[] { "RUNE-A", "RUNE-B", "BOTTOM" })
            cards[id] = new(id, cardNo: "UNL-R01", isExhausted: true,
                tags: [CardObjectTags.RuneCard, "COLOR:red"], ownerId: "P1", controllerId: "P1");
        return new(room, 0, 1, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, seed: 424242,
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Red] = redPower }), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], Base = ["RUNE-A", "RUNE-B"], RuneDeck = ["BOTTOM"] },
                ["P2"] = PlayerZones.Empty with { Base = ["FOREIGN"] } }, cardObjects: cards,
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["CARD"] = new("P1", "HAND"), ["RUNE-A"] = new("P1", "BASE"), ["RUNE-B"] = new("P1", "BASE"),
                ["BOTTOM"] = new("P1", "RUNE_DECK"), ["FOREIGN"] = new("P2", "BASE") });
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
