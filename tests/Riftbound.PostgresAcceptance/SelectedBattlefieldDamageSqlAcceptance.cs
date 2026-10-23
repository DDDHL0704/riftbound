using System.IO.Compression;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class SelectedBattlefieldDamageSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Selected-field SQL evidence needs a fresh output.");
        Directory.CreateDirectory(output);
        foreach (var card in new[] { "bullet", "drop", "crescent" })
        {
            foreach (var gate in new[] { false, true })
            foreach (var returned in new[] { false, true }) await Run(card, gate, returned, false, false);
            await Run(card, false, false, false, true);
        }
        foreach (var gate in new[] { false, true }) await Run("bullet", gate, false, true, false);

        async Task Run(string branch, bool gate, bool returned, bool zero, bool forged)
        {
            var name = branch + (gate ? "-gate" : "-plain") + (returned ? "-return" : "-keep") + (zero ? "-zero" : "") + (forged ? "-forge" : "");
            var folder = Path.Combine(output, name); Directory.CreateDirectory(folder);
            var room = "selected-field-damage-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); MatchState state = null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "selected-damage-" + branch + (gate ? "-gate" : "");
            var setup = await session.SeedScenarioAsync("alice", "original-selected-field-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }, Json), default);
            Check(setup.Accepted, "The finite position must be an original accepted command."); await Cold();
            var opening = (await store.LoadAsync(room, default))!;
            Check(opening.OpeningSeed != 0 && opening.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && opening.Commands.Count == 3, "Empty room, original seats and finite seed stay in the complete history.");
            var card = branch == "bullet" ? "OGN·268/298" : branch == "drop" ? "OGN·250/298" : "UNL-072/219";
            string[] Targets(string field) => branch == "drop" ? ["P1-BASE", field]
                : branch == "crescent" ? [field, field == "A" ? "P2-A1" : "P2-B"] : [field];
            var play = new PlayCardCommand("SPELL", card, Targets("A"), OptionalCosts: branch == "bullet" && !zero ? ["SPEND_POWER:3"] : null);
            await Submit("alice", play, duplicate: true);
            var original = state.StackItems.Single();
            Check(original.TargetObjectIds.SequenceEqual(Targets("A")), "Persist the actual field choice in its ordered target slots.");
            if (forged)
            {
                await Save("original-final-frame.json.gz", (await store.LoadAsync(room, default))!);
                await SaveRows("original-final-seven-tables.json.gz"); await CorruptAndRefuse();
            }
            else
            {
                if (returned)
                {
                    var actor = branch == "drop" ? "alice" : "bob";
                    if (actor == "bob") await Submit("alice", new PassPriorityCommand());
                    var id = branch == "drop" ? "P1-BASE" : "P2-A1";
                    await Submit(actor, new PlayCardCommand(actor == "alice" ? "P1-RETURN" : "P2-RETURN", "OGN·104/298", [id]));
                    await Resolve(state.StackItems.Last().StackItemId);
                    Check(state.PlayerZones[actor].Hand.Contains(id) && state.CardObjects[id].ObjectGeneration > 0
                        && state.StackItems.Single().TargetObjectIds.SequenceEqual(original.TargetObjectIds),
                        "A real response changes the target incarnation without rewriting the original choice.");
                }
                await Resolve(original.StackItemId);
                var damage = journal.Entries.SelectMany(e => e.Events).Where(e => e.Kind == "DAMAGE_APPLIED"
                    && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "SPELL")).ToArray();
                var bonus = 1 + (gate ? 1 : 0);
                if (branch == "drop" && returned)
                    Check(damage.Length == 0 && !journal.Entries.SelectMany(e => e.Events).Any(e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"),
                        "A missing selected unit supplies no computable Might or movement.");
                else
                {
                    var first = (branch == "bullet" ? zero ? 0 : 3 : 4) + bonus;
                    var other = (branch == "crescent" ? 1 : branch == "bullet" ? zero ? 0 : 3 : 4) + bonus;
                    Check(damage.Length == (returned ? 1 : 2) && (returned || state.CardObjects["P2-A1"].Damage == first)
                        && state.CardObjects["P2-A2"].Damage == other, "Current selected-field recipients receive each global and local bonus once.");
                    if (branch == "drop") Check(state.ObjectLocations["P1-BASE"].BattlefieldObjectId == "A"
                        && state.BattleState is { IsActive: true, BattlefieldObjectId: "A" } && state.FocusPlayerId == "alice",
                        "The movement uses the same chosen field and opens the actual contested battle.");
                }
                Check(state.CardObjects["P2-B"].Damage == 0 && state.CardObjects["ANNIE"].Damage == 0
                    && state.CardObjects["P2-BASE"].Damage == 0 && state.StackItems.Count == 0,
                    "Other fields, friendly units and bases receive no selected-field damage.");
                await Save("original-final-frame.json.gz", (await store.LoadAsync(room, default))!); await SaveRows("original-final-seven-tables.json.gz");
            }
            Check(cold == journal.Entries.Count, "Every original prefix was cold restored and independently replayed.");
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_SELECTED_FIELD_DAMAGE_SCOPED", room, branch = name, card, scenario,
                originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold, duplicateOriginalIntentNoMutation = true,
                structurallyConsistentOriginalJournalForgeryRejected = forged, actualRuleInputs = RuleInputIdentity.Current,
                originalPrivateSeed = opening.OpeningSeed, controlledPositionNotOrdinaryOpening = true,
                noAcceptedHistoryRewrite = true, wholeCardCertification = false }, Json));
            Console.WriteLine($"SELECTED_FIELD_DAMAGE_SQL_PASS {name} inputs={journal.Entries.Count} cold={cold}");

            async Task Resolve(string id)
            {
                for (var i = 0; state.StackItems.Any(s => s.StackItemId == id); i++)
                { Check(i < 4, "A response or spell must finish in the bounded position."); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            }
            async Task Submit(string actor, GameCommand command, bool duplicate = false)
            {
                var id = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, id + "-original-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, id + "-original-command.json"), raw.ToJsonString());
                var payload = JsonSerializer.SerializeToElement(raw, Json);
                var result = await session.SubmitAsync(actor, id, command, payload, default);
                Check(result.Accepted, result.ErrorMessage ?? "Selected-field intent was rejected."); await Cold();
                if (!duplicate) return;
                var before = MatchStateHasher.Hash(state); var count = journal.Entries.Count;
                var replay = await session.SubmitAsync(actor, id, command, payload, default);
                Check(replay.Accepted && MatchStateHasher.Hash(replay.State) == before && journal.Entries.Count == count,
                    "Cold-restored duplicate intent must leave the authoritative state and SQL history unchanged.");
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                    "Real SQL views and loaded input identity agree: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0,
                    "Every entire original prefix independently replays.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                state = frame.AuthoritativeState!; cold++;
            }
            async Task CorruptAndRefuse()
            {
                var targets = Targets("B");
                var forgedState = state with { StackItems = [original with { TargetObjectIds = targets,
                    TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration) }] };
                await using (var connection = await source.OpenConnectionAsync())
                await using (var transaction = await connection.BeginTransactionAsync())
                {
                    await Write("state_snapshots", "state_tick", null, forgedState);
                    foreach (var v in ResolutionResult.BuildSnapshots(forgedState)) await Write("snapshots", "snapshot_tick", v.Key, v.Value);
                    foreach (var v in ResolutionResult.BuildPrompts(forgedState)) await Write("action_prompts", "prompt_tick", v.Key, v.Value);
                    await transaction.CommitAsync();
                    async Task Write(string table, string tick, string? actor, object value)
                    {
                        await using var command = new NpgsqlCommand($"update {table} set payload=@payload where match_id=@room and {tick}=@tick" + (actor is null ? "" : " and player_id=@actor"), connection, transaction);
                        command.Parameters.AddWithValue("room", room); command.Parameters.AddWithValue("tick", state.Tick);
                        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(value, value.GetType(), Json));
                        if (actor is not null) command.Parameters.AddWithValue("actor", actor);
                        Check(await command.ExecuteNonQueryAsync() == 1, "Only this new room's one latest view is deliberately corrupted.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!; await Save("actual-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                Check(corrupt.IsConsistent, "All coordinated views pass structural checks: " + string.Join("; ", corrupt.ValidationErrors));
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Actual field selection must be authenticated against the original command history.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "A fresh registry refuses the actual coherently corrupted SQL room.");
                await File.WriteAllTextAsync(Path.Combine(folder, "actual-refusal-errors.json"), JsonSerializer.Serialize(errors, Json));
            }
            async Task Save(string name, object value)
            {
                await using var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, value, value.GetType(), Json);
            }
            async Task SaveRows(string name)
            {
                var rows = new Dictionary<string, JsonElement>(); await using var connection = await source.OpenConnectionAsync();
                foreach (var table in Tables)
                {
                    await using var command = new NpgsqlCommand($"select coalesce(json_agg(t order by row_to_json(t)::text),'[]'::json)::text from (select * from {table} where match_id=@room)t", connection);
                    command.Parameters.AddWithValue("room", room); using var document = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!); rows[table] = document.RootElement.Clone();
                }
                await Save(name, rows);
            }
        }
    }

    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private sealed class OriginalJournal(PostgresMatchJournal inner, string folder) : IMatchRoomSeatJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        private async Task Save(MatchJournalEntry entry)
        {
            await using var file = new FileStream(Path.Combine(folder, $"entry-{Entries.Count + 1:D2}-original-before-persist.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, entry, Json);
        }
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { await Save(entry); await inner.RecordAsync(entry, token); Entries.Add(entry); }
        public async ValueTask RecordRoomSeatAsync(MatchJournalEntry entry, string hash, CancellationToken token) { await Save(entry); await inner.RecordRoomSeatAsync(entry, hash, token); Entries.Add(entry); }
    }
}
