using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

var root = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound"));
if (settings.Database != "riftbound_acceptance" || settings.Host != "127.0.0.1") throw new Exception("Dedicated loopback audit database required.");
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
var engine = new CoreRuleEngine(); var receipts = new List<object>();
foreach (var branch in new[] { "p2-choice-owner-False", "p2-choice-owner-True" })
{
    using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "fixed-prefixes", branch, "p2-choice-owner-prefix.json")));
    var initial = fixture.RootElement.GetProperty("initial").Deserialize<MatchState>(json)! with { RoomId = "sql-sole-choice-owner-" + Guid.NewGuid().ToString("N") };
    var room = initial.RoomId; var current = initial;
    var fixtureState = fixture.RootElement.GetProperty("state").Deserialize<MatchState>(json)!;
    var expectedOrder = fixtureState.TriggerQueue.Where(t => t.UnitPlayMight is not null)
        .OrderBy(t => t.UnitPlayMight!.CapturedTick).Select(t => t.UnitPlayMight!.PlayedUnit.ObjectId).ToArray();
    Check(initial.Tick == 0 && initial.CardObjects.Count > 0 && expectedOrder.Length == 4,
        "Explicit midgame prefix; legal original opening is not certified.");
    var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
    var store = new PostgresMatchRecoveryStore(source); IMatchSession session = new MatchSession(initial, engine, journal, players);
    foreach (var player in initial.Seats.Keys) await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("chrono-" + player), default);
    var checkpoints = new List<object>(); var attacks = new List<object>(); var count = 0; var events = 0; var attacked = false; var nextMatched = 0; var afterCold = false;
    foreach (var entry in fixture.RootElement.GetProperty("commands").EnumerateArray())
    {
        await Submit(entry.GetProperty("playerId").GetString()!, entry.GetProperty("clientIntentId").GetString()!, entry.GetProperty("rawCommand"));
        if (current.TriggerQueue.Any(t => t.UnitPlayMight is not null))
        {
            Check(current.TriggerQueue.Where(t => t.UnitPlayMight is not null).All(t => t.EnqueuedAtTick == t.UnitPlayMight!.CapturedTick), "Every persisted legend clock equals its independent play origin.");
            await Cold("pending-original-effect-play");
            if (!attacked) { await Attack(); attacked = true; }
        }
    }
    await Cold("before-owner-order");
    Check(current.TurnPlayerId == "P1" && !session.PromptFor("P1").Actionable && session.PromptFor("P2").Actionable,
        "Actual SQL-restored prompts grant the sole simultaneous choice to off-turn P2.");
    var prompt = session.PromptFor("P2");
    var candidate = prompt.Candidates!.Single(c => c.Action == CommandTypes.OrderTriggers && c.Enabled);
    var ids = ((IEnumerable<string>)candidate.Metadata!["orderedTriggerIds"]!).ToArray();
    var pair = current.TriggerQueue.Where(t => t.ControllerId == "P2" && t.EnqueuedAtTick == 10)
        .OrderBy(t => (t.UnitPlayMight is not null) == branch.EndsWith("True", StringComparison.Ordinal) ? 0 : 1).ToArray();
    Check(pair.Length == 2, "Exactly one controller owns the only two-skill simultaneous block.");
    var index = Array.FindIndex(ids, id => pair.Any(t => t.TriggerId == id));
    ids[index] = pair[0].TriggerId; ids[index + 1] = pair[1].TriggerId;
    var command = new OrderTriggersCommand(OrderedTriggerIds: ids); var beforeWrong = await Digest();
    var wrong = await engine.ResolveAsync(current, new("wrong-owner", "P1", CommandTypes.OrderTriggers), command, default);
    Check(!wrong.Accepted && MatchStateHasher.Hash(wrong.State) == MatchStateHasher.Hash(current)
        && beforeWrong == await Digest(), "Independent wrong-controller rejection cannot change restored state or SQL.");
    await Submit("P2", "sql-owner-order", Raw(command));
    await Cold("chronological-confirmations-after-owner-order");
    var powers = expectedOrder.ToDictionary(id => id, id => current.CardObjects[id].Power); var confirmed = new List<string>();
    while (current.PendingCardChoice is { } choice)
    {
        var item = current.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var played = item.UnitPlayMight!.PlayedUnit.ObjectId; confirmed.Add(played);
        await Submit(choice.PlayerId, "sql-confirm-" + count, Raw(new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [played])));
        await Cold("confirmed-" + played);
    }
    Check(confirmed.SequenceEqual(expectedOrder), "Actual SQL-restored confirmations preserve earned chronology, including both controllers.");
    while (current.StackItems.Count > 0)
    {
        Check(count < 60 && current.PriorityPlayerId is not null, "Bounded genuine response loop.");
        await Submit(current.PriorityPlayerId!, "sql-response-" + count, Raw(new PassPriorityCommand()));
    }
    foreach (var id in expectedOrder) Check(current.CardObjects[id].Power == powers[id] + 1, "Each genuine legendary skill resolves once.");
    await Cold("final-original-replay");
    var final = await store.LoadAsync(room, default) ?? throw new Exception("Missing final SQL frame.");
    await File.WriteAllTextAsync(Path.Combine(output, branch + "-receipt.json"), JsonSerializer.Serialize(new {
        status = "REAL_POSTGRES_OFFTURN_SOLE_CHOICE_OWNER_PASS", room, branch, commands = count, events, checkpoints,
        attacks, originalNextCommandsMatched = nextMatched, confirmed, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
        productionRegistryPositiveReplayFromBlank = "NOT_APPLICABLE_EXPLICIT_MIDGAME",
        scope = "Fresh explicit-midgame rules fixture, same actual compiled modules as current conformance. Not a native match, blank opening, legal ordinary opening, visible off-turn ordering, whole card, or Windows acceptance."
    }, json));
    receipts.Add(new { branch, room, commands = count, events, coldRecoveries = checkpoints.Count, forgedClocksRejected = attacks.Count, originalNextCommandsMatched = nextMatched });

    async Task Submit(string actor, string intent, JsonElement template)
    {
        var fields = JsonNode.Parse(template.GetRawText())!.AsObject(); fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
        var raw = JsonSerializer.SerializeToElement(fields, json); var command = GameCommandJsonMapper.Map(raw);
        var expected = afterCold ? await engine.ResolveAsync(current, new(intent, actor, command.CmdType), command, default) : null;
        var result = await session.SubmitAsync(actor, intent, command, raw, default); Check(result.Accepted, result.ErrorMessage ?? command.CmdType);
        if (afterCold)
        {
            Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)), "Original next command/state/events agree after actual SQL cold restore.");
            nextMatched++;
        }
        afterCold = false; current = result.State; count++; events += result.Events.Count;
    }
    async Task Cold(string stage)
    {
        var before = await Digest(); var frame = await store.LoadAsync(room, default) ?? throw new Exception("Missing SQL frame.");
        Check(frame.IsConsistent && frame.Commands.Count == count && frame.Events.Count == events && frame.ReplayInitialState is { Tick: 0 } s && s.CardObjects.Count == 0
            && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "SQL original prefix/state/log: " + string.Join("; ", frame.ValidationErrors));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, frame.Commands, current, engine, default, frame.Events);
        Check(replay.IsMatch, "Explicit original midgame action/event/state replay: " + string.Join("; ", replay.Errors));
        var restored = MatchSession.Restore(frame, engine, journal, players);
        foreach (var player in initial.Seats.Keys)
            Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))), "Both player views survive genuine cold reconstruction.");
        Check(before == await Digest(), "Cold recovery cannot write any of seven original SQL tables.");
        session = restored; checkpoints.Add(new { stage, current.Tick, commands = count, events, fullOriginalReplay = true, sevenTablesUnchanged = true }); afterCold = true;
    }
    async Task Attack()
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var read = new NpgsqlCommand("select payload::text from state_snapshots where match_id=@room order by state_tick desc,last_event_sequence desc limit 1", connection);
        read.Parameters.AddWithValue("room", room); var saved = (string)(await read.ExecuteScalarAsync())!;
        var original = await Digest(); var clean = await store.LoadAsync(room, default); var log = JsonSerializer.Serialize(new { clean!.Commands, clean.Events }, json);
        foreach (var mutation in new[] { "missing", "zero", "future" })
        {
            var payload = JsonNode.Parse(saved)!.AsObject(); var queue = payload["triggerQueue"]!.AsArray();
            var t = queue.Single(n => n?["unitPlayMight"] is not null)!.AsObject();
            t["enqueuedAtTick"] = mutation == "missing" ? null : JsonValue.Create(mutation == "zero" ? 0L : current.Tick + 1);
            try
            {
                await Write(payload.ToJsonString(json)); var bad = await store.LoadAsync(room, default) ?? throw new Exception("Missing forged SQL frame.");
                Check(!bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("unit play might trigger")), "Independent origin guard rejects an altered persisted clock.");
                Check(log == JsonSerializer.Serialize(new { bad.Commands, bad.Events }, json), "Accepted history unchanged by attack.");
                var attackedDigest = await Digest(); var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                try { await registry.GetOrCreateAsync(room, default); throw new Exception("Forged chronology recovered."); }
                catch (MatchSessionException error) { Check(error.Code == ErrorCodes.RecoveryInconsistent && error.Message.Contains("unit play might trigger"), error.Message); }
                Check(await registry.FindAsync(room, default) is null && attackedDigest == await Digest(), "Rejected clock cannot cache a session or write SQL.");
                attacks.Add(new { mutation, structuralAndRegistryRejected = true, acceptedHistoryUnchanged = true, noSqlWritesOnRejection = true });
            }
            finally { await Write(saved); Check(original == await Digest(), "Restore every seven-table row exactly after bounded forgery."); }
        }
        async Task Write(string payload)
        {
            await using var update = new NpgsqlCommand("update state_snapshots set payload=@payload::jsonb where match_id=@room and state_tick=(select max(state_tick) from state_snapshots where match_id=@room)", connection);
            update.Parameters.AddWithValue("room", room); update.Parameters.AddWithValue("payload", payload); Check(await update.ExecuteNonQueryAsync() == 1, "Only the dedicated latest snapshot is modified.");
        }
    }
    async Task<string> Digest()
    {
        var rows = new List<string>(); await using var connection = await source.OpenConnectionAsync();
        foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
        {
            await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text,E'\\n' order by row_to_json(t)::text),'') from {table} t where match_id=@room", connection);
            query.Parameters.AddWithValue("room", room); rows.Add((string)(await query.ExecuteScalarAsync())!);
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
    }
}
await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new { status = "REAL_SQL_SOLE_CHOICE_TWO_EXPLICIT_FIXTURES_PASS", receipts }, json));
Console.WriteLine(JsonSerializer.Serialize(receipts));
JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), json);
static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
