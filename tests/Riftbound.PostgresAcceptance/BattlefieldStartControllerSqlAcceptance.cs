using System.IO.Compression;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class BattlefieldStartControllerSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh battlefield-start SQL evidence required.");
        Directory.CreateDirectory(output);
        foreach (var branch in new[] { "normal", "actor-and-context", "actor-context-and-event", "no-history", "wrong-inputs" })
            await Run(branch);

        async Task Run(string branch)
        {
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "battlefield-start-controller-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            const string scenario = "battlefield-turn-start-damage";
            var seeded = await session.SeedScenarioAsync("alice", "original-controlled-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(seeded.Accepted, "The existing recorded controlled scenario must be accepted."); await Cold();
            var opened = await Submit("alice", new EndTurnCommand());
            var item = opened.State.StackItems.Single();
            Check(opened.State.TurnPlayerId == "bob" && item.ControllerId == "alice" && item.BattlefieldStart?.ControllerId == "alice",
                "Literal CN190.6: Alice's controlled Frost skill remains Alice's on Bob's turn.");
            Check(item.BattlefieldStart is { CardNo: "UNL-212/219", Damage: 1, DrawCount: 0 }, "Exact original printing and one damage.");
            var original = (await store.LoadAsync(room, default))!;
            await Save("original-active-frame-before-any-tamper.json.gz", original);
            await SaveRows("original-all-seven-tables-before-any-tamper.json.gz");
            Check(journal.Entries.Count == 4 && cold == 4 && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT")
                && original.ReplayInitialState is { Status: "EMPTY", Seats.Count: 0 } && original.OpeningSeed != 0,
                "Two original atomic seats, the declared controlled scenario and one original end turn, from the original empty private-seed room.");

            var rejected = false;
            if (branch == "normal")
            {
                await Submit(item.ControllerId, new PassPriorityCommand());
                var frame = (await store.LoadAsync(room, default))!;
                await Submit(frame.AuthoritativeState!.PriorityPlayerId!, new PassPriorityCommand());
                var final = (await store.LoadAsync(room, default))!;
                Check(final.AuthoritativeState is { Phase: MatchPhases.Main, StackItems.Count: 0 }
                    && final.Events.Count(e => e.Event.Kind == "BATTLEFIELD_TRIGGER_RESOLVED"
                        && JsonSerializer.SerializeToElement(e.Event.Payload, Json).GetProperty("playerId").GetString() == "alice") == 1
                    && final.AuthoritativeState.PlayerZones["alice"].Graveyard.Contains("P1-BATTLEFIELD-FROST-FALLING")
                    && final.AuthoritativeState.CardObjects["P2-BATTLEFIELD-FROST-SURVIVOR"].Damage == 0,
                    "One captured Alice skill damages only units actually at its field, then Bob's turn resumes.");
                Check(final.Commands.Count == 6 && cold == 6, "Both original priority passes persist and cold-recover.");
                await Save("original-final-resolved-frame.json.gz", final);
                await SaveRows("original-all-seven-tables-after-normal-resolution.json.gz");
            }
            else
            {
                await using (var connection = await source.OpenConnectionAsync())
                await using (var transaction = await connection.BeginTransactionAsync())
                {
                    if (branch is "actor-and-context" or "actor-context-and-event")
                    {
                        var forged = original.AuthoritativeState! with { StackItems = [item with {
                            ControllerId = "bob", BattlefieldStart = item.BattlefieldStart! with { ControllerId = "bob" } }] };
                        await WritePayload("state_snapshots", "state_tick", forged.Tick, null, forged);
                        foreach (var view in ResolutionResult.BuildSnapshots(forged))
                            await WritePayload("snapshots", "snapshot_tick", forged.Tick, view.Key, view.Value);
                        foreach (var view in ResolutionResult.BuildPrompts(forged))
                            await WritePayload("action_prompts", "prompt_tick", forged.Tick, view.Key, view.Value);
                        if (branch == "actor-context-and-event")
                            await Execute("update game_events set payload=jsonb_set(payload,'{payload,controllerId}',to_jsonb('bob'::text)) where match_id=@room and event_type='TRIGGER_QUEUED'");
                    }
                    else if (branch == "no-history")
                    {
                        await Execute("delete from command_log where match_id=@room");
                        await Execute("delete from game_events where match_id=@room");
                        await Execute("update matches set last_event_sequence=0,seed=@seed where match_id=@room", seed: original.AuthoritativeState!.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        foreach (var table in new[] { "state_snapshots", "snapshots", "action_prompts" })
                            await Execute($"update {table} set last_event_sequence=0 where match_id=@room");
                    }
                    else
                    {
                        var current = RuleInputIdentity.Current;
                        var wrong = new RuleInputIdentity(current.SchemaVersion, current.Region, current.Language,
                            current.AuthoritySha256, current.CatalogSha256, new string('a', 64));
                        foreach (var table in new[] { "matches", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                            await Execute($"update {table} set rule_inputs=@rules,ruleset_version=@version,faq_version=@faq where match_id=@room", wrong);
                        foreach (var table in new[] { "command_log", "state_snapshots" })
                            await Execute($"update {table} set payload=jsonb_set(payload,'{{ruleInputs}}',@rules) where match_id=@room", wrong);
                    }
                    await transaction.CommitAsync();

                    async Task WritePayload(string table, string tickColumn, long tick, string? actor, object value)
                    {
                        await using var command = new NpgsqlCommand($"update {table} set payload=@payload where match_id=@room and {tickColumn}=@tick"
                            + (actor is null ? "" : " and player_id=@actor"), connection, transaction);
                        command.Parameters.AddWithValue("room", room); command.Parameters.AddWithValue("tick", tick);
                        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(value, value.GetType(), Json));
                        if (actor is not null) command.Parameters.AddWithValue("actor", actor);
                        Check(await command.ExecuteNonQueryAsync() == 1, "Exactly one original current row is altered in this isolated negative room.");
                    }
                    async Task Execute(string sql, RuleInputIdentity? identity = null, string? seed = null)
                    {
                        await using var command = new NpgsqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("room", room);
                        if (identity is not null) {
                            command.Parameters.AddWithValue("rules", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(identity, Json));
                            command.Parameters.AddWithValue("version", identity.IdentitySha256); command.Parameters.AddWithValue("faq", identity.AuthoritySha256);
                        }
                        if (seed is not null) command.Parameters.AddWithValue("seed", seed);
                        Check(await command.ExecuteNonQueryAsync() > 0, "Each deliberate SQL tamper must alter actual rows.");
                    }
                }
                var bad = (await store.LoadAsync(room, default))!;
                await Save("actual-rejected-frame-after-deliberate-tamper.json.gz", bad);
                await SaveRows("actual-all-seven-tables-after-deliberate-tamper.json.gz");
                if (branch is "actor-and-context" or "actor-context-and-event")
                    Check(bad.IsConsistent, "All forged projections must match; journal/replay, not an unrelated projection mismatch, rejects this case.");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(bad, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Original trigger birth, strict original replay, missing history or wrong actual inputs must reject.");
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejected = true; }
                Check(rejected && journal.Entries.Count == 4, "Cold recovery must refuse the corrupted isolated room without writing another accepted input.");
                await File.WriteAllTextAsync(Path.Combine(folder, "actual-refusal-errors.json"), JsonSerializer.Serialize(errors, Json));
            }
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_BATTLEFIELD_START_CAPTURED_CONTROLLER_ORIGINAL_BIRTH_AND_INPUT_IDENTITY",
                room, branch, originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold, deliberateTamperRecoveryRejected = rejected,
                originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                controlledScenarioExplicitlyRecordedNotAnOrdinaryMatch = true, noOldGameMigrationOrHistoryRewriteForPassing = true,
                wholePrintingOrNativeVisibleCertification = false }, Json));
            Console.WriteLine($"BATTLEFIELD_START_CONTROLLER_SQL_PASS {branch} {journal.Entries.Count} original inputs {cold} cold rejected={rejected}");

            async Task<ResolutionResult> Submit(string actor, GameCommand command)
            {
                var result = await session.SubmitAsync(actor, "original-" + journal.Entries.Count, command,
                    JsonSerializer.SerializeToElement(command, command.GetType(), Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "Original command rejected."); await Cold(); return result;
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                    "The original SQL frame and actual input identity must be consistent before cold recovery: "
                    + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0,
                    "Every original accepted prefix must strictly replay before cold recovery.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                cold++;
            }
            async Task Save(string name, object value)
            {
                await using var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(zip, value, value.GetType(), Json);
            }
            async Task SaveRows(string name)
            {
                var rows = new Dictionary<string, JsonElement>();
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" }) {
                    await using var command = new NpgsqlCommand($"select coalesce(json_agg(t order by row_to_json(t)::text),'[]'::json)::text from (select * from {table} where match_id=@room)t", connection);
                    command.Parameters.AddWithValue("room", room); using var doc = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!); rows[table] = doc.RootElement.Clone();
                }
                await Save(name, rows);
            }
        }
    }

    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    private sealed class OriginalJournal(PostgresMatchJournal inner, string folder) : IMatchRoomSeatJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        private async Task Save(MatchJournalEntry entry) {
            await using var file = new FileStream(Path.Combine(folder, $"entry-{Entries.Count + 1:D2}-original-before-persist.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, entry, Json);
        }
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { await Save(entry); await inner.RecordAsync(entry, token); Entries.Add(entry); }
        public async ValueTask RecordRoomSeatAsync(MatchJournalEntry entry, string tokenHash, CancellationToken token)
        { await Save(entry); await inner.RecordRoomSeatAsync(entry, tokenHash, token); Entries.Add(entry); }
    }
}
