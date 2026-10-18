using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class CopiedLastBreathInsightSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "copied-last-breath-insight-openings.json")));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var results = new List<object>();
        foreach (var row in fixture.RootElement.GetProperty("branches").EnumerateArray())
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var recycleFirst in new[] { false, true })
        {
            var branch = row.GetProperty("branch").GetString()!; var songs = row.GetProperty("songs").GetInt32();
            var room = "postgres-copied-last-breath-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original ordinary blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false;
            var attacks = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("last-breath-sql-" + player), default);
            var entries = row.GetProperty("acceptedCommands").EnumerateArray().ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                var command = GameCommandJsonMapper.Map(entries[i].GetProperty("rawCommand"));
                if (i == 6 || command is AssembleEquipmentCommand or MoveUnitCommand or PlayCardCommand { CardNo: "OGS·003/024" }) { await Cold(); afterCold = true; }
                await Command(entries[i].GetProperty("playerId").GetString()!, "prefix-" + count, command);
            }
            Check(current.TriggerQueue.Count == songs + 1 && current.TriggerQueue.Count(t => t.InsightContext?.Origin is not null) == songs
                && current.TriggerQueue.All(t => t.ControllerId == "P1" && t.InsightContext is { Kind: "LAST_BREATH", Count: 2 })
                && current.TurnPlayerId == "P2", "Original and copied LastBreaths retain their off-turn controller.");
            await Cold(); afterCold = true;
            var copied = current.TriggerQueue.First(t => t.InsightContext?.Origin is not null);
            var printed = current.TriggerQueue.Single(t => t.InsightContext?.Origin is null);
            var captured = copied.InsightContext!; var origin = captured.Origin!;
            MatchState Forged(InsightTriggerContext changed) => current with { TriggerQueue = current.TriggerQueue.Select(t => t.TriggerId == copied.TriggerId
                ? t with { InsightContext = changed } : t).ToArray() };
            var swapped = current with { TriggerQueue = current.TriggerQueue.Select(t => t.TriggerId == copied.TriggerId
                ? t with { InsightContext = captured with { Origin = null } } : t.TriggerId == printed.TriggerId
                    ? t with { InsightContext = t.InsightContext! with { Origin = origin } } : t).ToArray() };
            await RejectForgeries([
                ("unknown-copied-equipment", Forged(captured with { Origin = origin with { Equipment = origin.Equipment with { ObjectId = "UNKNOWN" } } }), false, null),
                ("future-copy-revision", Forged(captured with { Origin = origin with { Revision = 99 } }), false, null),
                ("future-copy-capture", Forged(captured with { Origin = origin with { CapturedTick = current.Tick + 1 } }), false, null),
                ("future-source-generation", Forged(captured with { SourceGeneration = 99 }), false, null),
                ("false-token-factory", Forged(captured with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo }), false, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1, "CN", "zh-CN", RuleInputIdentity.Current.AuthoritySha256,
                    RuleInputIdentity.Current.CatalogSha256, new string('a', 64)) }, false, null),
                ("coordinated-copied-origin-removal", Forged(captured with { Origin = null }), true, null),
                ("coordinated-swapped-printed-and-copied-bindings", swapped, true, null)]);
            await Command("P1", "original-order", new OrderTriggersCommand(OrderedTriggerIds: current.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
            await Cold(); afterCold = true;
            var originalHand = current.PlayerZones["P1"].Hand.ToArray(); var originalDeck = current.PlayerZones["P1"].MainDeck.ToArray();
            for (var i = 0; i <= songs; i++)
            {
                var deck = current.PlayerZones["P1"].MainDeck.ToArray(); var top = deck.Take(2).ToArray();
                await Command(current.PriorityPlayerId!, "original-pass-" + count, new PassPriorityCommand());
                await Command(current.PriorityPlayerId!, "original-pass-" + count, new PassPriorityCommand());
                while (current.PendingRuleChoice is { Request.Kind: "DECK_OBSERVATION_BANISH" } observation)
                    await Command("P1", "original-observation-keep-" + count, new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
                var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Missing original private LastBreath Insight choice.");
                Check(choice.PlayerId == "P1" && choice.ChoiceWindow == "INSIGHT" && choice.RequiredCount == 0 && choice.MaxCount == 2
                    && choice.ContextObjectIds.SequenceEqual(top), "Each independent LastBreath reads the current original top two.");
                await Cold(); afterCold = true;
                var selected = recycleFirst && i == 0 ? top : Array.Empty<string>();
                await Command("P1", "original-choice-" + count, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
                await Cold(); afterCold = true;
                var kept = top.Except(selected).Reverse().ToArray();
                if (kept.Length == 2)
                {
                    var order = current.PendingCardChoice ?? throw new InvalidOperationException("Missing original private kept-card order.");
                    Check(order.ChoiceWindow == "INSIGHT_ORDER", "Original independent keep ordering remains pending.");
                    await Command("P1", "original-private-order-" + count, new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, kept));
                    await Cold(); afterCold = true;
                }
                var actual = current.PlayerZones["P1"].MainDeck;
                Check(actual.Take(actual.Count - selected.Length).SequenceEqual(kept.Concat(deck.Skip(2)))
                    && actual.Skip(actual.Count - selected.Length).Order().SequenceEqual(selected.Order()), "Keep order and recycle suffix respect each current deck.");
            }
            Check(current.PlayerZones["P1"].Hand.SequenceEqual(originalHand) && current.PlayerZones["P1"].MainDeck.Order().SequenceEqual(originalDeck.Order()),
                "Insight neither draws nor loses cards.");
            Check(current.StackItems.Count == 0 && current.TriggerQueue.Count == 0 && current.PendingCardChoice is null
                && current.TurnPlayerId == "P2" && current.Phase == MatchPhases.Main && current.TimingState == TimingStates.NeutralOpen,
                "Independent LastBreaths restore the original opponent main turn.");
            await Command("P2", "original-next-end-turn", new EndTurnCommand());
            Check(current.TurnPlayerId == "P1" && current.Phase == MatchPhases.Main, "Original next EndTurn advances normally.");
            var final = await Cold();
            Check(count == entries.Length + 2 + 4 * (songs + 1) - (recycleFirst ? 1 : 0) && attacks.Count == 8,
                "Retain every prefix and independent LastBreath command and forgery.");
            var path = Path.Combine(output, branch + "-" + format + "-" + recycleFirst + "-original-frame.json.gz");
            Check(!File.Exists(path), "Never overwrite an original SQL frame.");
            await using (var file = File.Create(path)) await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize))
                await JsonSerializer.SerializeAsync(zip, final, json);
            results.Add(new { branch, songs, format = format.ToString(), recycleFirst, room,
                commands = count, events = final.Events.Count, coldRestores = coldCount, originalNextCommandsMatched = nextMatched,
                attacks, originalBlankSeed = 0, originalFullReplayMatched = true, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs });

            Task<ResolutionResult> Command(string actor, string intent, GameCommand command)
                => Submit(actor, intent, JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject());
            async Task<ResolutionResult> Submit(string actor, string intent, JsonObject fields)
            {
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var command = GameCommandJsonMapper.Map(raw);
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid,
                            "Both original formats require a legal deck.");
                var expected = afterCold ? await engine.ResolveAsync(current, new(intent, actor, command.CmdType), command, default) : null;
                var result = command switch { SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, intent, submitted, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default), _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)),
                        "The original next command/state/events match after matching-format cold recovery."); nextMatched++;
                }
                current = result.State; afterCold = false; count++; return result;
            }
            async Task<MatchRecoveryFrame> Cold()
            {
                var beforeDigest = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing original SQL frame.");
                Check(frame.IsConsistent && frame.Commands.Count == count && frame.OpeningSeed == 0
                    && frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Seating } && frame.ReplayInitialState.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "Original blank state and complete accepted history stay consistent: " + string.Join(';', frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "All original accepted commands/events replay exactly.");
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players, options).GetOrCreateAsync(room, default);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))), "Both original private player views and prompts survive recovery.");
                Check(await Digest() == beforeDigest, "Cold reads/replay cannot write any original seven-table row.");
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
                    var registry = new InMemoryMatchSessionRegistry(engine, journal, store, players, options);
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged copied LastBreath Insight state was accepted."); }
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
            status = "REAL_POSTGRES_ORDINARY_COPIED_LAST_BREATH_INSIGHT_MATCHING_FORMAT_CHOICES_COLD_ORIGINAL_REPLAY_TAMPER_PASS", results }, json));
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string value)
    {
        await using var update = new NpgsqlCommand($"update {table} set payload=@payload::jsonb where id=@id", connection, transaction);
        update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("payload", value);
        Check(await update.ExecuteNonQueryAsync() == 1, "Update exactly the retained original row.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
