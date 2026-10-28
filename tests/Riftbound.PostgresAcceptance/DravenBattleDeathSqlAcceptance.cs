using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class DravenBattleDeathSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];
    private const string Effect = "CAPTURED_DRAVEN_BATTLE_DEATH_OPPONENT_SCORE";
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh Draven death SQL evidence is required."); Directory.CreateDirectory(output);
        foreach (var branch in new[] { "normal", "alt-normal", "winning", "mutual-normal", "mutual-winning",
            "altar-pay", "altar-decline", "mixed-defender", "recycle", "forge-generation", "forge-printing", "forge-role" }) await Run(branch);

        async Task Run(string branch)
        {
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "draven-battle-death-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); MatchState state = null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var winning = branch.EndsWith("winning", StringComparison.Ordinal);
            var mutual = branch.StartsWith("mutual", StringComparison.Ordinal);
            var forge = branch.StartsWith("forge-", StringComparison.Ordinal);
            var altar = branch.StartsWith("altar-", StringComparison.Ordinal);
            var mixed = branch == "mixed-defender";
            var scenario = "draven-death-" + (altar ? "altar" : forge || branch == "recycle" ? "normal" : branch);
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-draven-death-position", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The controlled position is an original accepted command."); await Cold();
            var original = (await store.LoadAsync(room, default))!;
            Check(original.OpeningSeed != 0 && original.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && original.Commands.Count == 3 && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"),
                "Original seats, private opening and controlled seed all remain in the action log.");
            await Submit("alice", new MoveUnitCommand("P1-UNIT", "BASE", "BATTLEFIELD:FIELD", []));
            await Submit("alice", new PassFocusCommand()); await Submit("bob", new PassFocusCommand());
            await Submit("alice", new DeclareBattleCommand("FIELD", ["P1-UNIT"], ["P2-UNIT"], ["COMBAT_ASSIGNMENT"]));
            if (altar)
            {
                var c = state.PendingRuleChoice!.Request;
                Check(state.StackItems.Count == 0, "Replacement choice precedes any committed death skill.");
                await Submit("alice", new PayCostCommand(c.Id, "RULE_REPLACEMENT", [branch == "altar-pay" ? "ALTAR:P1-UNIT:FIELD" : "DECLINE"]));
                if (branch == "altar-decline") Check(state.StackItems.Single().DravenBattleDeathScore!.DeathTick == state.Tick,
                    "The eventual accepted replacement command supplies the actual death completion tick.");
            }
            if (forge || branch == "recycle") await Confirm();
            if (forge)
            {
                await Save("original-final-frame.json.gz", (await store.LoadAsync(room, default))!);
                await SaveRows("original-final-seven-tables.json.gz"); await CorruptAndRefuse();
            }
            else
            {
                if (branch == "recycle")
                {
                    var captured = state.StackItems.Single().DravenBattleDeathScore!;
                    if (state.PriorityPlayerId == "alice") await Submit("alice", new PassPriorityCommand());
                    await Submit("bob", new PlayCardCommand("DISPOSAL", "UNL-103/219", ["P1-UNIT"], "RECYCLE_OPPONENT_GRAVEYARD_UP_TO_3"));
                    await Top();
                    Check(state.PlayerZones["alice"].MainDeck.Contains("P1-UNIT") && state.CardObjects["P1-UNIT"].ObjectGeneration > captured.Source.Generation
                        && state.StackItems.Single().DravenBattleDeathScore == captured, "Recycling creates a new private incarnation without rewriting the earned death skill.");
                    var view = JsonSerializer.SerializeToElement(session.SnapshotFor("bob"), Json);
                    Check(view.GetProperty("stack").EnumerateArray().Single().GetProperty("sourceObjectId").GetString() == "HIDDEN", "New private source identity remains hidden.");
                }
                for (var i = 0; state.Status == MatchStatuses.InProgress && (state.PendingCardChoice is not null || state.TriggerQueue.Count > 0 || state.StackItems.Count > 0); i++)
                {
                    Check(i < 14, "Bounded death/result skills must finish.");
                    if (state.PendingCardChoice is not null) await Confirm();
                    else if (state.TriggerQueue.Count > 0)
                    {
                        var orderers = tokens.Keys.Where(p => session.PromptFor(p).Candidates?.Any(c => c.Enabled && c.Action == CommandTypes.OrderTriggers) == true).ToArray();
                        Check(orderers.Length == 1, "Exactly one controller may order the next simultaneous trigger group.");
                        var prompt = JsonSerializer.SerializeToElement(session.PromptFor(orderers[0]), Json);
                        var candidate = prompt.GetProperty("candidates").EnumerateArray().Single(c =>
                            c.GetProperty("action").GetString() == CommandTypes.OrderTriggers && c.GetProperty("enabled").GetBoolean());
                        var ids = candidate.GetProperty("metadata").GetProperty("orderedTriggerIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
                        await Submit(orderers[0], new OrderTriggersCommand(OrderedTriggerIds: ids));
                    }
                    else await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
                }
                var events = journal.Entries.SelectMany(e => e.Events).ToArray();
                var scores = events.Where(e => e.Kind == "SCORE_GAINED" && Equals(e.Payload.GetValueOrDefault("reason"), Effect)).ToArray();
                var count = branch == "altar-pay" ? 0 : mutual && !winning ? 2 : 1;
                Check(scores.Length == count && (count == 0 || Equals(scores[0].Payload["playerId"], mutual || mixed ? "alice" : "bob")),
                    "Literal opponent recipient and simultaneous-death LIFO score order.");
                if (winning) Check(state.WinnerPlayerId == (mutual ? "alice" : "bob") && state.PendingBattleResult is not null
                    && !events.Any(e => e.Kind is "BATTLEFIELD_CONQUERED" or "BATTLE_CLOSED"), "Normal scoring victory halts before later skills, control and close.");
                else Check(state.WinnerPlayerId is null && state.PendingBattleResult is null && events.Count(e => e.Kind == "BATTLE_CLOSED") == 1,
                    "Nonwinning result continues through actual control and one battle close.");
                if (branch == "altar-pay") Check(state.PlayerZones["alice"].Base.Contains("P1-UNIT")
                    && !events.Any(e => e.Kind == "TRIGGER_QUEUED" && Equals(e.Payload.GetValueOrDefault("effectKind"), Effect)), "Recall replacement does not create a death skill.");
                if (mixed)
                {
                    await Save("original-before-order-check-frame.json.gz", (await store.LoadAsync(room, default))!);
                    await SaveRows("original-before-order-check-seven-tables.json.gz");
                    var deathScoreIndex = Array.IndexOf(events, scores[0]);
                    var resultIndex = Array.IndexOf(events, events.Single(e => e.Kind == "BATTLE_RESULT_DETERMINED"));
                    var drawIndex = Array.IndexOf(events, events.Single(e => e.Kind == "CARD_DRAWN"));
                    var conquestIndex = Array.IndexOf(events, events.Single(e => e.Kind == "BATTLEFIELD_CONQUERED"));
                    var closeIndex = Array.IndexOf(events, events.Single(e => e.Kind == "BATTLE_CLOSED"));
                    Check(deathScoreIndex < resultIndex && resultIndex < drawIndex && drawIndex < conquestIndex && conquestIndex < closeIndex,
                        "CN468.2 completes death skills before the result; result skills complete before control, conquest and battle close.");
                }
                await Save("original-final-frame.json.gz", (await store.LoadAsync(room, default))!); await SaveRows("original-final-seven-tables.json.gz");
            }
            Check(cold == journal.Entries.Count, "Every accepted prefix independently replayed and cold restored.");
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_DRAVEN_BATTLE_DEATH_SCOPED", room, branch, scenario,
                originalAcceptedInputs = journal.Entries.Count, coldRecoveries = cold,
                structurallyConsistentOriginalJournalForgeryRejected = forge, actualRuleInputs = RuleInputIdentity.Current,
                originalPrivateSeed = original.OpeningSeed, controlledPositionNotOrdinaryOpening = true,
                noAcceptedHistoryRewrite = true, wholeCardCertification = false }, Json));
            Console.WriteLine($"DRAVEN_BATTLE_DEATH_SQL_PASS {branch} inputs={journal.Entries.Count} cold={cold}");

            async Task Confirm()
            {
                var c = state.PendingCardChoice!;
                await Submit(c.PlayerId, new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, [c.PlayerId == "alice" ? "bob" : "alice"]));
            }
            async Task Top()
            {
                var id = state.StackItems.Last().StackItemId;
                for (var i = 0; state.StackItems.Any(s => s.StackItemId == id); i++)
                { Check(i < 4, "A reaction parent must finish in this bounded position."); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            }
            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var payload = JsonSerializer.SerializeToElement(raw, Json);
                var result = await session.SubmitAsync(actor, prefix, GameCommandJsonMapper.Map(payload), payload, default);
                Check(result.Accepted, result.ErrorMessage ?? "An original battle-result-continuation intent was rejected."); await Cold();
            }
            async Task Cold()
            {
                var frame = (await store.LoadAsync(room, default))!;
                Check(frame.IsConsistent && frame.AuthoritativeState!.RuleInputs == RuleInputIdentity.Current,
                    "Actual SQL rows and loaded inputs must agree: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0,
                    "The entire original prefix must independently replay before every cold restore.");
                await Save($"prefix-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray()) tokens[actor] = (await session.ReconnectPlayerAsync(actor, tokens[actor], default)).ReconnectToken;
                state = frame.AuthoritativeState!; cold++;
            }
            async Task CorruptAndRefuse()
            {
                var item = state.StackItems.Single(); var context = item.DravenBattleDeathScore!;
                if (branch == "forge-generation") context = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } };
                else if (branch == "forge-printing") { context = context with { CardNo = "SFD·148a/221" }; item = item with { CardNo = context.CardNo }; }
                else context = context with { Role = "DEFENDER" };
                var forged = state with { StackItems = [item with { DravenBattleDeathScore = context }] };
                await using (var connection = await source.OpenConnectionAsync())
                await using (var transaction = await connection.BeginTransactionAsync())
                {
                    await Write("state_snapshots", "state_tick", null, forged);
                    foreach (var v in ResolutionResult.BuildSnapshots(forged)) await Write("snapshots", "snapshot_tick", v.Key, v.Value);
                    foreach (var v in ResolutionResult.BuildPrompts(forged)) await Write("action_prompts", "prompt_tick", v.Key, v.Value);
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
                Check(corrupt.IsConsistent, "Coordinated projected views must pass structural checks before original-history rejection: " + string.Join("; ", corrupt.ValidationErrors));
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Original trigger birth or result event must reject coordinated history forgery.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "A new registry must refuse this actual corrupted SQL room.");
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
