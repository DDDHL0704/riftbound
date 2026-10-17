using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class OptionalCostsSqlAcceptance
{
    private static readonly string[] Kinds = ["PLAY_CARD", "ACTIVATE_ABILITY", "LEGEND_ACT", "HIDE_CARD",
        "REVEAL_CARD", "MOVE_UNIT", "ASSEMBLE_EQUIPMENT", "DECLARE_BATTLE"];
    private static readonly (string Name, string? Value, string? Extra)[] Malformed = [
        ("null", "null", null), ("object", "{}", null), ("number", "1", null),
        ("boolean", "true", null), ("scalar-string", "\"ECHO\"", null),
        ("null-element", "[null]", null), ("number-element", "[1]", null),
        ("empty-element", "[\"\"]", null), ("blank-element", "[\"  \"]", null),
        ("leading-space", "[\" ECHO\"]", null), ("trailing-space", "[\"ECHO \"]", null),
        ("mixed-elements", "[\"ECHO\",null]", null),
        ("duplicate-valid", "[]", ",\"optionalCosts\":[]"),
        ("duplicate-last-valid", "null", ",\"optionalCosts\":[]"),
        ("property-whitespace", null, ",\" optionalCosts \":[]")
    ];

    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "card-play-history-current-opening-20261017.json");
        var fixtureBytes = await File.ReadAllBytesAsync(fixturePath);
        using var fixture = JsonDocument.Parse(fixtureBytes);
        Check(fixture.RootElement.GetProperty("openingSeed").GetInt64() == 0, "Preserve the fixture's explicit original seed0.");
        var recorded = fixture.RootElement.GetProperty("acceptedCommands").EnumerateArray().ToArray();
        Check(recorded.Length == 9, "Preserve all nine original command identities.");
        var results = new List<object>();
        foreach (var shape in new[] { "omitted", "empty" })
        {
            var room = "postgres-optional-costs-" + shape + "-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room,
                fixture.RootElement.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial.Seed == 0 && initial.Tick == 0 && initial.CardObjects.Count == 0,
                "This bounded fixture must start at its original empty SEATING input.");
            var engine = new CoreRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source);
            var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("optional-" + player), default);
            var current = initial;
            var submitted = 0;
            foreach (var entry in recorded.Take(8))
            {
                var raw = entry.GetProperty("rawCommand").Clone();
                var actor = entry.GetProperty("playerId").GetString()!;
                var id = entry.GetProperty("clientIntentId").GetString()!;
                // These are fresh rooms, not imported accepted history. Submit
                // the same ordinary actions against each room's live hints.
                var fields = JsonNode.Parse(raw.GetRawText())!.AsObject();
                if (fields.ContainsKey("promptId")) fields["promptId"] = session.PromptFor(actor).PromptId;
                if (fields.ContainsKey("snapshotTick")) fields["snapshotTick"] = current.Tick;
                raw = JsonSerializer.SerializeToElement(fields);
                var command = GameCommandJsonMapper.Map(raw);
                var result = command switch
                {
                    SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, id, deck, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, id, raw, default),
                    _ => await session.SubmitAsync(actor, id, command, raw, default)
                };
                Check(result.Accepted, result.ErrorMessage);
                current = result.State;
                submitted++;
            }
            var actorId = recorded[^1].GetProperty("playerId").GetString()!;
            var plannedPlay = recorded[^1].GetProperty("rawCommand");
            var rejected = new List<object>();
            var checks = new List<object>();
            await Cold("original-eight-command-prefix");
            var beforeSql = await SqlDigest();
            foreach (var kind in Kinds)
            foreach (var malformed in Malformed)
                await Reject(kind, malformed.Name, Raw(kind, malformed.Value, malformed.Extra), kind + "-" + malformed.Name);
            foreach (var kind in Kinds)
                await Reject(kind, "stale-null", Raw(kind, "null", stale: true), kind + "-stale-null");
            Check(await SqlDigest() == beforeSql, "No invalid payload may add or alter any persisted row.");
            await Cold("after-128-invalid-payloads");

            // The initial malformed PLAY_CARD did not reserve its intent ID.
            var validRaw = Raw("PLAY_CARD", shape == "omitted" ? null : "[]");
            var accepted = await session.SubmitAsync(actorId, "PLAY_CARD-null", GameCommandJsonMapper.Map(validRaw), validRaw, default);
            Check(accepted.Accepted, accepted.ErrorMessage);
            current = accepted.State;
            submitted++;
            await Cold("legal-play-using-unreserved-intent");
            beforeSql = await SqlDigest();
            foreach (var kind in Kinds)
                await Reject(kind, "after-cold-null", Raw(kind, "null"), kind + "-after-cold-null");
            Check(await SqlDigest() == beforeSql, "Malformed requests after cold recovery must not poison accepted history.");

            var next = new PassPriorityCommand();
            var nextActor = current.PriorityPlayerId!;
            var nextId = "original-next-pass";
            var expected = await engine.ResolveAsync(current, new PlayerIntent(nextId, nextActor, next.CmdType), next, default);
            Check(expected.Accepted, expected.ErrorMessage);
            var nextRaw = JsonSerializer.SerializeToElement(new { cmdType = next.CmdType,
                promptId = session.PromptFor(nextActor).PromptId, snapshotTick = current.Tick });
            var actual = await session.SubmitAsync(nextActor, nextId, next, nextRaw, default);
            Check(actual.Accepted && MatchStateHasher.Hash(actual.State) == MatchStateHasher.Hash(expected.State)
                && JsonSerializer.Serialize(actual.Events, json) == JsonSerializer.Serialize(expected.Events, json),
                "Cold-restored execution of the original next command must match live rules and events.");
            current = actual.State;
            submitted++;
            var final = await Cold("original-next-command-after-cold");
            Check(final.Commands.Count == 10 && final.Commands.All(command => command.Accepted),
                "Exactly the eight original prefix, one legal play and one next command must be journaled.");
            var play = final.Commands.Single(command => command.CommandType == "PLAY_CARD").RawCommand!.Value;
            Check(shape == "omitted" ? !play.TryGetProperty("optionalCosts", out _) :
                play.GetProperty("optionalCosts").ValueKind == JsonValueKind.Array && play.GetProperty("optionalCosts").GetArrayLength() == 0,
                "Actual JSONB command history must retain the chosen canonical shape.");
            results.Add(new { room, shape, submitted, malformedRequestsRejected = rejected.Count, rejected, checkpoints = checks,
                events = final.Events.Count, finalHash = MatchStateHasher.Hash(current), finalSqlDigest = await SqlDigest(),
                ruleInputs = current.RuleInputs, databaseRoomRetained = true });

            async Task Reject(string kind, string malformed, JsonElement raw, string id)
            {
                var before = MatchStateHasher.Hash(current);
                var result = await session.SubmitAsync(actorId, id, GameCommandJsonMapper.Map(raw), raw, default);
                Check(!result.Accepted && result.ErrorCode == ErrorCodes.InvalidPayload && result.Events.Count == 0
                    && MatchStateHasher.Hash(result.State) == before, "Malformed " + kind + "/" + malformed + " reached journal/rules.");
                Check(await SqlDigest() == beforeSql, "An invalid request changed actual SQL rows: " + kind + "/" + malformed);
                rejected.Add(new { kind, malformed, errorCode = result.ErrorCode, stateAndEverySqlRowUnchanged = true });
            }

            JsonElement Raw(string kind, string? value, string? extra = null, bool stale = false)
            {
                var node = JsonNode.Parse(plannedPlay.GetRawText())!.AsObject();
                node["cmdType"] = kind;
                node["abilityId"] = "SHAPE_ONLY_ABILITY";
                node["origin"] = "BASE";
                node["destination"] = "BASE";
                node["targetObjectId"] = node["sourceObjectId"]!.GetValue<string>();
                node["battlefieldId"] = current.PlayerZones[actorId].Battlefields.First(id =>
                    current.CardObjects[id].Tags.Contains("CARD_TYPE:BATTLEFIELD"));
                node["attackerObjectIds"] = new JsonArray();
                node["defenderObjectIds"] = new JsonArray();
                node["promptId"] = stale ? "STALE_PROMPT" : session.PromptFor(actorId).PromptId;
                node["snapshotTick"] = stale ? current.Tick - 1 : current.Tick;
                node.Remove("optionalCosts");
                var text = node.ToJsonString();
                using var document = JsonDocument.Parse(text[..^1] + (value is null ? "" : ",\"optionalCosts\":" + value) + extra + "}");
                return document.RootElement.Clone();
            }

            async Task<MatchRecoveryFrame> Cold(string label)
            {
                var before = await SqlDigest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL room.");
                Check(frame.IsConsistent && frame.Commands.Count == submitted && frame.OpeningSeed == 0
                    && frame.ReplayInitialState!.Seed == 0 && frame.ReplayInitialState.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "Actual SQL frame must preserve seed, exact history and authoritative state: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0,
                    "Complete original action/event/state replay must match.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                var expectedViews = ResolutionResult.BuildSnapshots(current);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player), json),
                        JsonSerializer.SerializeToNode(expectedViews[player], json)), "Cold player snapshot changed.");
                Check(await SqlDigest() == before, "Cold registry/replay must not rewrite accepted SQL rows.");
                checks.Add(new { label, tick = current.Tick, commands = frame.Commands.Count, events = frame.Events.Count,
                    stateHash = MatchStateHasher.Hash(current), sqlRowsUnchangedByRecovery = true, completeOriginalReplayMatched = true });
                return frame;
            }

            async Task<string> SqlDigest()
            {
                var rows = new StringBuilder();
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    // Only the fixed table names above are interpolated; room identity remains a parameter.
                    await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text), '') from {table} t where match_id=@room", connection);
                    query.Parameters.AddWithValue("room", room);
                    rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
                }
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_OPTIONAL_COSTS_REJECTION_COLD_RECOVERY_AND_ORIGINAL_REPLAY_PASS",
            executedAtUtc = DateTimeOffset.UtcNow, fixtureSha256 = Convert.ToHexString(SHA256.HashData(fixtureBytes)).ToLowerInvariant(),
            originalExplicitSeed = 0, freshRoomPrefixUsesCurrentPromptIdentity = true, sourceFixtureNeverRewritten = true, results,
            scope = "Two new seed0 fixture rooms, eight optional-cost consumers, all seven durable tables, canonical omitted/empty play, cold original next command and complete original replay. This is not production entropy, visible clients, Windows, other raw fields or whole-card certification."
        }, json));
        Console.WriteLine("POSTGRES_OPTIONAL_COSTS_PASS: 2 actual rooms, 272 malformed requests rejected without SQL writes; both canonical plays, cold next command and original replay passed.");
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
