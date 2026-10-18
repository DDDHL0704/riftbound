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

internal static class RavenbloomPutIntoHandSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "ravenbloom-put-hand-openings.json")));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var results = new List<object>();
        foreach (var data in fixture.RootElement.GetProperty("branches").EnumerateArray())
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        {
            var spell = data.GetProperty("spell").GetBoolean(); var branch = spell ? "spell" : "unit";
            var battlefield = data.GetProperty("battlefield").GetString()!;
            var top = data.GetProperty("expectedRevealedObjectId").GetString()!;
            var room = "postgres-ravenbloom-put-hand-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room,
                new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original ordinary blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false;
            var malformedTargetIngressRejections = 0;
            var attacks = new List<object>(); var checkpoints = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("ravenbloom-" + player), default);
            MatchState? beforeReveal = null; ResolutionResult? last = null;
            var entries = data.GetProperty("acceptedCommands").EnumerateArray().ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i]; var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
                if (i == 6 || command is PlayCardCommand { CardNo: "UNL-074/219" } || command is DeclareBattleCommand)
                {
                    await Cold(); afterCold = true;
                    checkpoints.Add(new { stage = command.CmdType, commands = count });
                }
                if (command is DeclareBattleCommand)
                {
                    beforeReveal = current;
                    Check(current.DrawLedger.Counts.GetValueOrDefault("P2") == 1 && current.PlayerZones["P2"].MainDeck.First() == top,
                        "One actual off-turn draw precedes the original Ravenbloom reveal.");
                    await RejectMalformedTargets(entry.GetProperty("playerId").GetString()!,
                        JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject());
                }
                last = await Submit(entry.GetProperty("playerId").GetString()!, "prefix-" + count,
                    JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject());
            }
            var before = beforeReveal ?? throw new InvalidOperationException("Missing original pre-reveal state.");
            var events = last?.Events ?? throw new InvalidOperationException("Missing actual declaration events.");
            Check(count == 31 && current.DrawLedger.Counts.GetValueOrDefault("P2") == 1
                && current.PendingCardChoice is null && current.PendingEffectPlay is null
                && current.StackItems.Count == 0 && current.TriggerQueue.Count == 0
                && current.Phase == MatchPhases.Main && current.TimingState == TimingStates.NeutralOpen
                && current.FocusPlayerId is null && current.PriorityPlayerId is null,
                "Ravenbloom never counts as a draw, queues Frozen Gem, or leaves a false target prompt.");
            Check(!events.Any(ev => ev.Kind == "CARD_DRAWN"), "The printed put-into-hand action cannot create a draw event.");
            if (spell)
            {
                var added = events.Single(ev => ev.Kind == "CARD_ADDED_TO_HAND");
                Check(added.Payload.GetValueOrDefault("sourceObjectId") as string == battlefield
                    && ((IReadOnlyList<string>)added.Payload["cardIds"]!).SequenceEqual([top])
                    && current.PlayerZones["P2"].Hand.SequenceEqual(before.PlayerZones["P2"].Hand.Append(top))
                    && current.PlayerZones["P2"].MainDeck.SequenceEqual(before.PlayerZones["P2"].MainDeck.Skip(1)),
                    "Exactly the originally revealed spell enters hand without drawing.");
            }
            else Check(!events.Any(ev => ev.Kind == "CARD_ADDED_TO_HAND")
                && current.PlayerZones["P2"].Hand.SequenceEqual(before.PlayerZones["P2"].Hand)
                && current.PlayerZones["P2"].MainDeck.SequenceEqual(before.PlayerZones["P2"].MainDeck.Skip(1).Append(top)),
                "Exactly the nonspell is recycled; no card is added or drawn.");
            await Cold();
            var counts = new Dictionary<string, int>(current.DrawLedger.Counts) { ["P2"] = 2 };
            var negative = new Dictionary<string, int>(current.DrawLedger.Counts) { ["P2"] = -1 };
            await RejectForgeries([
                ("coordinated-false-second-draw-count", current with { DrawLedger = new(current.TurnNumber, counts) }, true, null),
                ("coordinated-closed-idle-main", current with { TimingState = TimingStates.NeutralClosed }, true, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1, "CN", "zh-CN",
                    RuleInputIdentity.Current.AuthoritySha256, RuleInputIdentity.Current.CatalogSha256, new string('a', 64)) }, false, null),
                ("negative-draw-count", current with { DrawLedger = new(current.TurnNumber, negative) }, false, "invalid turn draw ledger")]);
            afterCold = true;
            await Command("P1", "original-next-end-turn", new EndTurnCommand());
            Check(current.TurnPlayerId == "P2" && current.Phase == MatchPhases.Main && current.TimingState == TimingStates.NeutralOpen,
                "Original next EndTurn reaches P2 open main after current cold recovery.");
            var final = await Cold();
            Check(count == 32 && coldCount == 9 && nextMatched == 4 && attacks.Count == 4
                && malformedTargetIngressRejections == 15,
                "Retain every normal command, cold restore, original next command and independent attack.");
            var fileName = Path.Combine(output, branch + "-" + format + "-original-frame.json.gz");
            Check(!File.Exists(fileName), "Never overwrite an original SQL frame.");
            await using (var file = File.Create(fileName)) await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize))
                await JsonSerializer.SerializeAsync(zip, final, json);
            results.Add(new { branch, spell, format = format.ToString(), room, commands = final.Commands.Count,
                events = final.Events.Count, coldRestores = coldCount, originalNextCommandsMatched = nextMatched,
                attacks, checkpoints, malformedTargetIngressRejections, originalBlankSeed = 0, originalFullReplayMatched = true,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs });

            async Task RejectMalformedTargets(string actor, JsonObject fields)
            {
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
                fields.Remove("battlefieldTargetObjectIds");
                var canonical = fields.ToJsonString(); var beforeDigest = await Digest();
                var beforeHash = MatchStateHasher.Hash(current);
                foreach (var property in new[] {
                    "\"battlefieldTargetObjectIds\":null", "\"battlefieldTargetObjectIds\":{}",
                    "\"battlefieldTargetObjectIds\":1", "\"battlefieldTargetObjectIds\":true",
                    "\"battlefieldTargetObjectIds\":\"TARGET\"", "\"battlefieldTargetObjectIds\":[null]",
                    "\"battlefieldTargetObjectIds\":[1]", "\"battlefieldTargetObjectIds\":[\"\"]",
                    "\"battlefieldTargetObjectIds\":[\"  \"]", "\"battlefieldTargetObjectIds\":[\" TARGET\"]",
                    "\"battlefieldTargetObjectIds\":[\"TARGET \"]", "\"battlefieldTargetObjectIds\":[\"TARGET\",null]",
                    "\"battlefieldTargetObjectIds\":[],\"battlefieldTargetObjectIds\":[]",
                    "\"battlefieldTargetObjectIds\":null,\"battlefieldTargetObjectIds\":[]",
                    "\" battlefieldTargetObjectIds \":[]" })
                {
                    using var document = JsonDocument.Parse(canonical[..^1] + "," + property + "}");
                    var raw = document.RootElement.Clone();
                    var rejected = await session.SubmitAsync(actor, "prefix-" + count,
                        GameCommandJsonMapper.Map(raw), raw, default);
                    Check(!rejected.Accepted && rejected.ErrorCode == ErrorCodes.InvalidPayload
                        && rejected.Events.Count == 0 && MatchStateHasher.Hash(rejected.State) == beforeHash,
                        "Malformed optional battlefield targets are rejected before the real SQL journal.");
                    malformedTargetIngressRejections++;
                }
                Check(await Digest() == beforeDigest,
                    "All fifteen malformed attempts leave all seven SQL tables exact; the canonical command reuses the same intent.");
            }

            Task<ResolutionResult> Command(string actor, string intent, GameCommand command)
                => Submit(actor, intent, JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject());
            async Task<ResolutionResult> Submit(string actor, string intent, JsonObject fields)
            {
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = current.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var command = GameCommandJsonMapper.Map(raw);
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck,
                            deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid, "Both formats require a fully legal original deck.");
                var expected = afterCold ? await engine.ResolveAsync(current, new(intent, actor, command.CmdType), command, default) : null;
                var result = command switch {
                    SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, intent, submitted, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default),
                    _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)),
                        "Original next command, state and events survive the cold production registry."); nextMatched++;
                }
                current = result.State; afterCold = false; count++; return result;
            }
            async Task<MatchRecoveryFrame> Cold()
            {
                var beforeDigest = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Missing actual original SQL frame.");
                Check(frame.IsConsistent && frame.Commands.Count == count && frame.OpeningSeed == 0
                    && frame.ReplayInitialState is { Tick: 0, Status: MatchStatuses.Seating } && frame.ReplayInitialState.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(current),
                    "Original blank state and full original journal remain consistent: " + string.Join(';', frame.ValidationErrors));
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0,
                    "Every original accepted command and event replays exactly.");
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players, options).GetOrCreateAsync(room, default);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))),
                        "Both original private player views and prompts survive matching-format recovery.");
                Check(await Digest() == beforeDigest, "Cold recovery cannot rewrite any original seven-table row.");
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
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged Ravenbloom state was accepted."); }
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
            status = "REAL_POSTGRES_RAVENBLOOM_PUT_HAND_BOTH_FORMATS_COLD_ORIGINAL_NEXT_REPLAY_TAMPER_PASS", results,
            scope = "CN16 current exact SFD215 hand-entry classification; four normal blank seed0 runs with a genuine prior draw and matching session format. Native, wrong-format configuration identity, Windows and whole-card semantics remain separate."
        }, json));
        Console.WriteLine($"POSTGRES_RAVENBLOOM_PUT_HAND_PASS: {results.Count} actual ordinary matching-format branches; no false draw or Frozen Gem; original next turn and full replay survive cold SQL recovery.");
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string value)
    {
        await using var update = new NpgsqlCommand($"update {table} set payload=@payload::jsonb where id=@id", connection, transaction);
        update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("payload", value);
        Check(await update.ExecuteNonQueryAsync() == 1, "Update exactly the retained original row.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
