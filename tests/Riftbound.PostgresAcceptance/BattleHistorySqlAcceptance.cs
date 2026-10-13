using System.Text.Json;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class BattleHistorySqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        foreach (var (scenario, attackerEmpty, defenderEmpty) in new[] {
            ("battle-history-deck", false, false), ("battle-history-burnout", false, true),
            ("battle-history-attacker-deck", true, false), ("battle-history-attacker-burnout", true, true) })
        {
            var room = "postgres-battle-history-" + Guid.NewGuid().ToString("N");
            var dir = Path.Combine(output, scenario); Directory.CreateDirectory(dir);
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var playerStore = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, playerStore);
            var current = initial; var checkpoints = new List<object>(); var submitted = 0; long sequence = 0;
            foreach (var actor in initial.Seats.Keys)
                await playerStore.SavePlayerSessionAsync(room, actor, actor, ReconnectTokenHasher.Hash("audit-" + actor), default);
            try
            {
                var rawSetup = JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario });
                var setup = await session.SeedScenarioAsync("P1", "setup", scenario, rawSetup, default);
                await PersistedCheckpoint("setup", setup);
                await Submit("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
                var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Actual distributed attack must confirm a target.");
                await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY1"]));
                await Submit("P1", new PlayCardCommand("P1-SOUL", "SFD·163/221", ["HOST", "P1-ALLY"]));
                var removed = await Top(); Check(attackerEmpty == removed.Events.Any(e => e.Kind == "BURNOUT_APPLIED"), "Attacker draw/burnout mismatch.");
                if (current.PriorityPlayerId != "P2") await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                await Submit("P2", new PlayCardCommand("P2-SOUL", "SFD·163/221", ["ENEMY1", "P2-ALLY"]));
                removed = await Top(); Check(defenderEmpty == removed.Events.Any(e => e.Kind == "BURNOUT_APPLIED"), "Defender draw/burnout mismatch.");
                await Top();
                var closed = await Submit(current.FocusPlayerId!, new PassFocusCommand());
                if (!closed.Events.Any(e => e.Kind == "BATTLE_CLOSED")) closed = await Submit(current.FocusPlayerId!, new PassFocusCommand());
                Check(current.BattleResolutions is [{ Kind: "NO_RESULT", WinnerPlayerId: null }], "Both departed sides must close without combat damage.");
                var history = current.BattleResolutions[0];
                Check(history.PublicObjectBindings is { Count: 2 } && history.PublicObjectBindings.All(b => b.Generation == 0)
                    && history.PublicObjectBindings.Select(b => b.ObjectId).Order(StringComparer.Ordinal).SequenceEqual(new[] { "ENEMY1", "HOST" }), "SQL history needs actual role-generation bindings.");
                var durable = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing SQL frame.");
                Check(durable.Commands[0].CommandType == "DEV_SEED_SCENARIO:" + scenario, "Setup must be an actual retained command from seats.");
                var tamper = await RejectSqlHistoryTamper();
                await ColdRecover("restored-after-history-tamper");
                await using var connection = await source.OpenConnectionAsync();
                await using var query = new NpgsqlCommand("select (select count(*) from command_log where match_id=@room),(select count(*) from game_events where match_id=@room),(select count(*) from state_snapshots where match_id=@room),(select count(*) from snapshots where match_id=@room),(select count(*) from action_prompts where match_id=@room),version()", connection);
                query.Parameters.AddWithValue("room", room);
                await using var reader = await query.ExecuteReaderAsync(); Check(await reader.ReadAsync(), "Actual SQL rows required.");
                var counts = new { commands = reader.GetInt64(0), events = reader.GetInt64(1), states = reader.GetInt64(2), snapshots = reader.GetInt64(3), prompts = reader.GetInt64(4) };
                Check(counts.commands == submitted && counts.events == sequence && counts.states == submitted && counts.snapshots == 2 * submitted && counts.prompts == 2 * submitted,
                    "SQL retained rows disagree with actual unique commands/events/frames: " + JsonSerializer.Serialize(counts));
                var receipt = new { status = "REAL_POSTGRES_BATTLE_HISTORY_COLD_REPLAY_TAMPER_PASS", executedAtUtc = DateTimeOffset.UtcNow,
                    room, scenario, attackerEmpty, defenderEmpty, databaseVersion = reader.GetString(5), submitted, counts, checkpoints, tamper,
                    finalHash = MatchStateHasher.Hash(current), scope = "Logged QA setup from persisted seats plus actual core destruction/burnout and battle close; full official score gameplay/concurrency/failover/visible clients remain open." };
                await File.WriteAllTextAsync(Path.Combine(dir, "result.json"), JsonSerializer.Serialize(receipt, Json));
                Console.WriteLine($"POSTGRES_BATTLE_HISTORY_PASS: {scenario}, {submitted} real commands, {checkpoints.Count} cold checkpoints");
            }
            finally
            {
                await using var connection = await source.OpenConnectionAsync();
                await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection);
                cleanup.Parameters.AddWithValue("room", room); await cleanup.ExecuteNonQueryAsync();
            }

            async Task PersistedCheckpoint(string label, ResolutionResult result)
            {
                Check(result.Accepted, result.ErrorMessage); current = result.State; submitted++; sequence += result.Events.Count;
                await ColdRecover(label);
            }
            async Task ColdRecover(string label)
            {
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Actual SQL frame missing.");
                Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
                Check(frame.Commands.Count == submitted && frame.LastEventSequence == sequence, "Durable command/event counts mismatch.");
                Check(MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "Cold state/hash differs from live engine.");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default); Check(errors.Count == 0, string.Join("; ", errors));
                session = await new InMemoryMatchSessionRegistry(engine, journal, store).GetOrCreateAsync(room, default);
                foreach (var actor in initial.Seats.Keys)
                    Check(session.SnapshotFor(actor).Tick == current.Tick && session.PromptFor(actor).SnapshotTick == current.Tick, "Player cold view tick mismatch.");
                checkpoints.Add(new { label, tick = current.Tick, commands = submitted, events = sequence, hash = MatchStateHasher.Hash(current), replayMatched = true });
            }
            async Task<ResolutionResult> Submit(string actor, GameCommand command)
            {
                var prompt = session.PromptFor(actor); var intent = "step-" + submitted;
                var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(command, command.GetType(), Json), Json)!;
                fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId); fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
                var raw = JsonSerializer.SerializeToElement(fields);
                var result = await session.SubmitAsync(actor, intent, command, raw, default);
                await PersistedCheckpoint(intent, result);
                var duplicate = await session.SubmitAsync(actor, intent, command, raw, default);
                Check(duplicate.Accepted && (await store.LoadAsync(room, default))!.Commands.Count == submitted, "Duplicate after cold SQL recovery inserted a command.");
                return result;
            }
            async Task<ResolutionResult> Top()
            {
                await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
                return await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
            }
            async Task<object> RejectSqlHistoryTamper()
            {
                await using var connection = await source.OpenConnectionAsync();
                await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
                read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
                try
                {
                    await using var forge = new NpgsqlCommand("update state_snapshots set payload=jsonb_set(payload,'{battleResolutions,0,publicObjectBindings,0,generation}','999999'::jsonb) where match_id=@room and state_tick=@tick", connection);
                    forge.Parameters.AddWithValue("room", room); forge.Parameters.AddWithValue("tick", current.Tick); await forge.ExecuteNonQueryAsync();
                    var altered = await store.LoadAsync(room, default);
                    Check(altered is not null && !altered.IsConsistent && altered.ValidationErrors.Any(e => e.Contains("captured public battle history reference")), "SQL-forged battle generation must fail.");
                    return new { mutation = "public-battle-history-generation", rejected = true, errors = altered!.ValidationErrors };
                }
                finally
                {
                    await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=@tick", connection);
                    restore.Parameters.AddWithValue("payload", saved); restore.Parameters.AddWithValue("room", room); restore.Parameters.AddWithValue("tick", current.Tick);
                    await restore.ExecuteNonQueryAsync();
                }
            }
        }
    }

    private static void Check(bool valid, string? reason) { if (!valid) throw new InvalidOperationException(reason ?? "Battle SQL invariant failed."); }
}
