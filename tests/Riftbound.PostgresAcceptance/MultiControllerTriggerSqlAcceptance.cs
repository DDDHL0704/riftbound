using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class MultiControllerTriggerSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "multi-controller-trigger-openings.json"));
        using var fixture = JsonDocument.Parse(bytes);
        var data = fixture.RootElement;
        Check(data.GetProperty("openingSeed").GetInt64() == 0, "Retain the explicit original test seed0.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var results = new List<object>();
        foreach (var recipe in data.GetProperty("branches").EnumerateArray())
        {
            var caster = recipe.GetProperty("caster").GetString()!;
            var secondPlayer = caster == "P1" ? "P2" : "P1";
            var room = "postgres-controller-order-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, data.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial.Tick == 0 && initial.CardObjects.Count == 0, "A real blank opening, without injected cards or an imported midgame.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false;
            var attacks = new List<object>(); var checkpoints = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("controller-order-" + player), default);
            foreach (var entry in recipe.GetProperty("acceptedCommands").EnumerateArray())
                await Submit(entry.GetProperty("playerId").GetString()!, entry.GetProperty("clientIntentId").GetString()!, JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject());
            Check(count == recipe.GetProperty("expectedPrefixCommands").GetInt32() && current.TurnPlayerId == caster
                && current.TriggerQueue.Count == 4 && current.TriggerQueue.Select(t => t.EnqueuedAtTick).Distinct().Count() == 1
                && initial.Seats.Keys.All(player => current.TriggerQueue.Count(t => t.ControllerId == player) == 2), "Reach both owners' simultaneous groups through the exact ordinary prefix.");
            await Cold(); checkpoints.Add(new { stage = "both-controller-decisions", commands = count }); afterCold = true;
            await Submit(caster, "first-controller-order", JsonSerializer.SerializeToNode(Order(caster, caster == "P1"), json)!.AsObject());
            Check(current.TriggerQueue.Count == 4 && current.StackItems.Count == 0
                && current.TriggerQueue.All(t => t.ControllerOrderConfirmed == (t.ControllerId == caster))
                && !session.PromptFor(caster).Actionable
                && session.PromptFor(secondPlayer).Candidates!.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled), "Only the second controller now owns a decision; nothing resolves before both choose.");
            await Cold(); checkpoints.Add(new { stage = "first-confirmed-second-pending", commands = count });
            var firstId = current.TriggerQueue.First(t => t.ControllerId == caster).TriggerId;
            var swapped = current.TriggerQueue.ToArray();
            var own = Enumerable.Range(0, swapped.Length).Where(index => swapped[index].ControllerId == caster).ToArray();
            (swapped[own[0]], swapped[own[1]]) = (swapped[own[1]], swapped[own[0]]);
            await RejectForgeries([
                ("partial-controller-confirmation", current with { TriggerQueue = current.TriggerQueue.Select(t => t.TriggerId == firstId ? t with { ControllerOrderConfirmed = false } : t).ToArray() }, false, "invalid trigger controller order confirmations"),
                ("wrong-first-controller", current with { TriggerQueue = current.TriggerQueue.Select(t => t with { ControllerOrderConfirmed = t.ControllerId == secondPlayer }).ToArray() }, false, "invalid trigger controller order confirmations"),
                ("all-controllers-confirmed-with-queue", current with { TriggerQueue = current.TriggerQueue.Select(t => t with { ControllerOrderConfirmed = true }).ToArray() }, false, "invalid trigger controller order confirmations"),
                ("coordinated-confirmation-cleared", current with { TriggerQueue = current.TriggerQueue.Select(t => t with { ControllerOrderConfirmed = false }).ToArray() }, true, null),
                ("coordinated-confirmed-order-replaced", current with { TriggerQueue = swapped }, true, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1, "CN", "zh-CN", RuleInputIdentity.Current.AuthoritySha256, RuleInputIdentity.Current.CatalogSha256, new string('a', 64)) }, false, null)]);
            afterCold = true;
            var second = Order(secondPlayer, caster == "P2");
            await Submit(secondPlayer, "second-controller-order", JsonSerializer.SerializeToNode(second, json)!.AsObject());
            Check(current.TriggerQueue.Count == 0 && current.StackItems.AsEnumerable().Reverse().Select(item => item.StackItemId)
                .SequenceEqual(second.OrderedTriggerIds!.Select(id => "ordered-" + id)), "Both independently chosen own orders determine the combined resolution stack.");
            await Cold(); checkpoints.Add(new { stage = "combined-stack-after-both-choices", commands = count }); afterCold = true;
            for (var step = 0; step < 12 && current.StackItems.Count > 0; step++)
                await Submit(current.PriorityPlayerId!, "controller-skill-response-" + step, JsonSerializer.SerializeToNode(new PassPriorityCommand(), json)!.AsObject());
            Check(current.StackItems.Count == 0 && initial.Seats.Keys.All(player => current.PlayerZones[player].Base.Count(id => current.CardObjects[id].TokenFactoryCardNo is not null) == 2), "Resolve all four independently captured Viktor skills exactly once.");
            var final = await Cold(); checkpoints.Add(new { stage = "complete-original-blank-replay", commands = count });
            Check(coldCount == 10 && nextMatched == 3 && attacks.Count == 6, "Retain ten actual cold restores, three original next commands and all six independent attacks.");
            results.Add(new { caster, room, commands = final.Commands.Count, events = final.Events.Count, coldRestores = coldCount,
                originalNextCommandsMatched = nextMatched, checkpoints, attacks, finalHash = MatchStateHasher.Hash(current),
                originalBlankSeed = 0, completeOriginalReplay = true, productionRegistryRestored = true, ruleInputs = current.RuleInputs });
            OrderTriggersCommand Order(string player, bool reverse)
            {
                var candidate = session.PromptFor(player).Candidates!.Single(c => c.Action == CommandTypes.OrderTriggers && c.Enabled);
                var metadata = JsonSerializer.SerializeToElement(candidate.Metadata, json);
                var ids = metadata.GetProperty("orderedTriggerIds").Deserialize<string[]>()!;
                var own = Enumerable.Range(0, ids.Length).Where(index => current.TriggerQueue.Single(t => t.TriggerId == ids[index]).ControllerId == player).ToArray();
                Check(own.Length == 2 && metadata.GetProperty("reorderableTriggerIds").Deserialize<string[]>()!.ToHashSet().SetEquals(own.Select(index => ids[index])), "The server only exposes the current controller's reorderable pair.");
                if (reverse) (ids[own[0]], ids[own[1]]) = (ids[own[1]], ids[own[0]]);
                return new(OrderedTriggerIds: ids);
            }
            async Task<ResolutionResult> Submit(string actor, string intent, JsonObject fields)
            {
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var command = GameCommandJsonMapper.Map(raw);
                var expected = afterCold ? await engine.ResolveAsync(current, new(intent, actor, command.CmdType), command, default) : null;
                if (command is SubmitDeckCommand deck)
                {
                    var catalog = await OfficialCardCatalog.LoadDefaultAsync();
                    foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, format).IsValid, "Both original forty-card decks must remain legal.");
                }
                var result = command switch {
                    SubmitDeckCommand submittedDeck => await session.SubmitDeckAsync(actor, intent, submittedDeck, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                    _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)), "The exact original next command, state and events must match after SQL recovery.");
                    nextMatched++;
                }
                afterCold = false; current = result.State; count++; return result;
            }
            async Task<MatchRecoveryFrame> Cold()
            {
                var before = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual SQL frame.");
                Check(frame.IsConsistent && frame.Commands.Count == count && frame.OpeningSeed == 0
                    && frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Seating } && frame.ReplayInitialState.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current), "Original blank state/log/input consistency: " + string.Join(';', frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Full original replay must match.");
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players).GetOrCreateAsync(room, default);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))), "Both private player views must survive actual production registry recovery.");
                Check(await Digest() == before, "Recovery cannot rewrite any original seven-table row.");
                session = restored; coldCount++; return frame;
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
                        $"{scenario}: invalid permissions fail independent structure; coordinated valid shapes reach the original action-log audit.");
                    var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players);
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged controller order was accepted."); }
                    catch (MatchSessionException error)
                    {
                        Check(error.Code == ErrorCodes.RecoveryInconsistent && (!coordinated || error.Message.Contains("action-log audit failed", StringComparison.Ordinal)), error.Message);
                    }
                    Check(await registry.FindAsync(room, default) is null && await Digest() == attackedDigest,
                        "Rejection cannot cache a session or write any SQL row.");
                    attacks.Add(new { scenario, rebuiltBothPlayerViews = true,
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
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_MULTI_CONTROLLER_TRIGGER_ORDER_PASS", results,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            scope = "Two genuine ordinary blank openings, both deck formats, each controller's own untargeted simultaneous death-observer choices, persisted handoff, actual production registry cold recovery, independent marker/identity and coordinated original-history attacks. Other target/cost/placement consumers and visible native/Windows remain separately OPEN." }, json));
        Console.WriteLine("POSTGRES_MULTI_CONTROLLER_TRIGGER_ORDER_PASS: two ordinary branches,20 cold restores,6 original next commands,12 forgery rejections and complete original replays.");
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string payload)
    {
        await using var command = new NpgsqlCommand($"update {table} set payload=cast(@payload as jsonb) where id=@id", connection, transaction);
        command.Parameters.AddWithValue("payload", payload); command.Parameters.AddWithValue("id", id);
        Check(await command.ExecuteNonQueryAsync() == 1, "Exactly one original SQL row must be updated.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
