using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class HereLocationSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "here-controller-rengar-confirmation-opening.json"));
        using var fixture = JsonDocument.Parse(bytes);
        var data = fixture.RootElement;
        Check(data.GetProperty("openingSeed").GetInt64() == 0, "Keep the new recipe's original explicit seed0.");
        var room = "postgres-here-location-" + Guid.NewGuid().ToString("N");
        var initial = MatchReplayInitialStateBuilder.FromSeats(room, data.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
        var journal = new PostgresMatchJournal(source); var players = new PostgresMatchPlayerStore(source);
        var store = new PostgresMatchRecoveryStore(source); var engine = new CoreRuleEngine();
        IMatchSession session = new MatchSession(initial, engine, journal, players);
        foreach (var player in initial.Seats.Keys)
            await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("here-location-" + player), default);
        var current = initial; var checkpoints = new List<object>(); var attacks = new List<object>();
        var index = 0; var originalNextAfterCold = false;
        var nextColdFamily = ""; var hereOriginalNext = 0; var rengarOriginalNext = 0;
        var actor = data.GetProperty("expectedActor").GetString()!;
        var opponent = data.GetProperty("expectedOpponent").GetString()!;
        var unit = data.GetProperty("expectedSource").GetString()!;
        var enemy = data.GetProperty("expectedEnemy").GetString()!;
        var boundary = data.GetProperty("pendingHereCommandCount").GetInt32();
        var orderBoundary = data.GetProperty("pendingOrderCommandCount").GetInt32();
        var targetBoundary = data.GetProperty("pendingRengarChoiceCommandCount").GetInt32();
        var confirmedBoundary = data.GetProperty("confirmedRengarCommandCount").GetInt32();
        foreach (var entry in data.GetProperty("acceptedCommands").EnumerateArray())
        {
            var player = entry.GetProperty("playerId").GetString()!;
            // Execute a fresh match under this actual runtime, with its current
            // prompt/tick. No old accepted history or rule-input header is edited.
            var fields = JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject();
            fields["promptId"] = session.PromptFor(player).PromptId; fields["snapshotTick"] = current.Tick;
            var raw = JsonSerializer.SerializeToElement(fields); var command = GameCommandJsonMapper.Map(raw);
            var intent = entry.GetProperty("clientIntentId").GetString()!;
            var expected = originalNextAfterCold ? await engine.ResolveAsync(current, new(intent, player, command.CmdType), command, default) : null;
            var result = command switch {
                SubmitDeckCommand deck => await session.SubmitDeckAsync(player, intent, deck, raw, default),
                ReadyCommand => await session.ReadyAsync(player, intent, raw, default),
                _ => await session.SubmitAsync(player, intent, command, raw, default)
            };
            Check(result.Accepted, $"{index} {player} {command.CmdType}: {result.ErrorMessage}");
            if (originalNextAfterCold)
            {
                Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)),
                    "The actual original next command and events must match after cold recovery.");
                if (nextColdFamily == "rengar") rengarOriginalNext++; else hereOriginalNext++;
            }
            originalNextAfterCold = false; current = result.State; index++;
            if (index == orderBoundary || index == targetBoundary || index == confirmedBoundary)
            {
                var stage = index == orderBoundary ? "rengar-order" : index == targetBoundary ? "rengar-target" : "rengar-confirmed";
                if (index == orderBoundary)
                {
                    Check(current.TriggerQueue.Count == 2 && current.PendingCardChoice is null && current.PriorityPlayerId is null
                        && current.TriggerQueue.Single(t => t.UnitPlayMight is not null).UnitPlayMight!.PlayedUnit.ObjectId == unit
                        && current.TriggerQueue.Single(t => t.PermanentPlayStackItemId is not null).SourceObjectId == unit,
                        "The actual printed ability and captured legend must await shared ordering.");
                }
                else if (index == targetBoundary)
                    Check(current.PendingCardChoice is { ChoiceWindow: "TRIGGER_CONFIRMATION", RequiredCount: 1, MaxCount: 1 } choice
                        && choice.PlayerId == actor && current.PlayerZones[actor].LegendZone.Contains(choice.SourceObjectId!)
                        && choice.LegalObjectIds.Contains(unit) && current.PriorityPlayerId is null,
                        "The mandatory legend target belongs to the actual captured legend after unit entry.");
                else
                {
                    var legend = current.StackItems.Single(i => i.UnitPlayMight is not null);
                    Check(legend.ControllerId == actor && legend.TargetObjectIds.SequenceEqual([unit])
                        && legend.TargetGenerations![unit] == current.CardObjects[unit].ObjectGeneration,
                        "The independent legend target is bound before response, separately from the printed skill.");
                }
                var frame = await Cold();
                checkpoints.Add(new { stage, commandCount = index, commands = frame.Commands.Count,
                    events = frame.Events.Count, fullOriginalReplay = true, sevenSqlTablesUnchanged = true });
                if (index == orderBoundary)
                {
                    var own = current.TriggerQueue.Single(t => t.PermanentPlayStackItemId is not null);
                    var forged = current with { TriggerQueue = current.TriggerQueue.Select(t => t.TriggerId == own.TriggerId
                        ? t with { PermanentPlayStackItemId = null } : t).ToArray() };
                    await RejectForgeries([("missing-rengar-companion-pointer", forged, false, "invalid deferred permanent play ordering origin")]);
                }
                if (index == confirmedBoundary)
                {
                    var legend = current.StackItems.Single(i => i.UnitPlayMight is not null);
                    var forged = current with { StackItems = current.StackItems.Select(i => i.StackItemId == legend.StackItemId
                        ? i with { TargetObjectIds = [enemy], TargetGenerations = new Dictionary<string, long> {
                            [enemy] = current.CardObjects[enemy].ObjectGeneration } } : i).ToArray() };
                    await RejectForgeries([("coordinated-rengar-target-swap", forged, true, null)]);
                }
                nextColdFamily = "rengar"; originalNextAfterCold = true;
            }
            if (index == boundary || index == boundary + 1)
            {
                var item = current.StackItems.Single();
                Check(item.SourceObjectId == unit && item.SourceConfirmed && item.PermanentPlaySource is { } binding
                    && binding.ObjectId == unit && binding.Generation == current.CardObjects[unit].ObjectGeneration
                    && item.ControllerId == actor && current.CardObjects[unit].OwnerId == actor
                    && current.CardObjects[unit].ControllerId == opponent
                    && current.ObjectLocations[unit].BattlefieldObjectId == data.GetProperty("expectedBattlefield").GetString()
                    && current.ObjectLocations[enemy].BattlefieldObjectId == data.GetProperty("expectedBattlefield").GetString()
                    && current.PlayerZones[opponent].Battlefields.Contains(unit) && current.PlayerZones[opponent].Battlefields.Contains(enemy),
                    "Retain the real pending Here source, old ability controller, new permanent controller and same battlefield.");
                var frame = await Cold();
                checkpoints.Add(new { stage = "here-response", commandCount = index, commands = frame.Commands.Count, events = frame.Events.Count,
                    fullOriginalReplay = true, sevenSqlTablesUnchanged = true, source = item.PermanentPlaySource,
                    abilityController = actor, permanentController = opponent, actualSharedBattlefield = current.ObjectLocations[unit].BattlefieldObjectId });
                if (index == boundary) await RejectForgeries([
                    ("missing-here-binding", current with { StackItems = [item with { PermanentPlaySource = null }] }, false,
                        "invalid battlefield-only permanent play trigger origin"),
                    ("coordinated-here-controller", current with { StackItems = [item with { ControllerId = opponent }] }, true, null)]);
                nextColdFamily = "here"; originalNextAfterCold = true;
            }
        }
        Check(checkpoints.Count == 5 && attacks.Count == 4 && hereOriginalNext == 2 && rengarOriginalNext == 3 && !originalNextAfterCold,
            "Keep both original Here boundaries/next commands/forgeries and add all three Rengar boundaries/next commands and two independent forgeries.");
        var final = await Cold();
        Check(current.PlayerZones[opponent].Battlefields.Contains(enemy) && current.CardObjects[enemy].Damage == 2
            && current.PlayerZones[opponent].Battlefields.Contains(unit)
            && current.CardObjects[unit].OwnerId == actor && current.CardObjects[unit].ControllerId == opponent,
            "Printed damage2 reaches the original enemy on the same battlefield after control changes.");
        var damage = final.Events.Where(e => e.Event.Kind == "DAMAGE_APPLIED")
            .Select(e => JsonSerializer.SerializeToElement(e.Event.Payload)).Single(e => e.GetProperty("targetObjectId").GetString() == enemy);
        Check(damage.GetProperty("damage").GetInt32() == 2, "The official printed damage is2.");
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_HERE_ABILITY_CONTROLLER_COLD_FORGERY_ORIGINAL_REPLAY_PASS", executedAtUtc = DateTimeOffset.UtcNow,
            room, originalExplicitSeed = 0, commands = final.Commands.Count, events = final.Events.Count,
            checkpoints, attacks, originalNextCommandsMatched = hereOriginalNext, rengarOriginalNextCommandsMatched = rengarOriginalNext,
            finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), databaseRoomRetained = true,
            scope = "Fresh blank opening with legal Rengar/Renata decks and actual mandatory independent legend targeting/shared ordering. Three Rengar plus both original Here cold boundaries and original next commands, damage2 after Takeover, missing-pointer/binding and coordinated target/controller forgery rejection, exact seven-table restoration and original full replay. Prior83-command evidence remains unchanged. Token lifecycle SQL, base-positive movement, whole cards, other consumers, current native and physical Windows remain separate."
        }, json));
        Console.WriteLine("POSTGRES_HERE_ABILITY_CONTROLLER_PASS: blank opening,5 cold boundaries,5 original next commands,4 forgery rejections,mandatory Rengar/shared ordering,damage2 and full original replay.");

        async Task<MatchRecoveryFrame> Cold()
        {
            var before = await Digest();
            var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL frame.");
            Check(frame.IsConsistent && frame.OpeningSeed == 0 && frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Seating }
                && frame.ReplayInitialState.CardObjects.Count == 0 && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                "Exact original blank opening and current state must survive: " + string.Join("; ", frame.ValidationErrors));
            Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Full original replay must match.");
            var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
            foreach (var player in current.Seats.Keys)
                Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))),
                    "Both original player snapshots and prompts must survive.");
            Check(await Digest() == before, "Cold recovery cannot rewrite any accepted SQL row.");
            session = restored; return frame;
        }
        async Task RejectForgeries(IReadOnlyList<(string Scenario, MatchState Forged, bool Coordinated, string? StructuralError)> cases)
        {
            var clean = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing clean SQL frame.");
            var originalLog = JsonSerializer.Serialize(new { clean.Commands, clean.Events }, json); var originalDigest = await Digest();
            var saved = new List<(string Table, long Id, string? Player, string Payload)>();
            await using (var connection = await source.OpenConnectionAsync())
                foreach (var (table, tickColumn) in new[] { ("state_snapshots", "state_tick"), ("snapshots", "snapshot_tick"), ("action_prompts", "prompt_tick") })
                {
                    await using var read = new NpgsqlCommand($"select id,{(table == "state_snapshots" ? "null::text" : "player_id")},payload::text from {table} where match_id=@room and {tickColumn}=@tick", connection);
                    read.Parameters.AddWithValue("room", room); read.Parameters.AddWithValue("tick", current.Tick);
                    await using var rows = await read.ExecuteReaderAsync();
                    while (await rows.ReadAsync()) saved.Add((table, rows.GetInt64(0), rows.IsDBNull(1) ? null : rows.GetString(1), rows.GetString(2)));
                }
            Check(saved.Count == 5, "Retain one authoritative state and both original player snapshots/prompts.");
            foreach (var (scenario, forged, coordinated, structuralError) in cases)
            {
                try
                {
                    var snapshots = ResolutionResult.BuildSnapshots(forged); var prompts = ResolutionResult.BuildPrompts(forged);
                    await using (var connection = await source.OpenConnectionAsync())
                    await using (var transaction = await connection.BeginTransactionAsync())
                    {
                        foreach (var row in saved) await Set(connection, transaction, row.Table, row.Id, row.Table == "state_snapshots"
                            ? JsonSerializer.Serialize(forged, json) : row.Table == "snapshots"
                            ? JsonSerializer.Serialize(snapshots[row.Player!], json) : JsonSerializer.Serialize(prompts[row.Player!], json));
                        await transaction.CommitAsync();
                    }
                    var attackedDigest = await Digest();
                    var bad = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing forged SQL frame.");
                    Check(JsonSerializer.Serialize(new { bad.Commands, bad.Events }, json) == originalLog, "Accepted commands/events cannot be rewritten.");
                    Check(coordinated ? bad.IsConsistent : !bad.IsConsistent
                        && (structuralError is null || bad.ValidationErrors.Contains(structuralError)),
                        $"{scenario}: missing binding/pointer fails independent structure; coordinated valid shapes reach the original action-log audit.");
                    var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged trigger continuation was accepted."); }
                    catch (MatchSessionException error)
                    {
                        Check(error.Code == ErrorCodes.RecoveryInconsistent && (!coordinated || error.Message.Contains("action-log audit failed", StringComparison.Ordinal)), error.Message);
                    }
                    Check(await registry.FindAsync(room, default) is null && await Digest() == attackedDigest,
                        "Rejection cannot cache a session or write any SQL row.");
                    attacks.Add(new { scenario, coordinatedWrongAbilityController = scenario == "coordinated-here-controller",
                        coordinatedTargetSwap = scenario == "coordinated-rengar-target-swap", rebuiltBothPlayerViews = true,
                        structuralValidationPassed = bad.IsConsistent, productionRegistryRejected = true,
                        acceptedHistoryUnchanged = true, allSqlRowsUnchangedByRejection = true });
                }
                finally
                {
                    await using var connection = await source.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync();
                    foreach (var row in saved) await Set(connection, transaction, row.Table, row.Id, row.Payload);
                    await transaction.CommitAsync();
                }
                Check(await Digest() == originalDigest, "Restore all seven original table contents byte for byte."); await Cold();
            }
        }
        async Task<string> Digest()
        {
            var rows = new StringBuilder(); await using var connection = await source.OpenConnectionAsync();
            foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            {
                await using var query = new NpgsqlCommand($"select coalesce(string_agg(row_to_json(t)::text, E'\\n' order by row_to_json(t)::text),'') from {table} t where match_id=@room", connection);
                query.Parameters.AddWithValue("room", room); rows.Append(table).Append('\n').Append((string)(await query.ExecuteScalarAsync())!).Append('\n');
            }
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows.ToString()))).ToLowerInvariant();
        }
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string payload)
    {
        await using var command = new NpgsqlCommand($"update {table} set payload=cast(@payload as jsonb) where id=@id", connection, transaction);
        command.Parameters.AddWithValue("payload", payload); command.Parameters.AddWithValue("id", id);
        Check(await command.ExecuteNonQueryAsync() == 1, "Exactly one original SQL row must be updated.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
