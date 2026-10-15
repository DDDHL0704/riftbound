using System.Text.Json;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound")
    ?? throw new InvalidOperationException("A real dedicated PostgreSQL connection is required; this gate cannot return without SQL.");
var settings = new NpgsqlConnectionStringBuilder(connectionString);
if (settings.Database != "riftbound_acceptance" || settings.Host is not ("127.0.0.1" or "localhost"))
    throw new InvalidOperationException("Use the dedicated loopback riftbound_acceptance database; no shared application database is allowed.");
var output = Environment.GetEnvironmentVariable("RIFTBOUND_POSTGRES_EVIDENCE") ?? "var/qa/postgres-acceptance";
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
await using var dataSource = NpgsqlDataSource.Create(connectionString);
await using (var connection = await dataSource.OpenConnectionAsync()) {
    foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Sql"), "*.sql").Order(StringComparer.Ordinal)) {
        await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(path), connection);
        await command.ExecuteNonQueryAsync();
    }
}
var room = "postgres-core-" + Guid.NewGuid().ToString("N");
var seats = new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" };
var initial = MatchReplayInitialStateBuilder.FromSeats(room, seats);
var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(dataSource);
var players = new PostgresMatchPlayerStore(dataSource); var store = new PostgresMatchRecoveryStore(dataSource);
IMatchSession session = new MatchSession(initial, engine, journal, players);
var current = initial; var checkpoints = new List<object>(); var submitted = 0;
var expectedTicks = new HashSet<long>(); var expectedFrames = new HashSet<(long Tick, long Sequence)>();
long expectedEventSequence = 0;
var catalog = await OfficialCardCatalog.LoadDefaultAsync();
var decks = PreconstructedDeckCatalog.Build(catalog);
await players.SavePlayerSessionAsync(room, "P1", "P1", ReconnectTokenHasher.Hash("audit-p1"), default);
await players.SavePlayerSessionAsync(room, "P2", "P2", ReconnectTokenHasher.Hash("audit-p2"), default);

try {
    foreach (var (actor, id) in new[] { ("P1", "jhin-lowcurve"), ("P2", "rumble-lowcurve") }) {
        var deck = decks.Single(d => d.Id == id).Decklist;
        var command = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        var result = await session.SubmitDeckAsync(actor, "deck-" + actor, command, Raw(command), default);
        Check(result.Accepted, result.ErrorMessage); Record(result);
    }
    foreach (var actor in seats.Keys) {
        var result = await session.ReadyAsync(actor, "ready-" + actor, Raw(new ReadyCommand()), default);
        Check(result.Accepted, result.ErrorMessage); Record(result);
    }
    Check(current.Phase == MatchPhases.Mulligan, "Real official deck opening must reach mulligan.");
    var first = current.ActivePlayerId; var second = current.OpeningSecondActionPlayerId!;
    await Submit(first, "mulligan-first", new MulliganCommand([]));
    Check(current.PlayerZones.Values.All(z => z.Hand.Count > 0), "The real opening must create private hands.");
    await ColdRecover("pending-second-mulligan");
    await Submit(second, "mulligan-second", new MulliganCommand([]));
    Check(current.Phase != MatchPhases.Mulligan, "Recovered opening must finish mulligan.");
    await ColdRecover("completed-opening");
    var invalid = new UnsupportedCommand("INVALID_POSTGRES_AUDIT");
    await Submit("P1", "invalid", invalid, accepted: false);
    await ColdRecover("accepted-and-rejected-raw-commands");
    await Submit("P1", "surrender", new SurrenderCommand());
    Check(current.Status == MatchStatuses.Finished, "Real surrender must persist the completed match.");
    await ColdRecover("finished-match");

    var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing durable frame.");
    Check(frame.Commands.Count == submitted && frame.Commands.Count(c => !c.Accepted) == 1, "Actual SQL command identities and rejected command must be retained.");
    Check(frame.Commands.Single(c => c.ClientIntentId == "invalid").RawCommand!.Value.GetProperty("cmdType").GetString() == "INVALID_POSTGRES_AUDIT",
        "Actual SQL must retain the rejected raw payload.");
    var tamperResults = new List<object>();
    await using (var connection = await dataSource.OpenConnectionAsync()) {
        await using var begin = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
        begin.Parameters.AddWithValue("room", room); var saved = (string)(await begin.ExecuteScalarAsync())!;
        await using var forge = new NpgsqlCommand("update state_snapshots set payload=jsonb_set(payload,'{tick}','999999'::jsonb) where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
        forge.Parameters.AddWithValue("room", room); await forge.ExecuteNonQueryAsync();
        var forged = await store.LoadAsync(room, default);
        Check(forged is not null && !forged.IsConsistent && forged.ValidationErrors.Any(e => e.Contains("tick", StringComparison.OrdinalIgnoreCase)), "SQL-forged authoritative tick must be rejected.");
        tamperResults.Add(new { mutation = "authoritative-tick", rejected = true, errors = forged!.ValidationErrors });
        await using var restore = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
        restore.Parameters.AddWithValue("payload", saved); restore.Parameters.AddWithValue("room", room); await restore.ExecuteNonQueryAsync();
    }
    await ColdRecover("restored-after-tamper");
    await using var sqlConnection = await dataSource.OpenConnectionAsync();
    await using var countsCommand = new NpgsqlCommand("select (select count(*) from command_log where match_id=@room),(select count(*) from game_events where match_id=@room),(select count(*) from state_snapshots where match_id=@room),(select count(*) from snapshots where match_id=@room),(select count(*) from action_prompts where match_id=@room),version(),(select count(*) from snapshots where match_id=@room and snapshot_tick=(select max(snapshot_tick) from snapshots where match_id=@room)),(select count(*) from action_prompts where match_id=@room and prompt_tick=(select max(prompt_tick) from action_prompts where match_id=@room))", sqlConnection);
    countsCommand.Parameters.AddWithValue("room", room);
    await using var reader = await countsCommand.ExecuteReaderAsync(); Check(await reader.ReadAsync(), "Actual database counts required.");
    var counts = new { commands = reader.GetInt64(0), events = reader.GetInt64(1), states = reader.GetInt64(2), snapshots = reader.GetInt64(3), prompts = reader.GetInt64(4), currentSnapshots = reader.GetInt64(6), currentPrompts = reader.GetInt64(7) };
    await File.WriteAllTextAsync(Path.Combine(output, "sql-counts.json"), JsonSerializer.Serialize(new { submitted, counts }, json));
    Check(counts.commands == submitted && counts.events == expectedEventSequence && counts.events > 0 && counts.states == expectedFrames.Count
        && counts.snapshots == 2 * expectedTicks.Count && counts.prompts == 2 * expectedTicks.Count
        && counts.currentSnapshots == 2 && counts.currentPrompts == 2,
        "SQL rows must retain accepted state/view history and both current player views: " + JsonSerializer.Serialize(counts, json));
    var receipt = new { status = "REAL_POSTGRES_CORE_OPENING_RECOVERY_REPLAY_AND_TAMPER_PASS", databaseVersion = reader.GetString(5), host = settings.Host, port = settings.Port, database = settings.Database,
        executedAtUtc = DateTimeOffset.UtcNow, room, submitted, counts, expectedDistinctTicks = expectedTicks.Count, expectedStateFrames = expectedFrames.Count,
        checkpoints, tamperResults, finalStateHash = MatchStateHasher.Hash(current),
        scope = "Actual core official opening, cold registry recovery, raw rejection, persisted surrender and SQL tamper detection; not full-score victory, all cards, visible clients or database failover." };
    await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(receipt, json));
    Console.WriteLine("POSTGRES_CORE_ACCEPTANCE_PASS: " + submitted + " real commands, " + checkpoints.Count + " recovery/replay checkpoints, SQL tamper rejected");
} finally {
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var cleanup = new NpgsqlCommand("delete from matches where match_id=@room", connection); cleanup.Parameters.AddWithValue("room", room);
    await cleanup.ExecuteNonQueryAsync();
}

await BattleHistorySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "battle-history"));
await MaskedAttendantSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "masked-attendant"));
await GrantedPredictSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "granted-predict"));
await PrintedPredictSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "printed-predict"));
await SivirOptionalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "sivir-optional"));
await RuneRecycleDestinationSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "rune-destination"));
await SivirPaymentSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "sivir-payment"));
await ConquestTokenSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "conquest-token"));
await RecycledCardBoonSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "recycled-card-boon"));
await RecycleBoonLifecycleSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "recycle-boon-lifecycle"));
await AttachedRecycleLifecycleSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "attached-recycle-lifecycle"));
await CardPlayHistorySqlAcceptance.RunAsync(dataSource, Path.Combine(output, "typed-play-history"));
await PatchedPorobotSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "porobot"));
await CardPlayHistoryCausalSqlAcceptance.RunAsync(dataSource, Path.Combine(output, "typed-play-history-causal"));

JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), json);
async Task Submit(string actor, string intent, GameCommand command, bool accepted = true) {
    var prompt = session.PromptFor(actor);
    var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Raw(command), json)!;
    fields["promptId"] = JsonSerializer.SerializeToElement(prompt.PromptId);
    fields["snapshotTick"] = JsonSerializer.SerializeToElement(current.Tick);
    var raw = JsonSerializer.SerializeToElement(fields); var before = MatchStateHasher.Hash(current);
    var result = await session.SubmitAsync(actor, intent, command, raw, default);
    Check(result.Accepted == accepted, result.ErrorMessage);
    if (!accepted) Check(MatchStateHasher.Hash(result.State) == before, "Rejected command must not change durable state.");
    Record(result);
    var rows = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Actual SQL write missing."); var count = rows.Commands.Count;
    var duplicate = await session.SubmitAsync(actor, intent, command, raw, default);
    Check(duplicate.Accepted == accepted && (await store.LoadAsync(room, default))!.Commands.Count == count, "Duplicate intent must not persist twice.");
}
void Record(ResolutionResult result) {
    current = result.State; submitted++; expectedEventSequence += result.Events.Count;
    expectedTicks.Add(current.Tick); expectedFrames.Add((current.Tick, expectedEventSequence));
}
async Task ColdRecover(string label) {
    var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Actual SQL recovery missing.");
    Check(frame.IsConsistent, string.Join("; ", frame.ValidationErrors));
    Check(MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "Cold SQL recovery must equal live core state.");
    var errors = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default);
    Check(errors.Count == 0, string.Join("; ", errors));
    var registry = new InMemoryMatchSessionRegistry(engine, journal, store);
    session = await registry.GetOrCreateAsync(room, default);
    foreach (var actor in seats.Keys) Check(session.SnapshotFor(actor).Tick == current.Tick, "Both players must recover the current prompt/snapshot tick.");
    checkpoints.Add(new { label, tick = current.Tick, commands = frame.Commands.Count, events = frame.Events.Count, stateHash = MatchStateHasher.Hash(current), replayMatched = true });
}
static void Check(bool valid, string? reason) { if (!valid) throw new InvalidOperationException(reason ?? "PostgreSQL acceptance invariant failed."); }
