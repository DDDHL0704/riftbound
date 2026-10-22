using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class LightningRushFlowSqlAcceptance
{
    private sealed record Case(string Name, string Scenario = "normal", bool Decline = false, string? Corruption = null);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh Lightning Rush SQL evidence is required."); Directory.CreateDirectory(output);
        Case[] cases = [new("normal-draw"), new("normal-decline", Decline: true), new("flow", "flow"),
            new("empty-flow", "flow-empty"), new("echo-flow", "flow-echo"), new("observed", "observed"),
            new("flow-counter", "flow-counter"), new("flow-return-counter", "flow-return-counter"), new("flow-control", "flow-control"),
            new("reject-missing-flow", "flow", Corruption: "missing-flow"), new("reject-generation", "flow", Corruption: "generation"),
            new("reject-old-catalog", "flow", Corruption: "old-catalog"), new("reject-choice-context", Corruption: "choice-context")];
        foreach (var branch in cases) await Run(branch);

        async Task Run(Case branch)
        {
            var folder = Path.Combine(output, branch.Name); Directory.CreateDirectory(folder);
            var room = "lightning-rush-flow-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); var state = (MatchState)null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "lightning-rush-" + branch.Scenario;
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The declared development position must be an original recorded command."); await Cold();
            await Submit("alice", new PlayCardCommand("PARENT", "VEN·156", [], OptionalCosts: branch.Scenario == "flow-echo" ? ["ECHO"] : []));
            var original = (await store.LoadAsync(room, default))!;
            Check(original.OpeningSeed != 0 && original.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"), "Original atomic seats, private seed and EMPTY origin are retained.");
            if (branch.Corruption == "choice-context") await Top();
            await SaveRows("original-before-continuation-seven-tables.json.gz");
            if (branch.Corruption is not null) { await CorruptAndRefuse(branch.Corruption); await Receipt(true); return; }
            if (branch.Scenario is "flow-counter" or "flow-return-counter" or "flow-control") {
                var target = state.StackItems.Single(s => s.SourceObjectId == "PARENT").StackItemId;
                await Submit("alice", new PassPriorityCommand());
                await Submit("bob", new PlayCardCommand("COUNTER", state.CardObjects["COUNTER"].CardNo!, [target]));
            }
            for (var steps = 0; state.StackItems.Count > 0 || state.PendingCardChoice is not null || state.PendingEffectPlay is not null || state.TriggerQueue.Count > 0; steps++)
            {
                Check(steps < 20, "The finite original spell, observation and counter Insight sequence must complete.");
                if (branch.Scenario == "flow-control" && state.StackItems.FirstOrDefault(s => s.SourceObjectId == "PARENT" && s.ControllerId == "bob") is { } controlled)
                    Check(state.CardObjects["PARENT"].ObjectGeneration == 1 && controlled.FlowPlay is { Source.Generation: 1, OwnerId: "alice" }
                        && state.PendingRuleChoice is null, "Control transfer preserves the original game object, Flow receipt and absence of casting targets.");
                if (state.PendingRuleChoice is { } rule) {
                    Check(rule.Request.Kind == "DECK_OBSERVATION_BANISH", "Only the actual observed card's optional banish is expected.");
                    await Submit(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
                }
                else if (state.PendingCardChoice is { } choice) {
                    Check(choice.ChoiceWindow is "DECK_EFFECT" or "INSIGHT" or "INSIGHT_ORDER", "Only the source's real private selections are expected.");
                    var selected = choice.ChoiceWindow == "INSIGHT_ORDER" ? choice.ContextObjectIds
                        : choice.ChoiceWindow == "INSIGHT" || branch.Decline ? []
                        : choice.LegalObjectIds.Contains("P1_FIRST_FILLER_1") ? new[] { "P1_FIRST_FILLER_1" }
                        : choice.LegalObjectIds.Take(1).ToArray();
                    await Submit(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
                }
                else if (state.PendingEffectPlay is { } pending) {
                    Check(pending.Parent.ObservedPlayPermission is not null, "Only Nocturne's separately earned play remains.");
                    var id = pending.Sources.Keys.Single();
                    await Submit(pending.PlayerId, new PlayCardCommand(id, state.CardObjects[id].CardNo!, []));
                }
                else if (state.TriggerQueue.Count > 0 && state.PriorityPlayerId is null)
                    await Submit("alice", new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                else await Top();
            }
            var own = state.PlayerZones["alice"]; var flow = branch.Scenario.StartsWith("flow", StringComparison.Ordinal);
            Check((flow ? own.Banished : own.Graveyard).Contains("PARENT")
                && !(flow ? own.Graveyard : own.Banished).Contains("PARENT") && state.CardObjects["PARENT"].ObjectGeneration == 2,
                "The physical spell makes exactly one owner-correct final transition; only Flow is banished.");
            if (branch.Scenario is "flow-counter" or "flow-return-counter")
                Check(own.MainDeck.Count == 7 && own.Hand.Count == 0 && own.Graveyard.Count == 0
                    && journal.Entries.SelectMany(e => e.Events).All(e => e.Kind != "CARDS_PUT_IN_GRAVEYARD"), "A countered Flow never performs its look/draw/grave instructions, including return-to-hand counters.");
            else if (branch.Scenario == "flow-empty")
                Check(own.MainDeck.Count == 0 && own.Hand.Count == 0 && own.Graveyard.Count == 0, "Empty look has no draw or burnout but completes Flow.");
            else if (branch.Scenario == "observed")
                Check(own.Base.Contains("P1_FIRST") && own.Hand.SequenceEqual(["P1_FIRST_FILLER_1"])
                    && own.Graveyard.Contains("P1_FIRST_FILLER_2") && !own.Graveyard.Contains("P1_FIRST"), "Observed Nocturne leaves the looked set, and is played only after the parent's draw/grave instructions.");
            else if (branch.Scenario == "flow-echo")
                Check(own.Hand.Count == 2 && own.Graveyard.Count == 4 && own.MainDeck.SequenceEqual(["P1_TAIL"]), "Echo executes two successive three-card instructions for one physical Flow spell.");
            else if (branch.Scenario == "flow-control")
                Check(own.MainDeck.Count == 7 && own.Hand.Count == 0 && own.Graveyard.Count == 0
                    && state.PlayerZones["bob"].Hand.Count == 1 && state.PlayerZones["bob"].Graveyard.Count == 3,
                    "The new controller draws from its own deck while the physical owner receives the final banished Flow card.");
            else
                Check(own.Hand.Count == (branch.Decline ? 0 : 1) && own.MainDeck.Count == 4
                    && own.Graveyard.Count == (branch.Decline ? 4 : flow ? 2 : 3), "Optional selected draw and every unchosen direct graveyard card have exact final counts.");
            Check(journal.Entries.SelectMany(e => e.Events).All(e => e.Kind is not ("CARDS_RECYCLED" or "CARD_DISCARDED" or "UNIT_DESTROYED")), "A direct graveyard instruction never masquerades as another action.");
            var final = (await store.LoadAsync(room, default))!;
            Check(final.OpeningSeed == original.OpeningSeed && MatchStateHasher.Hash(final.ReplayInitialState!) == MatchStateHasher.Hash(original.ReplayInitialState!)
                && final.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)), "No original command or opening identity is rewritten.");
            await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz"); await Receipt(false);

            async Task Top() { await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-snapshot.json"), JsonSerializer.Serialize(session.SnapshotFor(actor), Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-other-snapshot.json"), JsonSerializer.Serialize(session.SnapshotFor(actor == "alice" ? "bob" : "alice"), Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "An original Lightning Rush intent was rejected."); await Cold();
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                    "Actual SQL rows and running inputs must agree: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0, "The entire original prefix independently replays.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                state = frame.AuthoritativeState!; cold++;
            }
            async Task CorruptAndRefuse(string attack)
            {
                var forged = state;
                if (attack == "missing-flow") forged = state with { StackItems = state.StackItems.Select(s => s with { FlowPlay = null }).ToArray() };
                if (attack == "generation") {
                    var item = state.StackItems.Single(); var binding = new ObjectBinding("PARENT", 99);
                    forged = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["PARENT"] = state.CardObjects["PARENT"] with { ObjectGeneration = 99 } },
                        StackItems = [item with { FlowPlay = item.FlowPlay! with { Source = binding } }],
                        CardPlays = state.CardPlays with { Finalized = state.CardPlays.Finalized.Select(p => p.Source.ObjectId == "PARENT" ? p with { Source = binding } : p).ToArray() } };
                }
                if (attack == "old-catalog") { var identity = RuleInputIdentity.Current;
                    forged = state with { RuleInputs = new(1, "CN", "zh-CN", identity.AuthoritySha256,
                        "b04ddc91471924f8ba19913d50b87094089804271ef272f292c98bbcfd102298", identity.ImplementationSha256) }; }
                if (attack == "choice-context") {
                    var choice = state.PendingCardChoice!;
                    forged = state with { PendingCardChoice = choice with { ContextObjectIds = choice.ContextObjectIds.Append("P1_TAIL").ToArray(), LegalObjectIds = choice.LegalObjectIds.Append("P1_TAIL").ToArray() } };
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
                        Check(await cmd.ExecuteNonQueryAsync() == 1, "Each deliberately corrupted latest original view must be unique.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!;
                await Save("actual-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Actual coordinated SQL forgery must fail against the original accepted input log.");
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
                Check(cold == journal.Entries.Count, "Every accepted SQL command has an actual complete cold recovery.");
                await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_SQL_LIGHTNING_RUSH_FLOW_SCOPED", room, branch = branch.Name, scenario,
                    originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold, deliberateTamperRecoveryRejected = rejected,
                    originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                    controlledPositionNotOrdinaryOpening = true, noHistoryRewrite = true, wholeCardOrNativeVisibleCertification = false }, Json));
                Console.WriteLine($"LIGHTNING_RUSH_FLOW_SQL_PASS {branch.Name} inputs={journal.Entries.Count} cold={cold} rejected={rejected}");
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
