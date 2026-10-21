using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

Check(args.Length == 2 && !File.Exists(args[1]), "Exact retained original partial frame and unused output required.");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
Check(RuleInputIdentity.Current.IdentitySha256 == "ec217d6fa40c76c0fa4da67cefa8a2f7b184a196bdcdef03e26acfcddede1468", "Actual original fixed7a API input identity, without current-WIP substitution.");
using var zip = new GZipStream(File.OpenRead(args[0]), CompressionMode.Decompress); using var original = JsonDocument.Parse(zip);
var saved = original.RootElement.Deserialize<MatchRecoveryFrame>(json)!; var room = saved.RoomId;
Check(room == "RB-CXM5MH" && saved.Commands.Count == 41 && saved.Events.Count == 263, "The exact actual visible native ordinary match.");
var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound"));
Check(settings.Host == "127.0.0.1" && settings.Port == 55486 && settings.Database == "riftbound_here_native" && settings.Username == "codex_riftbound_audit", "Only own retained native database.");
settings.Options = "-c default_transaction_read_only=on"; await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
var frame = await new PostgresMatchRecoveryStore(source).LoadAsync(room, default) ?? throw new Exception("Original partial room must remain in SQL.");
Check(Same(original.RootElement, JsonSerializer.SerializeToElement(frame, json)) && frame.IsConsistent && frame.OpeningSeed == saved.OpeningSeed && frame.OpeningSeed != 0 && frame.ReplayInitialState is { Tick: 0, CardObjects.Count: 0 }, "Every original frame field/null/omission, private seed and blank opening remains unchanged after the separate second match.");
var final = frame.AuthoritativeState!; var actors = final.Seats.Keys.ToArray();
Check(final.Status == MatchStatuses.Finished && final.TurnNumber == 18 && final.WinnerPlayerId == "qa64678a069e88e6" && final.PlayerScores["qa64678a069e88e6"] == 8 && final.PlayerScores["qa64678b644fc6bc"] == 0 && final.PlayerZones["qa64678a069e88e6"].MainDeck.Count == 27 && final.PlayerZones["qa64678b644fc6bc"].MainDeck.Count == 26, "Literal normal18-turn8:0 score victory with both original decks nonempty.");
var states = new Dictionary<(long, long), MatchState>(); var counts = new Dictionary<string, int>();
await using (var connection = await source.OpenConnectionAsync())
{
    Check((string)(await new NpgsqlCommand("show transaction_read_only", connection).ExecuteScalarAsync())! == "on", "Actual read-only connection.");
    await using (var query = new NpgsqlCommand("select last_event_sequence,payload::text from state_snapshots where match_id=@room order by id", connection))
    {
        query.Parameters.AddWithValue("room", room); await using var rows = await query.ExecuteReaderAsync();
        while (await rows.ReadAsync())
        {
            var raw = rows.GetString(1); var state = JsonSerializer.Deserialize<MatchState>(raw, json)!;
            Check(JsonNode.DeepEquals(JsonNode.Parse(raw), JsonSerializer.SerializeToNode(state, json)) && state.RuleInputs == RuleInputIdentity.Current && states.TryAdd((state.Tick, rows.GetInt64(0)), state), "Every original authority field/null/omission and tick/sequence retained.");
        }
    }
    Check(states.Count == 41 && frame.Commands.All(c => c.Accepted && c.RuleInputs == RuleInputIdentity.Current && states.ContainsKey((c.CompletedTick, c.CompletedEventSequence))), "All original41 actual native accepted commands retain their individual historical authority."); counts["state_snapshots"] = states.Count;
    var expected = frame.Commands.GroupBy(c => c.CompletedTick).Select(g => g.OrderBy(c => c.CompletedEventSequence).Last()).SelectMany(c => states[(c.CompletedTick,c.CompletedEventSequence)].Seats.Keys.Select(actor => (c.CompletedTick, c.CompletedEventSequence, actor))).ToHashSet();
    foreach (var table in new[] { "snapshots", "action_prompts" })
    {
        await using var query = new NpgsqlCommand($"select player_id,last_event_sequence,payload::text from {table} where match_id=@room order by id", connection); query.Parameters.AddWithValue("room", room);
        await using var rows = await query.ExecuteReaderAsync(); var actual = new HashSet<(long, long, string)>();
        while (await rows.ReadAsync())
        {
            var actor = rows.GetString(0); var sequence = rows.GetInt64(1); var raw = rows.GetString(2); using var document = JsonDocument.Parse(raw);
            var tick = document.RootElement.GetProperty(table == "snapshots" ? "tick" : "snapshotTick").GetInt64();
            Check(states.TryGetValue((tick, sequence), out var state) && actual.Add((tick, sequence, actor)), "Each original private row retains its viewer/tick/sequence binding.");
            var typed = table == "snapshots" ? JsonSerializer.SerializeToNode(JsonSerializer.Deserialize<SnapshotDto>(raw, json), json) : JsonSerializer.SerializeToNode(JsonSerializer.Deserialize<ActionPromptDto>(raw, json), json);
            var projected = table == "snapshots" ? JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state!)[actor], json) : JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state!)[actor], json);
            Check(JsonNode.DeepEquals(JsonNode.Parse(raw), typed) && JsonNode.DeepEquals(JsonNode.Parse(raw), projected), "Every original private field/null/omission survives typed read and equals its original historical projection.");
            foreach (var other in state!.Seats.Keys.Where(p => p != actor)) foreach (var id in state.PlayerZones.GetValueOrDefault(other,PlayerZones.Empty).MainDeck.Concat(state.PlayerZones.GetValueOrDefault(other,PlayerZones.Empty).Hand)) Check(!raw.Contains("\"" + id + "\"", StringComparison.Ordinal), "Opponent hidden identities remain absent.");
        }
        Check(actual.SetEquals(expected), "Both viewers retain the exact original latest private projection at every tick."); counts[table] = actual.Count;
    }
}
var expectedCounts=new Dictionary<string,int>{{"SUBMIT_DECK",2},{"READY",2},{"MULLIGAN",2},{"END_TURN",17},{"TAP_RUNE",2},{"PLAY_CARD",1},{"MOVE_UNIT",1},{"PASS_FOCUS",2},{"PASS_PRIORITY",12}};
Check(frame.Commands.Count==41&&frame.Commands.GroupBy(c=>c.CommandType).ToDictionary(g=>g.Key,g=>g.Count()).OrderBy(x=>x.Key).SequenceEqual(expectedCounts.OrderBy(x=>x.Key)),"Exactly41 native accepted commands: original setup6,17 normal ends,2 rune taps,one play,one move,2 focus passes and12 priority passes.");
var scores=frame.Events.Where(e=>e.Event.Kind=="SCORE_GAINED").Select(e=>JsonSerializer.SerializeToElement(e.Event.Payload,json)).ToArray();
Check(scores.Length==8&&scores.Select(e=>e.GetProperty("score").GetInt32()).SequenceEqual(Enumerable.Range(1,8))&&scores.All(e=>e.GetProperty("playerId").GetString()=="qa64678a069e88e6"&&e.GetProperty("amount").GetInt32()==1)&&scores[0].GetProperty("reason").GetString()=="BATTLEFIELD_CONQUERED_SCORE"&&scores.Skip(1).All(e=>e.GetProperty("reason").GetString()=="BATTLEFIELD_HELD_SCORE"),"Exactly one conquest and seven holds provide all eight points.");
Check(frame.Events.Count(e=>e.Event.Kind=="MATCH_WON")==1&&frame.Commands.All(c=>c.CommandType!="SURRENDER")&&frame.Events.All(e=>!e.Event.Kind.Contains("SURRENDER",StringComparison.Ordinal)&&!e.Event.Kind.Contains("BURNOUT",StringComparison.Ordinal)),"One ordinary score win, without surrender or burnout.");
Check(final.CardObjects.Values.Count(c=>c.Tags.Contains(CardObjectTags.MinionTokenFamily))==6,"Six completed pre-victory held-field skills create the six actual original minions.");
await using(var promptConnection=await source.OpenConnectionAsync()) foreach(var command in frame.Commands.Skip(4)){
 await using var q=new NpgsqlCommand("select payload::text from action_prompts where match_id=@room and player_id=@actor and prompt_tick=@tick and last_event_sequence=@sequence",promptConnection);q.Parameters.AddWithValue("room",room);q.Parameters.AddWithValue("actor",command.PlayerId);q.Parameters.AddWithValue("tick",command.StartedTick);q.Parameters.AddWithValue("sequence",command.StartedEventSequence);var rawPrompt=(string)(await q.ExecuteScalarAsync())!;Check(rawPrompt is not null,"Actual original native input prompt is independently retained.");using var prompt=JsonDocument.Parse(rawPrompt!);Check(command.RawCommand!.Value.GetProperty("promptId").GetString()==prompt.RootElement.GetProperty("promptId").GetString()&&command.RawCommand.Value.GetProperty("snapshotTick").GetInt64()==command.StartedTick,"Each of37 native gameplay inputs stamps the exact previously persisted private prompt and tick.");
}
var replaySession = new MatchSession(frame.ReplayInitialState!, new CoreRuleEngine(), NoopMatchJournal.Instance); var histories = 0;
Check(frame.Commands.All(c => !c.CommandType.StartsWith("DEV_SEED_SCENARIO", StringComparison.Ordinal)), "Original ordinary match contains no development seed command.");
foreach (var command in frame.Commands.OrderBy(c => c.StartedEventSequence).ThenBy(c => c.CompletedEventSequence).ThenBy(c => c.ClientIntentId, StringComparer.Ordinal))
{
    var raw = command.RawCommand!.Value; var intent = GameCommandJsonMapper.Map(raw);
    var result = intent switch { ReadyCommand => await replaySession.ReadyAsync(command.PlayerId, command.ClientIntentId, raw, default), SubmitDeckCommand deck => await replaySession.SubmitDeckAsync(command.PlayerId, command.ClientIntentId, deck, raw, default), _ => await replaySession.SubmitAsync(command.PlayerId, command.ClientIntentId, intent, raw, default) };
    Check(result.Accepted == command.Accepted && result.ErrorMessage == command.ErrorMessage && result.State.Tick == command.CompletedTick && Same(JsonSerializer.SerializeToElement(result.State, json), JsonSerializer.SerializeToElement(states[(command.CompletedTick, command.CompletedEventSequence)], json)), "Every original accepted command and complete historical state independently replays, without submitting anything to the live room.");
    var events = frame.Events.Where(e => e.Sequence > command.StartedEventSequence && e.Sequence <= command.CompletedEventSequence).OrderBy(e => e.Sequence).ThenBy(e => e.Order).ToArray();
    Check(events.Length == result.Events.Count, "Exact original event span count.");
    for (var i = 0; i < events.Length; i++) Check(JsonNode.DeepEquals(EventNode(events[i].Event), EventNode(result.Events[i])), "Every original event kind/description/payload exact; only unordered simultaneous rune actor sets are normalized.");
    histories++;
}
Check(histories == 41 && (await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default)).Count == 0, "All original partial history and complete unchanged recovery replay pass.");
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(new { status = "PASS_INDEPENDENT_VISIBLE_NATIVE41_ORDINARY_SCORE_ALL_HISTORY_REPLAY", room, commands = 41, events = 263, histories, actualHistoricalPayloads = counts, actualRuleInputs = RuleInputIdentity.Current, actualReadOnlyConnection = true, everyOriginalFieldNullOmissionAndPrivateProjectionExact = true, originalSeedBlankOpeningAndAcceptedHistoryUnchanged = true, nativeUiControlsAndVisibleBothResultWindowsActuallyObserved = true, physicalWindowsAcceptance = false, gameplayGatewayHelperUsed = false }, json));
Console.WriteLine("PASS visible native41/263 all original authority/private histories and complete normal8:0 replay.");
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static bool Same(JsonElement left, JsonElement right) => JsonNode.DeepEquals(JsonNode.Parse(left.GetRawText()), JsonNode.Parse(right.GetRawText()));
JsonNode EventNode(GameEvent value)
{
    var node = JsonSerializer.SerializeToNode(value, json)!;
    if (value.Kind == "RUNE_POOL_CLEARED" && node["payload"]?["playerIds"] is JsonArray ids) node["payload"]!["playerIds"] = new JsonArray(ids.Select(i => i!.GetValue<string>()).Order(StringComparer.Ordinal).Select(i => JsonValue.Create(i)).ToArray());
    return node;
}
