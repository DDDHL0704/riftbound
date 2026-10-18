using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class EmptyObservedDeckRepeatSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "empty-observed-deck-repeat-openings.json")));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var results = new List<object>();
        foreach (var data in fixture.RootElement.GetProperty("branches").EnumerateArray())
        foreach (var draw in data.GetProperty("cardNo").GetString() == "SFD·122/221" ? new[] { true } : new[] { true, false })
        {
            var card = data.GetProperty("cardNo").GetString()!;
            var firstIds = data.GetProperty("expectedFirstView").Deserialize<string[]>()!;
            var secondIds = data.GetProperty("expectedSecondView").Deserialize<string[]>()!;
            var sourceId = data.GetProperty("spellSource").GetString()!;
            var room = "postgres-empty-observed-repeat-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Start from the original ordinary blank state and seed0.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false;
            var attacks = new List<object>(); var checkpoints = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("empty-observed-repeat-" + player), default);
            var entries = data.GetProperty("acceptedCommands").EnumerateArray().ToArray();
            foreach (var entry in entries)
            {
                if (count == entries.Length - 1) { await Cold(); checkpoints.Add(new { stage = "before-last-banish", commands = count }); afterCold = true; }
                await Submit(entry.GetProperty("playerId").GetString()!, "prefix-" + count, JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject());
            }
            var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Paid repeat must reach its second ordinary view.");
            Check(count == data.GetProperty("expectedPrefixCommands").GetInt32() && choice.PlayerId == "P1" && choice.ChoiceWindow == "DECK_EFFECT"
                && choice.SourceObjectId == sourceId && choice.ContextObjectIds.SequenceEqual(secondIds) && choice.LegalObjectIds.SequenceEqual(secondIds)
                && choice.RequiredCount == (card == "SFD·122/221" ? 1 : 0) && choice.MaxCount == 1 && choice.DeckContext?.ExecutionIndex == 1
                && current.PendingEffectPlay is null && firstIds.All(id => !current.PlayerZones["P1"].MainDeck.Contains(id))
                && current.ObservedDeckPlays!.Select(observed => observed.Card.ObjectId).Order().SequenceEqual(firstIds.Order()), "Independent second-view identity, mandatory/optional permission and delayed observed-play order.");
            foreach (var id in secondIds)
                Check(!JsonSerializer.Serialize(session.SnapshotFor("P2")).Contains(id) && !JsonSerializer.Serialize(session.PromptFor("P2")).Contains(id), "Second private view leaked to opponent.");
            var handCount = current.PlayerZones["P1"].Hand.Count; var tail = current.PlayerZones["P1"].MainDeck.Skip(secondIds.Length).ToArray();
            await Cold(); checkpoints.Add(new { stage = "pending-second-view", commands = count });
            var item = current.StackItems.Single(slot => slot.SourceObjectId == sourceId);
            MatchState WithItem(StackItemState altered) => current with { StackItems = current.StackItems.Select(slot => slot.StackItemId == item.StackItemId ? altered : slot).ToArray() };
            var reordered = secondIds.Reverse().ToArray();
            var coordinated = current with {
                PendingCardChoice = choice with { ContextObjectIds = reordered, LegalObjectIds = reordered },
                PlayerZones = current.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1" ? pair.Value with { MainDeck = reordered.Concat(tail).ToArray() } : pair.Value) };
            await RejectForgeries([
                ("earlier-completed-count", WithItem(item with { CompletedDeckExecutions = 0 }), false, "invalid private deck choice continuation"),
                ("later-completed-count", WithItem(item with { CompletedDeckExecutions = 2 }), false, "invalid private deck choice continuation"),
                ("premature-item-completion", WithItem(item with { DeckChoiceCompleted = true }), false, "invalid deck resolution continuation count"),
                ("maximum-two", current with { PendingCardChoice = choice with { MaxCount = 2 } }, false, "invalid private deck choice permissions"),
                ("coordinated-second-view-order", coordinated, true, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1, "CN", "zh-CN", RuleInputIdentity.Current.AuthoritySha256, RuleInputIdentity.Current.CatalogSha256, new string('a', 64)) }, false, null)]);
            afterCold = true;
            await Submit("P1", "original-second-view", JsonSerializer.SerializeToNode(new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? [secondIds[0]] : []), json)!.AsObject());
            Check(current.PendingCardChoice is null && current.PlayerZones["P1"].Hand.Count == handCount + (draw ? 1 : 0)
                && current.PlayerZones["P1"].MainDeck.Take(tail.Length).SequenceEqual(tail)
                && current.PlayerZones["P1"].MainDeck.Skip(tail.Length).Order().SequenceEqual(secondIds.Where(id => !draw || id != secondIds[0]).Order())
                && current.PlayerZones["P1"].Graveyard.Count(id => id == sourceId) == 1, "Exactly the second view is selected/recycled and one paid spell completes.");
            await Cold(); checkpoints.Add(new { stage = "pending-observed-play", commands = count }); afterCold = true;
            for (var i = 0; i < firstIds.Length; i++)
            {
                var offered = current.PendingEffectPlay ?? throw new InvalidOperationException("Missing deferred observed-unit play offer.");
                await Submit("P1", "decline-observed-" + i, JsonSerializer.SerializeToNode(new ChooseCardsCommand(offered.ChoiceId, "EFFECT_PLAY", []), json)!.AsObject());
            }
            Check(current.PendingEffectPlay is null && current.StackItems.Count == 0 && current.TriggerQueue.Count == 0
                && current.PlayerZones["P1"].Banished.Order().SequenceEqual(firstIds.Order()), "All deferred play offers resolve after both paid executions.");
            var final = await Cold(); checkpoints.Add(new { stage = "complete-original-replay", commands = count });
            Check(count == entries.Length + 1 + firstIds.Length && coldCount == 10 && nextMatched == 3 && attacks.Count == 6, "Retain all real SQL cold/next/security checkpoints.");
            var originalFile = Path.Combine(output, "ordinary-empty-view-" + firstIds.Length + "-" + draw.ToString().ToLowerInvariant() + ".json.gz");
            Check(!File.Exists(originalFile), "Never overwrite the original SQL recovery frame.");
            await using (var file = File.Create(originalFile)) await using (var compressed = new GZipStream(file, CompressionLevel.SmallestSize))
                await JsonSerializer.SerializeAsync(compressed, final, json);
            results.Add(new { card, draw, room, commands = final.Commands.Count, events = final.Events.Count, coldRestores = coldCount,
                originalNextCommandsMatched = nextMatched, attacks, checkpoints, originalBlankSeed = 0, completeOriginalReplay = true,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs, validatedFormats = data.GetProperty("validatedFormats").Deserialize<string[]>() });

            async Task<ResolutionResult> Submit(string actor, string intent, JsonObject fields)
            {
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var command = GameCommandJsonMapper.Map(raw);
                var expected = afterCold ? await engine.ResolveAsync(current, new(intent, actor, command.CmdType), command, default) : null;
                if (command is SubmitDeckCommand deck)
                {
                    var catalog = await OfficialCardCatalog.LoadDefaultAsync();
                    var list = new OfficialDecklist(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
                    Check(OfficialDeckValidator.Validate(list, catalog, OfficialDeckFormat.CoreRules).IsValid, "Core rules legal ordinary QA deck required.");
                    var standard = OfficialDeckValidator.Validate(list, catalog, OfficialDeckFormat.ChinaStandard20260724);
                    Check(card == "SFD·122/221" ? !standard.IsValid && standard.Errors.Any(error => error.Contains("预判攻势", StringComparison.Ordinal)) : standard.IsValid,
                        "Core122 is China-standard banned; both032 must pass both formats. Do not relabel format legality.");
                }
                var result = command switch {
                    SubmitDeckCommand submittedDeck => await session.SubmitDeckAsync(actor, intent, submittedDeck, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                    _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)), "Original next command/state/events must match after cold SQL recovery."); nextMatched++;
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
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged deck continuation was accepted."); }
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
            status = "REAL_POSTGRES_ORDINARY_EMPTY_OBSERVED_REPEAT_SECOND_VIEW_COLD_REPLAY_TAMPER_PASS", results,
            scope = "CN text, core-rule SFD122 and both-format UNL032 ordinary blank seed0 branches; not native/physical Windows or whole-card certification."
        }, json));
        Console.WriteLine($"POSTGRES_EMPTY_OBSERVED_REPEAT_PASS: {results.Count} original blank branches with second-view, deferred play, cold recovery, original next commands and tamper rejection");
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string value)
    {
        await using var update = new NpgsqlCommand($"update {table} set payload=@payload::jsonb where id=@id", connection, transaction);
        update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("payload", value);
        Check(await update.ExecuteNonQueryAsync() == 1, "Update exactly the retained original row.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
