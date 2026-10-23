using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class BurnoutActionCompletionSqlAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Tables = ["matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts"];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh burnout-action SQL evidence is required."); Directory.CreateDirectory(output);
        foreach (var branch in new[] { "conquest", "draw-two", "second-burnout", "hwei", "portal-tie", "reject-forged-draw" })
            await Run(branch);

        async Task Run(string branch)
        {
            var folder = Path.Combine(output, branch); Directory.CreateDirectory(folder);
            var room = "burnout-action-completion-" + Guid.NewGuid().ToString("N");
            var journal = new OriginalJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, new CoreRuleEngine(), journal, players);
            var tokens = new Dictionary<string, string>(); MatchState state = null!; var cold = 0;
            tokens["alice"] = (await session.EnsurePlayerAsync("alice", default)).ReconnectToken; await Cold();
            tokens["bob"] = (await session.EnsurePlayerAsync("bob", default)).ReconnectToken; await Cold();
            var scenario = "burnout-action-" + (branch == "reject-forged-draw" ? "conquest" : branch);
            var setup = await session.SeedScenarioAsync("alice", "original-controlled-setup", scenario,
                JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
            Check(setup.Accepted, "The declared development setup must be an original recorded command."); await Cold();
            var original = (await store.LoadAsync(room, default))!;
            Check(original.OpeningSeed != 0 && original.ReplayInitialState is { Tick: 0, Status: "EMPTY", Seats.Count: 0 }
                && original.Commands.Count == 3 && original.Commands.Take(2).All(c => c.CommandType == "ROOM_SEAT"),
                "Both original atomic seats, private seed, explicit development input and EMPTY origin are retained.");
            if (branch is "conquest" or "reject-forged-draw")
            {
                await Submit("alice", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
                await Submit("alice", new PassFocusCommand()); await Submit("bob", new PassFocusCommand());
                Check(state.PlayerZones["alice"].Hand.Count == 1 && state.PlayerZones["alice"].MainDeck.Count == 2
                    && state.PlayerZones["alice"].Graveyard.Count == 0 && state.PlayerScores["alice"] == 7
                    && state.RngCursor == 1 && state.CardObjects["BF"].ControllerId == "alice",
                    "The first recyclable final-conquest burnout must actually finish its original draw and RNG transition.");
            }
            else if (branch == "hwei")
            {
                await Submit("alice", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F"));
                await Top();
                var choice = state.PendingHandChoice;
                Check(choice is { RequiredCount: 1, DrawCount: 0 } && state.Status == MatchStatuses.InProgress
                    && state.WinnerPlayerId is null && state.PlayerScores["bob"] == 8
                    && state.PlayerZones["alice"].Hand.Count == 2
                    && journal.Entries.SelectMany(e => e.Events).All(e => e.Kind != "MATCH_WON"),
                    "Score8 cannot end this parent before its mandatory private discard.");
                var drawn = state.PlayerZones["alice"].Hand.Single(id => id != "HAND");
                Check(choice!.LegalObjectIds.Contains(drawn), "The real just-drawn card is selectable after SQL cold recovery.");
                await Submit("alice", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [drawn]));
                Check(state.PlayerZones["alice"].Hand.SequenceEqual(["HAND"])
                    && state.PlayerZones["alice"].Graveyard.Contains(drawn) && state.PendingHandChoice is null,
                    "The required discard completes physically before normal victory.");
            }
            else
            {
                var portal = branch == "portal-tie";
                // This is the already implemented, preselected CARDS branch only.
                // It does not certify the printed card's mode-selection ownership.
                await Submit("alice", new PlayCardCommand("HAND", portal ? "OGN·071/298" : "OGN·083/298", [],
                    portal ? "OTHER_PLAYERS_CHOOSE_CARDS" : ""));
                await Top();
                Check(state.PlayerZones["alice"].Hand.Count == (branch == "second-burnout" ? 1 : portal ? 1 : 2)
                    && state.PlayerZones["alice"].MainDeck.Count == (branch == "draw-two" ? 1 : 0),
                    "The actual one printed draw instruction retains its completed or partially completed cards.");
                if (portal) Check(state.PlayerScores["alice"] == 8 && state.PlayerScores["bob"] == 8
                    && state.PlayerZones["bob"].Hand.SequenceEqual(["P2-REFILL"]),
                    "Both first burnouts finish within this parent and can create a score tie.");
            }
            var allEvents = journal.Entries.SelectMany(e => e.Events).ToArray();
            var tie = branch == "portal-tie";
            Check(state.PlayerScores["bob"] == (branch == "second-burnout" ? 9 : 8)
                && state.Status == (tie ? MatchStatuses.InProgress : MatchStatuses.Finished)
                && state.WinnerPlayerId == (tie ? null : "bob"),
                "Independent literal scores distinguish normal cleanup from subsequent immediate victory.");
            Check(allEvents.Count(e => e.Kind == "BURNOUT_APPLIED") == (branch is "second-burnout" or "portal-tie" ? 2 : 1)
                && allEvents.Count(e => e.Kind == "MATCH_WON") == (tie ? 0 : 1),
                "No premature or duplicate victory event may survive the original journal.");
            var drawEvents = allEvents.Where(e => e.Kind == "CARD_DRAWN").ToArray();
            Check(drawEvents.Length == (tie ? 2 : 1)
                && drawEvents.All(e => Equals(e.Payload.GetValueOrDefault("count"), branch == "draw-two" ? 2 : 1))
                && drawEvents.Select(e => e.Payload.GetValueOrDefault("playerId")).SequenceEqual(tie ? ["alice", "bob"] : ["alice"]),
                "Every actual draw event retains the exact completed count and recipient; draw2 is one event with count2.");
            if (!tie)
            {
                var kinds = allEvents.Select(e => e.Kind).ToList();
                Check(kinds.IndexOf("CARD_DRAWN") < kinds.IndexOf("MATCH_WON"), "Actual partial/completed drawing precedes the victory event.");
                if (branch == "hwei") Check(kinds.Count(k => k == "HAND_CHOICE_RESOLVED") == 1
                    && kinds.IndexOf("HAND_CHOICE_RESOLVED") < kinds.IndexOf("MATCH_WON"),
                    "Mandatory discard confirmation precedes ordinary score cleanup.");
            }
            async Task Top()
            {
                var top = state.StackItems.Last().StackItemId;
                for (var step = 0; step < 8 && state.StackItems.Any(i => i.StackItemId == top); step++)
                    await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
                Check(!state.StackItems.Any(i => i.StackItemId == top), "The finite original top parent must complete or suspend into its required hand choice.");
            }
            var final = (await store.LoadAsync(room, default))!;
            Check(final.OpeningSeed == original.OpeningSeed
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(final.ReplayInitialState, Json), JsonSerializer.SerializeToNode(original.ReplayInitialState, Json))
                && final.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue).SequenceEqual(original.Commands.Select(MatchStateHasher.HashValue)),
                "No original setup, accepted history or private opening is rewritten.");
            await Save("original-final-frame.json.gz", final); await SaveRows("original-final-seven-tables.json.gz");
            if (branch == "reject-forged-draw") await CorruptAndRefuse();
            Check(cold == journal.Entries.Count, "Every accepted original SQL input has a real complete cold recovery.");
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "REAL_SQL_BURNOUT_ACTION_COMPLETION_SCOPED", room, branch, scenario, originalAcceptedInputs = journal.Entries.Count,
                coldRecoveries = cold, deliberateCoordinatedForgeryRejected = branch == "reject-forged-draw",
                originalPrivateSeed = original.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                controlledPositionNotOrdinaryOpening = true, noAcceptedHistoryRewrite = true, wholeCardOrNativeVisibleCertification = false }, Json));
            Console.WriteLine($"BURNOUT_ACTION_COMPLETION_SQL_PASS {branch} inputs={journal.Entries.Count} cold={cold}");

            async Task Submit(string actor, GameCommand command)
            {
                var prefix = "input-" + journal.Entries.Count.ToString("D2"); var prompt = session.PromptFor(actor);
                var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject(); raw["promptId"] = prompt.PromptId; raw["snapshotTick"] = state.Tick;
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-prompt.json"), JsonSerializer.Serialize(prompt, Json));
                await File.WriteAllTextAsync(Path.Combine(folder, prefix + "-original-command.json"), raw.ToJsonString());
                var result = await session.SubmitAsync(actor, prefix, command, JsonSerializer.SerializeToElement(raw, Json), default);
                Check(result.Accepted, result.ErrorMessage ?? "An original burnout-action intent was rejected."); await Cold();
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
                var drawn = state.PlayerZones["alice"].Hand.Single();
                var forged = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["alice"] = state.PlayerZones["alice"] with { Hand = [],
                        MainDeck = state.PlayerZones["alice"].MainDeck.Prepend(drawn).ToArray() } },
                    ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                        [drawn] = new("alice", "MAIN_DECK") } };
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
                        Check(await command.ExecuteNonQueryAsync() == 1, "Only one actual new room's latest original view may be deliberately corrupted.");
                    }
                }
                var corrupt = (await store.LoadAsync(room, default))!; await Save("actual-corrupt-frame.json.gz", corrupt); await SaveRows("actual-corrupt-seven-tables.json.gz");
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(corrupt, new CoreRuleEngine(), default);
                Check(errors.Count > 0, "Coordinated skipped-draw, physical-location and private-view forgery must fail against the unchanged original action log.");
                var refused = false;
                try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), journal, store, players).GetOrCreateAsync(room, default); }
                catch (MatchSessionException e) when (e.Code == ErrorCodes.RecoveryInconsistent) { refused = true; }
                Check(refused, "A fresh registry must refuse this actual corrupted database room.");
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
