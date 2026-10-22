using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class WildClawReflexiveSqlAcceptance
{
    private sealed record Case(string Name, string Scenario = "unit", bool Create = true, bool EmptySelection = false,
        bool PlayObserved = true, string? Attack = null);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh Wild Claw SQL evidence is required."); Directory.CreateDirectory(output);
        Case[] cases = [new("unit"), new("gear", "gear"), new("no-create", Create: false),
            new("decline-card", EmptySelection: true), new("empty", "empty"), new("unpayable", "unpayable"),
            new("echo", "echo"), new("observed-first-paid", "observed-first"),
            new("observed-second-declined", "observed-second", PlayObserved: false),
            new("recall", "recall"), new("reentry", "reentry"), new("mandatory", "mandatory"), new("insight", "insight"),
            new("reject-missing-ability", Attack: "missing-ability"), new("reject-early-confirmation", Attack: "early-confirmation"),
            new("reject-coordinated-generation", Attack: "coordinated-generation"), new("reject-old-catalog", Attack: "old-catalog")];
        foreach (var branch in cases) await Run(branch);

        async Task Run(Case branch)
        {
            var folder = Path.Combine(output, branch.Name); Directory.CreateDirectory(folder);
            var room = "wild-claw-reflexive-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); var state = (MatchState)null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "wild-claw-" + branch.Scenario;
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The explicit QA position must be an original recorded command."); await Cold();
            var echo = branch.Scenario is "echo" or "observed-second";
            await Submit("alice", new PlayCardCommand("PARENT", "VEN·089", [], OptionalCosts: echo ? ["ECHO"] : [])); await Top();
            for (var decisions = 0; !state.PlayerZones["alice"].Graveyard.Contains("PARENT"); decisions++)
            {
                Check(decisions < 12, "The finite parent choice sequence must complete.");
                if (state.PendingRuleChoice is { } rule)
                {
                    Check(rule.Request.Kind is "DECK_OBSERVATION_BANISH" or "DECK_REFLEXIVE_CREATE", "Only the source's exact synchronous choices are expected.");
                    var pick = rule.Request.Kind == "DECK_OBSERVATION_BANISH" ? "BANISH" : branch.Create ? "CREATE" : "DECLINE";
                    await Submit(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [pick]));
                }
                else if (state.PendingCardChoice is { ChoiceWindow: "DECK_PLAY_SELECTION" } choice)
                {
                    var parent = state.StackItems.Single(s => s.StackItemId == choice.DeckContext!.StackItemId);
                    var execution = parent.DeckPlaySequence!.ExecutionIndex;
                    var empty = branch.EmptySelection || branch.Scenario == "observed-second" && execution == 1;
                    var id = branch.Scenario == "observed-first" ? "P1_FIRST_FILLER_1" : execution == 0 ? "P1_FIRST" : "P1_SECOND";
                    Check(empty || choice.LegalObjectIds.Contains(id), "The exact selected unit or gear is a current legal viewed card.");
                    await Submit(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, empty ? [] : [id]));
                }
                else throw new InvalidOperationException("The parent ended without its expected finite continuation.");
            }
            var original = (await store.LoadAsync(room, default))!;
            Check(original.ReplayInitialState is { Status: "EMPTY", Seats.Count: 0 } && original.OpeningSeed != 0
                && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"), "Retain both atomic seats, the private seed, and the actual EMPTY origin.");
            await SaveRows("original-before-confirmation-seven-tables.json.gz");
            if (branch.Attack is not null)
            {
                await CorruptAndRefuse(branch.Attack); await Receipt(true); return;
            }
            var order = new List<string>();
            while (state.PendingEffectPlay is { } pending)
            {
                var id = pending.Sources.Keys.Single();
                if (pending.Parent.ObservedPlayPermission is not null && !branch.PlayObserved)
                    await Submit(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                else if (branch.Scenario == "unpayable")
                {
                    Check(state.RunePools["alice"].TotalPower == 0, "The parent's orange energy was really consumed.");
                    var play = new PlayCardCommand(id, state.CardObjects[id].CardNo!, []);
                    var before = MatchStateHasher.Hash(state); var rejected = await new CoreRuleEngine().ResolveAsync(state, new("unpayable", "alice", CommandTypes.PlayCard), play, default);
                    Check(!rejected.Accepted && rejected.Events.Count == 0 && before == MatchStateHasher.Hash(rejected.State), "The real remaining printed power cannot be waived.");
                    await Submit(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                }
                else
                {
                    var play = new PlayCardCommand(id, state.CardObjects[id].CardNo!, [], OptionalCosts:
                        branch.Scenario == "mandatory" ? ["RETURN_FRIENDLY_EQUIPMENT:COST_GEAR"] : []);
                    await Submit(pending.PlayerId, play); order.Add(id);
                }
            }
            if (branch.Scenario is "recall" or "reentry")
            {
                var captured = state.StackItems.Single().DeckPlayReflexive!.Played;
                await Submit("alice", new PlayCardCommand("RESPONSE", state.CardObjects["RESPONSE"].CardNo!, ["P1_FIRST"])); await Top();
                if (branch.Scenario == "reentry")
                    await Submit("alice", new PlayCardCommand("P1_FIRST", "OGN·096/298", [], Destination: "BATTLEFIELD:FIELD"));
                Check(state.CardObjects["P1_FIRST"].ObjectGeneration > captured!.Generation
                    && state.StackItems.Single().DeckPlayReflexive!.Played == captured, "An unrelated zone change never rebinds the old ability.");
            }
            for (var steps = 0; state.StackItems.Count > 0 || state.PendingCardChoice is not null || state.TriggerQueue.Count > 0; steps++)
            {
                Check(steps < 12, "The finite reflexive and earned Insight sequence must finish.");
                if (state.PendingCardChoice is { } choice)
                {
                    Check(choice.ChoiceWindow is "INSIGHT" or "INSIGHT_ORDER", "Only the actually earned Insight choices remain.");
                    await Submit(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                        choice.ChoiceWindow == "INSIGHT_ORDER" ? choice.ContextObjectIds : []));
                }
                else if (state.TriggerQueue.Count > 0 && state.PriorityPlayerId is null)
                    await Submit("alice", new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                else await Top();
            }
            var wanted = branch.Create && !branch.EmptySelection && branch.Scenario is not ("empty" or "unpayable" or "recall" or "reentry") ? echo && branch.Scenario == "echo" ? 2 : 1 : 0;
            Check(journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "OBJECT_EMPOWERED") == wanted, "Exact number of real empower state changes, excluding declined, absent, or departed cards.");
            if (branch.Scenario == "observed-first") Check(!state.CardObjects["P1_FIRST"].IsEmpowered && state.CardObjects["P1_FIRST_FILLER_1"].IsEmpowered, "The observed card is not the normal selected card.");
            if (branch.Scenario == "observed-second") Check(state.PlayerZones["alice"].Banished.Contains("P1_SECOND") && state.CardObjects["P1_FIRST"].IsEmpowered, "The later declined permission cannot overtake or become the earlier reference.");
            if (branch.Scenario == "mandatory") Check(state.PlayerZones["alice"].Hand.Contains("COST_GEAR"), "The mandatory equipment return was really paid.");
            if (branch.Scenario == "echo") Check(order.SequenceEqual(["P1_FIRST", "P1_SECOND"]), "Repeated cards confirm in their original insertion order.");
            var final = (await store.LoadAsync(room, default))!;
            Check(final.OpeningSeed == original.OpeningSeed && MatchStateHasher.Hash(final.ReplayInitialState!) == MatchStateHasher.Hash(original.ReplayInitialState!)
                && final.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)), "No migration or history rewrite.");
            await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz"); await Receipt(false);

            async Task Top()
            {
                await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            }
            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-snapshot.json"), JsonSerializer.Serialize(session.SnapshotFor(actor), Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-other-snapshot.json"), JsonSerializer.Serialize(session.SnapshotFor(actor == "alice" ? "bob" : "alice"), Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "An original Wild Claw input was rejected."); await Cold();
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                    "Actual SQL rows and running inputs must match: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0, "The full original prefix must independently replay.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                state = frame.AuthoritativeState!; cold++;
            }
            async Task CorruptAndRefuse(string attack)
            {
                var item = state.StackItems.Single(s => s.DeckPlayReflexive is not null); var c = item.DeckPlayReflexive!;
                var forged = state;
                if (attack == "missing-ability") forged = state with { StackItems = state.StackItems.Where(s => s.StackItemId != item.StackItemId).ToArray() };
                if (attack == "early-confirmation") forged = state with { StackItems = state.StackItems.Select(s => s.StackItemId == item.StackItemId ? s with { DeckPlayReflexive = c with { Confirmed = true } } : s).ToArray() };
                if (attack == "old-catalog") {
                    var identity = RuleInputIdentity.Current;
                    forged = state with { RuleInputs = new(1, "CN", "zh-CN", identity.AuthoritySha256,
                        "e7455edc32d43f8253a9d0c6fedb83c347bebafbef064f9b8dffaaabc4b4d266", identity.ImplementationSha256) };
                }
                if (attack == "coordinated-generation") {
                    var pending = state.PendingEffectPlay!; var parent = pending.Parent; var sequence = parent.DeckPlaySequence!;
                    var binding = new ObjectBinding("P1_FIRST", 99); var selected = sequence.Queue[0] with { Card = binding, BeforeBanishGeneration = 97 };
                    var viewed = sequence.Viewed.ToDictionary(); viewed["alice"] = viewed["alice"].Select(b => b.ObjectId == "P1_FIRST" ? b with { Generation = 97 } : b).ToArray();
                    sequence = sequence with { Viewed = viewed, Queue = [selected], Selected = new Dictionary<string, ObjectBinding> { ["alice"] = new("P1_FIRST", 98) } };
                    forged = state with {
                        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["P1_FIRST"] = state.CardObjects["P1_FIRST"] with { ObjectGeneration = 99 } },
                        PendingEffectPlay = pending with { Parent = parent with { DeckPlaySequence = sequence }, Sources = new Dictionary<string, long> { ["P1_FIRST"] = 99 } },
                        StackItems = state.StackItems.Select(s => s.DeckPlayPendingCard is { } card ? s with { DeckPlayPendingCard = card with { Card = binding } }
                            : s.StackItemId == item.StackItemId ? s with { DeckPlayReflexive = c with { Selected = selected } } : s).ToArray() };
                }
                await using (var connection = await source.OpenConnectionAsync())
                await using (var transaction = await connection.BeginTransactionAsync()) {
                    await Write("state_snapshots", "state_tick", null, forged);
                    foreach (var v in ResolutionResult.BuildSnapshots(forged)) await Write("snapshots", "snapshot_tick", v.Key, v.Value);
                    foreach (var v in ResolutionResult.BuildPrompts(forged)) await Write("action_prompts", "prompt_tick", v.Key, v.Value);
                    await transaction.CommitAsync();
                    async Task Write(string table, string tick, string? actor, object value) {
                        await using var cmd = new NpgsqlCommand($"update {table} set payload=@payload where match_id=@room and {tick}=@tick" + (actor is null ? "" : " and player_id=@actor"), connection, transaction);
                        cmd.Parameters.AddWithValue("room", room); cmd.Parameters.AddWithValue("tick", state.Tick);
                        cmd.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(value, value.GetType(), Json));
                        if (actor is not null) cmd.Parameters.AddWithValue("actor", actor);
                        Check(await cmd.ExecuteNonQueryAsync() == 1, "Exactly one deliberately corrupted latest row is required per view.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!;
                await Save("actual-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Original accepted inputs must reject the actual coordinated SQL forgery.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "A fresh registry must refuse the actual corrupt database room.");
                await File.WriteAllTextAsync(Path.Combine(folder, "actual-refusal-errors.json"), JsonSerializer.Serialize(errors, Json));
            }
            async Task Save(string name, object value) {
                await using var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, value, value.GetType(), Json);
            }
            async Task SaveRows(string name) {
                var rows = new Dictionary<string, JsonElement>(); await using var connection = await source.OpenConnectionAsync();
                foreach (var table in Tables) {
                    await using var cmd = new NpgsqlCommand($"select coalesce(json_agg(t order by row_to_json(t)::text),'[]'::json)::text from (select * from {table} where match_id=@room)t", connection);
                    cmd.Parameters.AddWithValue("room", room); using var document = JsonDocument.Parse((string)(await cmd.ExecuteScalarAsync())!); rows[table] = document.RootElement.Clone();
                }
                await Save(name, rows);
            }
            async Task Receipt(bool rejected) {
                Check(cold == journal.Entries.Count, "Every accepted SQL command must have a real cold recovery.");
                await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_SQL_WILD_CLAW_REFLEXIVE_SCOPED", room, branch = branch.Name, scenario,
                    originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold, deliberateTamperRecoveryRejected = rejected,
                    originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                    controlledPositionNotOrdinaryOpening = true, noHistoryRewrite = true, wholeCardOrNativeVisibleCertification = false }, Json));
                Console.WriteLine($"WILD_CLAW_REFLEXIVE_SQL_PASS {branch.Name} inputs={journal.Entries.Count} cold={cold} rejected={rejected}");
            }
        }
    }

    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    private sealed class OriginalJournal(PostgresMatchJournal inner, string folder) : IMatchRoomSeatJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        private async Task Save(MatchJournalEntry entry) {
            await using var file = new FileStream(Path.Combine(folder, $"entry-{Entries.Count + 1:D2}-original-before-persist.json.gz"), FileMode.CreateNew);
            await using var zip = new GZipStream(file, CompressionLevel.SmallestSize); await JsonSerializer.SerializeAsync(zip, entry, Json);
        }
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { await Save(entry); await inner.RecordAsync(entry, token); Entries.Add(entry); }
        public async ValueTask RecordRoomSeatAsync(MatchJournalEntry entry, string hash, CancellationToken token) { await Save(entry); await inner.RecordRoomSeatAsync(entry, hash, token); Entries.Add(entry); }
    }
}
