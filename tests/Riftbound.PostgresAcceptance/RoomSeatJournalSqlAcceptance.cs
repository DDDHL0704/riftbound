using System.IO.Compression;
using System.Text.Json;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class RoomSeatJournalSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Fresh room-seat SQL output required.");
        Directory.CreateDirectory(output);
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
        foreach (var reverse in new[] { false, true })
        foreach (var late in new[] { false, true }) await Run(reverse, late);
        foreach (var atomicFirst in new[] { true, false }) await RunAtomicity(atomicFirst);
        await RejectNonAtomicDurableJournal();
        await RejectMetadataOnlyCurrentRoom();

        async Task RunAtomicity(bool atomicFirst)
        {
            var folder = Path.Combine(output, $"atomic-first-{atomicFirst}"); Directory.CreateDirectory(folder);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var room = "room-seat-atomic-" + Guid.NewGuid().ToString("N");
            var constraint = "qa_room_seat_" + Guid.NewGuid().ToString("N");
            var engine = new CoreRuleEngine(); var journal = new CapturingJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, engine, journal, players);
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal); var cold = 0;
            if (!atomicFirst) await Join("seat-a");
            var before = await Rows();
            await File.WriteAllTextAsync(Path.Combine(folder, "original-seven-tables-before-controlled-failure.json"), JsonSerializer.Serialize(before, json));
            var actor = atomicFirst ? "seat-a" : "seat-b";
            await File.WriteAllTextAsync(Path.Combine(folder, "original-failing-join-input.json"), JsonSerializer.Serialize(new { room, actor, operation = "EnsurePlayerAsync", atomicFirst, constraint }, json));
            var rejected = false;
            await Execute($"alter table command_log add constraint {constraint} check (match_id <> '{room}') not valid");
            try
            {
                try { await session.EnsurePlayerAsync(actor, default); }
                catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.CheckViolation) { rejected = true; }
            }
            finally { await Execute($"alter table command_log drop constraint {constraint}"); }
            Check(rejected, "The actual journal SQL insert must fail inside the shared player/journal transaction.");
            var after = await Rows();
            await File.WriteAllTextAsync(Path.Combine(folder, "original-seven-tables-after-transaction-rollback.json"), JsonSerializer.Serialize(after, json));
            Check(JsonSerializer.Serialize(before, json) == JsonSerializer.Serialize(after, json), "Failed first/second seat changed original metadata or any accepted history.");
            var absent = false;
            try { session.SnapshotFor(actor); }
            catch (MatchSessionException) { absent = true; }
            Check(absent, "Failed durable registration published an in-memory seat.");
            var failedInput = Path.Combine(folder, $"entry-{journal.Entries.Count + 1:D2}-original-input-before-persist.json.gz");
            File.Move(failedInput, Path.Combine(folder, "original-failed-seat-input-before-transaction-rollback.json.gz"));
            var rollbackFrame = await store.LoadAsync(room, default);
            if (atomicFirst)
                Check(rollbackFrame is null && after.Values.All(rows => rows.Length == 0), "First-seat failure left a ghost room, credential, or implicit recovered seat.");
            else
            {
                Check(rollbackFrame is not null && rollbackFrame.IsConsistent && rollbackFrame.Commands.Count == 1
                    && rollbackFrame.AuthoritativeState!.Seats.Count == 1 && rollbackFrame.AuthoritativeState.Seats["seat-a"] == "P1",
                    "Second-seat failure must retain the exact first registered player and original journal.");
                await Save("original-after-rollback-frame.json.gz", rollbackFrame!);
                await Cold();
            }
            await Join(actor);
            if (atomicFirst) await Join("seat-b");
            var final = await store.LoadAsync(room, default) ?? throw new Exception("No retried original room.");
            Check(journal.Entries.Count == 2 && journal.Attempts.Count == 3 && journal.Attempts.Select(e => e.OpeningSeed).Distinct().Count() == 1
                && final.Commands.Count == 2 && final.Commands.All(c => c.Accepted && c.CommandType == "ROOM_SEAT")
                && final.AuthoritativeState!.Seats["seat-a"] == "P1" && final.AuthoritativeState.Seats["seat-b"] == "P2",
                "Original failed input, seed, accepted first seat and same next slot must survive retry without rewriting a command.");
            await Save("original-final-frame.json.gz", final);
            await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                status = "PASS_ACTUAL_FIRST_OR_SECOND_SEAT_JOURNAL_SQL_FAILURE_ATOMIC_ROLLBACK_COLD_RETRY_AND_ALL_ORIGINAL_PREFIX_REPLAYS",
                room, atomicFirst, commands = 2, events = final.Events.Count, coldRecoveries = cold,
                originalSeed = final.OpeningSeed, actualRuleInputs = RuleInputIdentity.Current,
                actualSqlCheckViolation = true, originalSevenTableRowsExactAfterRollback = true,
                firstFailureLeavesNoGhostRoomOrSeat = atomicFirst, originalUnacceptedAttemptRetained = true,
                temporarySqlConstraintRemoved = true, allOriginalAcceptedInputsAndSeedsRetained = true,
                oldGamesMigratedOrRewritten = false, currentNativeVisibleWindowsOrWholeProductAcceptance = false }, json));

            async Task Join(string player)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, $"input-{journal.Entries.Count + 1:D2}-join.json"), JsonSerializer.Serialize(new { actor = player, operation = "EnsurePlayerAsync" }, json));
                tokens[player] = (await session.EnsurePlayerAsync(player, default)).ReconnectToken;
                var frame = await store.LoadAsync(room, default) ?? throw new Exception("Original accepted seat frame missing.");
                Check(frame.IsConsistent && frame.Commands.Count == journal.Entries.Count, string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Retried original prefix replay differs.");
                await Save($"history-{journal.Entries.Count:D2}-original-frame.json.gz", frame);
                await Cold();
            }
            async Task Cold()
            {
                var frame = await store.LoadAsync(room, default) ?? throw new Exception("Missing original cold input.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                foreach (var player in tokens.Keys.ToArray())
                {
                    tokens[player] = (await session.ReconnectPlayerAsync(player, tokens[player], default)).ReconnectToken;
                    Check(MatchStateHasher.HashValue(session.SnapshotFor(player)) == MatchStateHasher.HashValue(frame.PlayerViews[player].Snapshot)
                        && MatchStateHasher.HashValue(session.PromptFor(player)) == MatchStateHasher.HashValue(frame.PlayerViews[player].Prompt),
                        "Original private view changed on actual cold authentication.");
                }
                cold++;
            }
            async Task Save(string name, MatchRecoveryFrame frame)
            {
                await using var file = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(gzip, frame, json);
            }
            async Task Execute(string sql)
            {
                await using var connection = await source.OpenConnectionAsync();
                await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
            }
            async Task<Dictionary<string, string[]>> Rows()
            {
                var value = new Dictionary<string, string[]>();
                await using var connection = await source.OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await new NpgsqlCommand("set transaction read only", connection, transaction).ExecuteNonQueryAsync();
                Check((string)(await new NpgsqlCommand("show transaction_read_only", connection, transaction).ExecuteScalarAsync())! == "on", "Actual rollback readback must be read only.");
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    var rows = new List<string>();
                    await using var q = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection, transaction);
                    q.Parameters.AddWithValue("room", room);
                    await using var reader = await q.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) rows.Add(reader.GetString(0));
                    value[table] = rows.ToArray();
                }
                await transaction.CommitAsync(); return value;
            }
        }

        async Task RejectNonAtomicDurableJournal()
        {
            var room = "room-seat-nonatomic-rejected-" + Guid.NewGuid().ToString("N");
            var journal = new NonAtomicJournalMustNotRun();
            var session = new MatchSession(room, new CoreRuleEngine(), journal, new PostgresMatchPlayerStore(source));
            var rejected = false;
            try { await session.EnsurePlayerAsync("seat-a", default); }
            catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejected = true; }
            Check(rejected && !journal.Called && await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) is null,
                "Unsupported durable journal composition must fail before writing a player or room.");
            await using var connection = await source.OpenConnectionAsync();
            var counts = new Dictionary<string, long>();
            foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            {
                await using var q = new NpgsqlCommand($"select count(*) from {table} where match_id=@room", connection);
                q.Parameters.AddWithValue("room", room); counts[table] = (long)(await q.ExecuteScalarAsync())!;
            }
            Check(counts.Values.All(n => n == 0), "Rejected non-atomic composition left durable room rows.");
            await File.WriteAllTextAsync(Path.Combine(output, "unsupported-nonatomic-journal-rejection.json"), JsonSerializer.Serialize(new {
                room, status = "PASS_NONATOMIC_DURABLE_JOURNAL_REJECTED_BEFORE_ANY_WRITE", journalCalled = journal.Called,
                errorCode = ErrorCodes.RecoveryInconsistent, actualSevenTableCounts = counts, actualRuleInputs = RuleInputIdentity.Current }));
        }

        async Task RejectMetadataOnlyCurrentRoom()
        {
            var room = "room-seat-metadata-only-rejected-" + Guid.NewGuid().ToString("N");
            const long seed = 424242;
            var players = new PostgresMatchPlayerStore(source);
            await players.SavePlayerSessionAsync(room, "seat-a", "P1",
                ReconnectTokenHasher.Hash(Guid.NewGuid().ToString("N")), default, seed);
            var before = await Rows();
            var store = new PostgresMatchRecoveryStore(source);
            var frame = await store.LoadAsync(room, default) ?? throw new Exception("Controlled metadata-only room missing.");
            Check(!frame.IsConsistent && frame.AuthoritativeState is null && frame.Commands.Count == 0
                && frame.OpeningSeed == seed && frame.ValidationErrors.Any(e => e.Contains("requires authoritative state", StringComparison.Ordinal)),
                "Matching current identity/seed and player metadata must not invent an unrecorded seat.");
            var rejected = false;
            try { await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), new PostgresMatchJournal(source), store, players).GetOrCreateAsync(room, default); }
            catch (MatchSessionException error) when (error.Code == ErrorCodes.RecoveryInconsistent) { rejected = true; }
            var after = await Rows();
            Check(rejected && before.SequenceEqual(after), "Cold rejection must preserve every original metadata row and must not synthesize accepted history.");
            await File.WriteAllTextAsync(Path.Combine(output, "metadata-only-current-room-rejection.json"), JsonSerializer.Serialize(new {
                room, status = "PASS_CURRENT_IDENTITY_METADATA_WITHOUT_ORIGINAL_ROOM_JOURNAL_REJECTED_NOT_MIGRATED",
                originalSeed = seed, actualRuleInputs = RuleInputIdentity.Current, validationErrors = frame.ValidationErrors,
                coldErrorCode = ErrorCodes.RecoveryInconsistent, originalSqlRowsBefore = before, originalSqlRowsAfter = after,
                originalMetadataRowsRetained = true, acceptedHistoryOrOldGameRewritten = false }));

            async Task<string[]> Rows()
            {
                var rows = new List<string>();
                await using var connection = await source.OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                await new NpgsqlCommand("set transaction read only", connection, transaction).ExecuteNonQueryAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    await using var q = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection, transaction);
                    q.Parameters.AddWithValue("room", room);
                    await using var reader = await q.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) rows.Add(table + ":" + reader.GetString(0));
                }
                await transaction.CommitAsync(); return rows.ToArray();
            }
        }

        async Task Run(bool reverse, bool late)
        {
            var folder = Path.Combine(output, $"reverse-{reverse}-late-{late}"); Directory.CreateDirectory(folder);
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
            var room = "room-seat-journal-" + Guid.NewGuid().ToString("N");
            var first = reverse ? "seat-b" : "seat-a"; var second = reverse ? "seat-a" : "seat-b";
            var engine = new CoreRuleEngine(); var journal = new CapturingJournal(new PostgresMatchJournal(source), folder);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(room, engine, journal, players);
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            MatchRecoveryFrame? current = null; var commands = 0; var cold = 0; var rejections = 0; var structural = 0; var warm = 0;
            var originals = new List<object>();
            try
            {
                await Join(first);
                if (late) await Deck(first);
                var beforeForgery = await store.LoadAsync(room, default) ?? throw new Exception("Original pre-game frame missing.");
                var forgedRaw = JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = "intruder", seat = "P2" }, json);
                await File.WriteAllTextAsync(Path.Combine(folder, "input-client-forgery-before-game.json"), JsonSerializer.Serialize(new { actor = first, intent = "client-before-second-seat", raw = forgedRaw }, json));
                var rejected = false;
                try { await session.SubmitAsync(first, "client-before-second-seat", GameCommandJsonMapper.Map(forgedRaw), forgedRaw, default); }
                catch (MatchSessionException) { rejected = true; }
                Check(rejected, "Pre-game client input cannot install a server seat.");
                var afterForgery = await store.LoadAsync(room, default) ?? throw new Exception("Original frame disappeared.");
                Check(MatchStateHasher.HashValue(beforeForgery) == MatchStateHasher.HashValue(afterForgery), "Pre-game rejection changed original history."); rejections++;
                await Join(second);
                if (!late) await Deck(first);
                await Deck(second);
                await Submit(first, "ready-first", new ReadyCommand());
                await Submit(second, "ready-second", new ReadyCommand());
                Check(current!.AuthoritativeState!.Phase == MatchPhases.Mulligan, "Original legal opening must reach mulligan.");
                var openingFirst = current.AuthoritativeState.ActivePlayerId;
                var openingSecond = current.AuthoritativeState.OpeningSecondActionPlayerId!;
                await Submit(openingFirst, "keep-first", new MulliganCommand([]));
                await Submit(openingSecond, "keep-second", new MulliganCommand([]));
                var before = current!.AuthoritativeState!;
                await File.WriteAllTextAsync(Path.Combine(folder, "input-client-forgery-after-start.json"), JsonSerializer.Serialize(new { actor = first, intent = "client-forged-seat", raw = forgedRaw }, json));
                var failed = await session.SubmitAsync(first, "client-forged-seat", GameCommandJsonMapper.Map(forgedRaw), forgedRaw, default);
                Check(!failed.Accepted && failed.Events.Count == 0 && MatchStateHasher.Hash(failed.State) == MatchStateHasher.Hash(before), "Client forged room seat was not an atomic rejection.");
                await Capture("client-forged-seat-rejected");
                Check(commands == 9 && current!.Commands.Count(c => c.Accepted && c.CommandType == "ROOM_SEAT") == 2
                    && current.Commands.Count(c => !c.Accepted && c.CommandType == "ROOM_SEAT") == 1, "Two original server joins and one retained client rejection required.");
                Check(current.AuthoritativeState!.Seats[first] == "P1" && current.AuthoritativeState.Seats[second] == "P2", "Original assignment differs from actual join order.");
                Check(current.AuthoritativeState.Seats.Count == 2 && !current.AuthoritativeState.Seats.ContainsKey("intruder"), "Client changed room membership.");
                Check(current.ReplayInitialState!.Status == MatchStatuses.Empty && current.ReplayInitialState.Seats.Count == 0, "Original replay origin must be the actual empty room.");
                await Save("original-final-frame.json.gz", current);
                await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new {
                    status = "PASS_ORIGINAL_ROOM_SEATS_FOUR_INTERLEAVINGS_SQL_COLD_REPLAY_AND_INDEPENDENT_GUARDS", room, reverse, late,
                    first, second, commands, events = current.Events.Count, coldRecoveries = cold,
                    preGameDirectAtomicRejections = rejections, independentStructuralRejections = structural, noOpKnownPlayerJoins = warm,
                    actualRuleInputs = RuleInputIdentity.Current, originalSeed = current.OpeningSeed, originals,
                    originalClientRawCommandsNotRewritten = true, allOriginalServerJoinEventsAndHistoriesRetained = true,
                    oldGamesMigratedOrRewritten = false, currentNativeVisibleWindowsOrWholeProductAcceptance = false }, json));
            }
            catch (Exception originalFailure)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, "original-failure.txt"), originalFailure.ToString());
                try
                {
                    var frame = await store.LoadAsync(room, default);
                    if (frame is not null) await Save("original-partial-frame-before-failure.json.gz", frame);
                }
                catch (Exception readFailure)
                { await File.WriteAllTextAsync(Path.Combine(folder, "subsequent-read-failure.txt"), readFailure.ToString()); }
                await File.WriteAllTextAsync(Path.Combine(folder, "partial-receipt.json"), JsonSerializer.Serialize(new { room, reverse, late, commands, cold, structural, originals, originalHistoryRetained = true }, json));
                throw;
            }

            async Task Join(string actor)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, $"input-{commands + 1:D2}-join.json"), JsonSerializer.Serialize(new { serverOperation = "EnsurePlayerAsync", actor }, json));
                var joined = await session.EnsurePlayerAsync(actor, default); tokens.Add(actor, joined.ReconnectToken);
                await Capture("server-seat-" + actor);
            }
            async Task Deck(string actor) => await Submit(actor, "deck", new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
            async Task Submit(string actor, string intent, GameCommand command)
            {
                var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
                await File.WriteAllTextAsync(Path.Combine(folder, $"input-{commands + 1:D2}-client.json"), JsonSerializer.Serialize(new { actor, intent, raw }, json));
                var result = command switch {
                    SubmitDeckCommand submit => await session.SubmitDeckAsync(actor, intent, submit, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                    _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage ?? "Original command rejected."); await Capture(intent + "-" + actor);
            }
            async Task Capture(string label)
            {
                current = await store.LoadAsync(room, default) ?? throw new Exception("Original accepted frame missing.");
                commands++; await Save($"history-{commands:D2}-original-frame.json.gz", current);
                Check(current.Commands.Count == commands && journal.Entries.Count == commands && current.OpeningSeed != 0, "Original command/seed identity changed.");
                Check(current.ValidationErrors.Count == 0, string.Join("; ", current.ValidationErrors));
                var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(current, engine, default);
                Check(errors.Count == 0, string.Join("; ", errors));
                var originalSeat = current.Commands.First(c => c.Accepted && c.CommandType == "ROOM_SEAT");
                foreach (var branch in new[] { "actor", "slot", "extra", "missing-command" })
                {
                    var replacement = branch switch {
                        "actor" => originalSeat with { RawCommand = JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = "intruder", seat = "P1" }, json) },
                        "slot" => originalSeat with { RawCommand = JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = first, seat = "P2" }, json), ClientIntentId = "room-seat:P2" },
                        "extra" => originalSeat with { RawCommand = JsonSerializer.SerializeToElement(new { cmdType = "ROOM_SEAT", playerId = first, seat = "P1", extra = true }, json) },
                        _ => originalSeat };
                    var bad = current with { Commands = branch == "missing-command" ? current.Commands.Skip(1).ToArray()
                        : current.Commands.Select(c => ReferenceEquals(c, originalSeat) ? replacement : c).ToArray() };
                    var rejected = MatchRecoveryValidator.Validate(room, bad.LastEventSequence, bad.Commands, bad.Events, bad.PlayerViews, bad.AuthoritativeState, bad.CurrentTick);
                    Check(rejected.Any(e => e.Contains("room seat", StringComparison.Ordinal)), "Independent room input guard accepted a forged " + branch); structural++;
                }
                originals.Add(new { index = commands, label, command = current.Commands[^1], finalTick = current.CurrentTick, finalEventSequence = current.LastEventSequence,
                    actualSeats = current.AuthoritativeState!.Seats, actualStateHash = MatchStateHasher.Hash(current.AuthoritativeState), replayInitialHash = MatchStateHasher.Hash(current.ReplayInitialState!) });
                var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                session = await registry.GetOrCreateAsync(room, default);
                foreach (var actor in tokens.Keys.ToArray())
                {
                    var reconnected = await session.ReconnectPlayerAsync(actor, tokens[actor], default); tokens[actor] = reconnected.ReconnectToken;
                    var expected = current.PlayerViews[actor];
                    Check(MatchStateHasher.HashValue(session.SnapshotFor(actor)) == MatchStateHasher.HashValue(expected.Snapshot), "Actual cold private snapshot differs.");
                    Check(MatchStateHasher.HashValue(session.PromptFor(actor)) == MatchStateHasher.HashValue(expected.Prompt), "Actual cold private prompt differs.");
                }
                cold++;
                await session.EnsurePlayerAsync(tokens.Keys.First(), default); warm++;
                var unchanged = await store.LoadAsync(room, default) ?? throw new Exception("Frame disappeared after reconnect.");
                Check(MatchStateHasher.HashValue(current) == MatchStateHasher.HashValue(unchanged), "Cold authentication or known join changed original game history.");
            }
            async Task Save(string name, MatchRecoveryFrame frame)
            {
                await using var stream = new FileStream(Path.Combine(folder, name), FileMode.CreateNew);
                await using var gzip = new GZipStream(stream, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(gzip, frame, json);
            }
        }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private sealed class NonAtomicJournalMustNotRun : IMatchJournal
    {
        public bool Called { get; private set; }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            Called = true;
            throw new InvalidOperationException("Non-atomic durable journal must be rejected before reaching a write.");
        }
    }

    private sealed class CapturingJournal(IMatchJournal inner, string folder) : IMatchRoomSeatJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public List<MatchJournalEntry> Attempts { get; } = [];
        public async ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            await CaptureOriginal(entry, cancellationToken);
            await inner.RecordAsync(entry, cancellationToken);
            Entries.Add(entry);
        }
        public async ValueTask RecordRoomSeatAsync(MatchJournalEntry entry, string reconnectTokenHash,
            CancellationToken cancellationToken)
        {
            await CaptureOriginal(entry, cancellationToken);
            await ((IMatchRoomSeatJournal)inner).RecordRoomSeatAsync(entry, reconnectTokenHash, cancellationToken);
            Entries.Add(entry);
        }
        private async Task CaptureOriginal(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            var name = Path.Combine(folder, $"entry-{Entries.Count + 1:D2}-original-input-before-persist.json.gz");
            await using (var file = new FileStream(name, FileMode.CreateNew))
            await using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
                await JsonSerializer.SerializeAsync(gzip, entry, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }, cancellationToken);
            Attempts.Add(entry);
        }
    }
}
