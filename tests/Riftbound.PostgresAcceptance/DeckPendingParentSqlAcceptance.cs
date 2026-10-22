using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class DeckPendingParentSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh pending-parent SQL output required."); Directory.CreateDirectory(output);
        foreach (var (branch, card) in new[] { ("reinforcements", "OGN·062/298"), ("void-rush", "SFD·188/221"),
            ("blind-fury", "OGN·025/298"), ("bright-future", "OGN·115/298") })
            await Run(branch, card, false, true, null);
        foreach (var (branch, card) in new[] { ("reinforcements-observed", "OGN·062/298"), ("bright-future-observed", "OGN·115/298") })
        foreach (var play in new[] { true, false }) await Run(branch, card, true, play, null);
        foreach (var attack in new[] { "missing-slot", "premature-parent", "coordinated-generation", "wrong-identity" })
            await Run("reinforcements", "OGN·062/298", false, true, attack);

        async Task Run(string scenarioBranch, string card, bool observed, bool playObserved, string? attack)
        {
            var branch = scenarioBranch + (observed ? playObserved ? "-paid" : "-declined" : "") + (attack is null ? "" : "-reject-" + attack);
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "deck-pending-parent-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); var cold = 0; var state = (MatchState)null!;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "deck-pending-" + scenarioBranch;
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The declared controlled setup must be recorded."); await Cold();
            await Submit("alice", new PlayCardCommand("PARENT", card, [], OptionalCosts: ["ECHO"]));
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            var first = card == "OGN·115/298" ? new[] { "P1_FIRST", "P2_FIRST" }
                : card == "OGN·025/298" ? ["P2_FIRST"] : ["P1_FIRST"];
            var order = card == "OGN·115/298" ? new[] { "P2_FIRST", "P1_FIRST" } : first;
            foreach (var id in first) await Choose(id);
            if (observed)
            {
                var choice = state.PendingRuleChoice!;
                Check(choice.Request.Kind == "DECK_OBSERVATION_BANISH", "The second observation must use the actual static replacement.");
                await Submit("alice", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
            }
            var parent = state.StackItems.Single(s => s.SourceObjectId == "PARENT");
            Check(!parent.DeckPlaySequence!.ParentCompleted && parent.DeckPlaySequence.ExecutionIndex == 1
                && state.PendingEffectPlay is null && state.PendingCardChoice is not null
                && state.StackItems.Where(s => s.DeckPlayPendingCard is not null).Select(s => s.SourceObjectId).SequenceEqual(order)
                && order.All(id => state.ObjectLocations[id].Zone == "STACK" && state.CardObjects[id].ObjectGeneration == 2)
                && state.PlayerZones.Values.All(z => !order.Any(z.Banished.Contains)),
                "Each first-execution play is pending before the original parent continues; no confirmation or early priority.");
            await SaveRows("original-pending-parent-seven-tables.json.gz");
            foreach (var actor in new[] { "alice", "bob" })
            {
                var rejected = await new CoreRuleEngine().ResolveAsync(state, new("early-pass", actor, CommandTypes.PassPriority), new PassPriorityCommand(), default);
                Check(!rejected.Accepted && MatchStateHasher.Hash(rejected.State) == MatchStateHasher.Hash(state), "Neither actor may pass priority during the parent choice.");
            }
            var original = (await store.LoadAsync(room, default))!;
            Check(original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT") && original.ReplayInitialState is { Status: "EMPTY", Seats.Count: 0 }
                && original.OpeningSeed != 0, "Two original atomic seats and one declared QA position retain their actual private opening seed and EMPTY origin.");
            if (attack is not null)
            {
                var c = parent.DeckPlaySequence!; var slot = state.StackItems.Single(s => s.DeckPlayPendingCard is not null);
                var cards = state.CardObjects.ToDictionary();
                IReadOnlyList<StackItemState> stack = state.StackItems;
                if (attack == "missing-slot") stack = stack.Where(s => s.StackItemId != slot.StackItemId).ToArray();
                if (attack == "premature-parent") stack = stack.Select(s => s.StackItemId == parent.StackItemId ? s with { EffectPlayCompleted = true } : s).ToArray();
                if (attack == "coordinated-generation")
                {
                    var binding = new ObjectBinding("P1_FIRST", 99);
                    cards["P1_FIRST"] = cards["P1_FIRST"] with { ObjectGeneration = 99 };
                    stack = stack.Select(s => s.StackItemId == parent.StackItemId ? s with { DeckPlaySequence = c with {
                            Queue = [c.Queue[0] with { Card = binding, BeforeBanishGeneration = 97 }] } }
                        : s.StackItemId == slot.StackItemId ? s with { DeckPlayPendingCard = s.DeckPlayPendingCard! with { Card = binding } } : s).ToArray();
                }
                var identity = RuleInputIdentity.Current;
                var forged = state with { CardObjects = cards, StackItems = stack, RuleInputs = attack == "wrong-identity"
                    ? new RuleInputIdentity(identity.SchemaVersion, identity.Region, identity.Language, identity.AuthoritySha256, identity.CatalogSha256, new string('a', 64)) : identity };
                await using (var connection = await source.OpenConnectionAsync())
                await using (var transaction = await connection.BeginTransactionAsync())
                {
                    await Write("state_snapshots", "state_tick", null, forged);
                    foreach (var view in ResolutionResult.BuildSnapshots(forged)) await Write("snapshots", "snapshot_tick", view.Key, view.Value);
                    foreach (var view in ResolutionResult.BuildPrompts(forged)) await Write("action_prompts", "prompt_tick", view.Key, view.Value);
                    await transaction.CommitAsync();
                    async Task Write(string table, string column, string? actor, object value)
                    {
                        await using var command = new NpgsqlCommand($"update {table} set payload=@payload where match_id=@room and {column}=@tick" + (actor is null ? "" : " and player_id=@actor"), connection, transaction);
                        command.Parameters.AddWithValue("room", room); command.Parameters.AddWithValue("tick", state.Tick);
                        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(value, value.GetType(), Json));
                        if (actor is not null) command.Parameters.AddWithValue("actor", actor);
                        Check(await command.ExecuteNonQueryAsync() == 1, "Exactly one latest row per view in this isolated deliberate-corruption room.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!;
                await Save("actual-deliberately-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Independent original-input replay must reject the actual corrupt SQL frame.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "Actual cold registry must refuse the corrupted room without rewriting its accepted history.");
                await Receipt(true, errors); return;
            }
            while (state.PendingCardChoice is { } choice)
            {
                var second = choice.ContextObjectIds.FirstOrDefault(id => id.EndsWith("_SECOND", StringComparison.Ordinal));
                await Submit(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, second is not null ? [second]
                    : choice.RequiredCount == 0 ? [] : choice.LegalObjectIds.Take(1).ToArray()));
            }
            Check(state.PlayerZones["alice"].Graveyard.Contains("PARENT"), "The actual parent completes before any chosen card confirms.");
            foreach (var id in order)
            {
                Check(state.PendingEffectPlay is { Parent.ObservedPlayPermission: null } && state.PendingEffectPlay.Sources.Keys.Single() == id,
                    "A later observed permission cannot jump the earlier original pending cards.");
                await PlayCurrent();
            }
            if (observed)
            {
                var permission = state.PendingEffectPlay!;
                Check(permission.Parent.ObservedPlayPermission is not null && permission.Sources.Keys.Single() == "P1_SECOND", "The second-execution observation follows all earlier pending cards.");
                if (playObserved) await PlayCurrent();
                else await Submit("alice", new ChooseCardsCommand(permission.ChoiceId, "EFFECT_PLAY", []));
            }
            while (state.PendingEffectPlay is not null) await PlayCurrent();
            Check(state.PendingCardChoice is null && state.StackItems.Count == 0 && state.PriorityPlayerId is null,
                "All original queued cards finish their ordinary play or inability and return to the actual open main.");
            if (observed) Check(state.PlayerZones["alice"].Base.Contains("P1_SECOND") == playObserved, "The paid and declined observation branches retain distinct final locations.");
            var final = (await store.LoadAsync(room, default))!;
            Check(final.OpeningSeed == original.OpeningSeed && MatchStateHasher.Hash(final.ReplayInitialState!) == MatchStateHasher.Hash(original.ReplayInitialState!)
                && final.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)),
                "The original prefix, private seed and EMPTY initial state are byte-semantic unchanged; no old-game migration.");
            await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz"); await Receipt(false, []);

            async Task PlayCurrent()
            {
                var pending = state.PendingEffectPlay!; var id = pending.Sources.Keys.Single();
                var command = new PlayCardCommand(id, state.CardObjects[id].CardNo!, []);
                var quote = new CoreRuleEngine().PreviewPlayCard(state, pending.PlayerId, new("quote", session.PromptFor(pending.PlayerId).PromptId!, state.Tick, command));
                if (quote.CanPay) await Submit(pending.PlayerId, command);
                else await Submit(pending.PlayerId, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            }
            async Task Choose(string id)
            {
                var choice = state.PendingCardChoice!; await Submit(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
            }
            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
                raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-snapshot.json"), JsonSerializer.Serialize(session.SnapshotFor(actor), Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "Original pending-parent input rejected."); await Cold();
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current, "Actual SQL payloads and current inputs must match: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0, "Every original accepted prefix must strictly replay.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                state = frame.AuthoritativeState!; cold++;
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
            async Task Receipt(bool rejected, IReadOnlyList<string> errors)
            {
                Check(cold == journal.Entries.Count, "Every original accepted SQL input has an independent cold recovery.");
                await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "REAL_SQL_DECK_PENDING_BEFORE_PARENT_AND_ORDERED_OBSERVATION", room, branch, scenario,
                    originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold, deliberateTamperRecoveryRejected = rejected,
                    originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current, actualRefusalErrors = errors,
                    controlledScenarioExplicitlyRecordedNotOrdinaryDeckValidation = true, noHistoryRewriteForPassing = true,
                    wholeCardOrNativeVisibleCertification = false }, Json));
                Console.WriteLine($"DECK_PENDING_PARENT_SQL_PASS {branch} inputs={journal.Entries.Count} cold={cold} rejected={rejected}");
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
