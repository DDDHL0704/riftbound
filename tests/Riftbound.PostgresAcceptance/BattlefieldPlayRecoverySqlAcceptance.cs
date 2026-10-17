using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class BattlefieldPlayRecoverySqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var results = new List<object>();
        foreach (var name in new[] { "merfolk", "blitzcrank" })
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "battlefield-base-" + name + ".json"));
            using var fixture = JsonDocument.Parse(bytes);
            var data = fixture.RootElement;
            Check(data.GetProperty("openingSeed").GetInt64() == 0, "Retain the explicit opening seed0.");
            var room = "postgres-battlefield-origin-" + name + "-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, data.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            var engine = new CoreRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source);
            var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("battlefield-origin-" + player), default);
            var current = initial;
            foreach (var entry in data.GetProperty("acceptedCommands").EnumerateArray())
            {
                var actor = entry.GetProperty("playerId").GetString()!;
                var fields = JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject();
                fields["promptId"] = session.PromptFor(actor).PromptId;
                fields["snapshotTick"] = current.Tick;
                var raw = JsonSerializer.SerializeToElement(fields);
                var command = GameCommandJsonMapper.Map(raw);
                var intent = entry.GetProperty("clientIntentId").GetString()!;
                var result = command switch {
                    SubmitDeckCommand deck => await session.SubmitDeckAsync(actor, intent, deck, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                    _ => await session.SubmitAsync(actor, intent, command, raw, default)
                };
                Check(result.Accepted, result.ErrorMessage);
                current = result.State;
            }
            var playerId = data.GetProperty("expectedActor").GetString()!;
            var unitId = data.GetProperty("expectedPlayedObject").GetString()!;
            var cardNo = data.GetProperty("expectedCardNo").GetString()!;
            Check(current.StackItems.Count == 0 && current.ObjectLocations[unitId].Zone == "BASE"
                && current.CardObjects[unitId].CardNo == cardNo, "The ordinary real-SQL base play must have no printed battlefield trigger.");
            var clean = await Cold();
            var originalDigest = await Digest();
            var log = JsonSerializer.Serialize(new { clean.Commands, clean.Events }, json);
            var saved = new List<(string Table, long Id, string? Player, string Payload)>();
            await using (var connection = await source.OpenConnectionAsync())
                foreach (var (table, tickColumn) in new[] { ("state_snapshots", "state_tick"), ("snapshots", "snapshot_tick"), ("action_prompts", "prompt_tick") })
                {
                    var playerColumn = table == "state_snapshots" ? "null::text" : "player_id";
                    await using var read = new NpgsqlCommand($"select id,{playerColumn},payload::text from {table} where match_id=@room and {tickColumn}=@tick", connection);
                    read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("tick", current.Tick);
                    await using var rows = await read.ExecuteReaderAsync();
                    while (await rows.ReadAsync()) saved.Add((table, rows.GetInt64(0), rows.IsDBNull(1) ? null : rows.GetString(1), rows.GetString(2)));
                }
            Check(saved.Count == 5, "Exactly one authoritative snapshot and both snapshots/prompts must be retained.");
            var attacks = new List<object>();
            var template = data.GetProperty("forgedPrimaryItem").Deserialize<StackItemState>(json)!;
            foreach (var battlefieldClaim in new[] { false, true })
            {
                var item = template with { Destination = battlefieldClaim
                    ? "BATTLEFIELD:" + current.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First() : "" };
                var forged = current with { StackItems = [item], PriorityPlayerId = playerId, TimingState = TimingStates.NeutralClosed };
                try
                {
                    await Write(forged);
                    var attackedDigest = await Digest();
                    var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing forged SQL frame.");
                    Check(log == JsonSerializer.Serialize(new { bad.Commands, bad.Events }, json), "Accepted command/event history must remain exact.");
                    if (battlefieldClaim) Check(bad.IsConsistent, "The plausible battlefield claim must reach the independent action-log audit: " + string.Join("; ", bad.ValidationErrors));
                    else Check(!bad.IsConsistent && bad.ValidationErrors.Any(e => e.Contains("battlefield-only", StringComparison.Ordinal)),
                        "The explicit false base condition must fail independent structural validation.");
                    var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                    try
                    {
                        await registry.GetOrCreateAsync(room, default);
                        throw new InvalidOperationException("Forged battlefield skill was accepted.");
                    }
                    catch (MatchSessionException error)
                    {
                        Check(error.Code == ErrorCodes.RecoveryInconsistent && (!battlefieldClaim
                            || error.Message.Contains("action-log audit failed", StringComparison.Ordinal)), error.Message);
                    }
                    Check(await registry.FindAsync(room, default) is null && await Digest() == attackedDigest,
                        "A rejected restore must not cache a session or change any persisted row.");
                    attacks.Add(new { battlefieldClaim, rebuiltBothPlayerViews = true, structuralValidationPassed = bad.IsConsistent,
                        productionRegistryRejected = true, acceptedHistoryUnchanged = true, everySqlRowUnchangedByRejection = true });
                }
                finally
                {
                    await using var connection = await source.OpenConnectionAsync();
                    await using var transaction = await connection.BeginTransactionAsync();
                    foreach (var row in saved) await Set(connection, transaction, row.Table, row.Id, row.Payload);
                    await transaction.CommitAsync();
                }
                Check(await Digest() == originalDigest, "All seven original SQL table contents must be exactly restored.");
                await Cold();
            }
            var next = new EndTurnCommand();
            var expected = await engine.ResolveAsync(current, new("original-next-end", playerId, next.CmdType), next, default);
            Check(expected.Accepted, expected.ErrorMessage);
            var nextRaw = JsonSerializer.SerializeToElement(new { cmdType = next.CmdType, promptId = session.PromptFor(playerId).PromptId, snapshotTick = current.Tick });
            var actual = await session.SubmitAsync(playerId, "original-next-end", next, nextRaw, default);
            Check(actual.Accepted && MatchStateHasher.Hash(actual.State) == MatchStateHasher.Hash(expected.State)
                && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(actual.Events, json), JsonSerializer.SerializeToNode(expected.Events, json)),
                "The exact original next command must still match after rejected forgeries and cold restore.");
            current = actual.State;
            var final = await Cold();
            results.Add(new { room, cardNo, originalExplicitSeed = 0, commands = final.Commands.Count, events = final.Events.Count,
                attacks, originalNextCommandMatched = true, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
                finalSqlDigest = await Digest(), fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), databaseRoomRetained = true });

            async Task<MatchRecoveryFrame> Cold()
            {
                var before = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing real SQL frame.");
                Check(frame.IsConsistent && frame.OpeningSeed == 0 && frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Seating }
                    && frame.ReplayInitialState.CardObjects.Count == 0 && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "Exact original empty opening/current state must survive SQL restore: " + string.Join("; ", frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Complete original replay must match.");
                session = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                Check(await Digest() == before, "Cold restore must not rewrite accepted SQL rows.");
                return frame;
            }

            async Task Write(MatchState state)
            {
                var snapshots = ResolutionResult.BuildSnapshots(state);
                var prompts = ResolutionResult.BuildPrompts(state);
                await using var connection = await source.OpenConnectionAsync();
                await using var transaction = await connection.BeginTransactionAsync();
                foreach (var row in saved)
                {
                    var payload = row.Table == "state_snapshots" ? JsonSerializer.Serialize(state, json)
                        : row.Table == "snapshots" ? JsonSerializer.Serialize(snapshots[row.Player!], json)
                        : JsonSerializer.Serialize(prompts[row.Player!], json);
                    await Set(connection, transaction, row.Table, row.Id, payload);
                }
                await transaction.CommitAsync();
            }

            async Task<string> Digest()
            {
                var rows = new StringBuilder();
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text),'') from {table} t where match_id=@room", connection);
                    query.Parameters.AddWithValue("room", room);
                    rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
                }
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_BATTLEFIELD_PLAY_ORIGIN_STRUCTURAL_AND_CAUSAL_GUARDS_PASS", executedAtUtc = DateTimeOffset.UtcNow, results,
            scope = "Two actual blank seed0 openings; four coordinated snapshot/view forgeries; independent structural and original-log rejection, exact seven-table restoration, cold original continuation and complete replay. Positive battlefield effects, visible clients, Windows and whole-card certification remain separate."
        }, json));
        Console.WriteLine("POSTGRES_BATTLEFIELD_PLAY_RECOVERY_PASS: 2 actual openings, 4 SQL forgeries rejected, all original rows restored, cold continuation and full replay passed.");
    }

    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string payload)
    {
        await using var update = new NpgsqlCommand($"update {table} set payload=@payload::jsonb where id=@id", connection, transaction);
        update.Parameters.AddWithValue("payload", payload); update.Parameters.AddWithValue("id", id);
        Check(await update.ExecuteNonQueryAsync() == 1, "Exactly one dedicated snapshot/view row must be updated.");
    }

    private static void Check(bool condition, string? message)
    { if (!condition) throw new InvalidOperationException(message); }
}
