using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;

internal static class FateWeaverSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fate-weaver-bright-future-opening.json"));
        using var fixture = JsonDocument.Parse(bytes);
        var data = fixture.RootElement;
        Check(data.GetProperty("openingSeed").GetInt64() == 0, "Keep this new recipe's original explicit test seed0.");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var results = new List<object>();
        foreach (var draw in new[] { true, false })
        {
            var room = "postgres-weaver-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, data.GetProperty("seats").Deserialize<Dictionary<string, string>>()!);
            Check(initial.Tick == 0 && initial.CardObjects.Count == 0, "Start at the genuine blank state; never inject a midgame snapshot.");
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false;
            var attacks = new List<object>(); var checkpoints = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("weaver-" + player), default);
            foreach (var entry in data.GetProperty("acceptedCommands").EnumerateArray())
            {
                await Submit(entry.GetProperty("playerId").GetString()!, entry.GetProperty("clientIntentId").GetString()!,
                    JsonNode.Parse(entry.GetProperty("rawCommand").GetRawText())!.AsObject());
                if (count == 24)
                {
                    Check(current.TurnPlayerId == "P1" && current.TriggerQueue.Count == 2 && current.TriggerQueue.All(t => t.ControllerId == "P2"),
                        "Only off-turn P2 has two skills to order; P1's separate Weaver skill is already stacked.");
                    Check(session.PromptFor("P2").Candidates!.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled)
                        && !session.PromptFor("P1").Actionable, "Retain the actual off-turn owner action and observer projection.");
                    await Cold(); checkpoints.Add(new { stage = "offturn-owner-order", commands = count }); afterCold = true;
                }
            }
            var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Weaver must suspend for its actual private choice.");
            var spells = data.GetProperty("expectedSpellIds").Deserialize<string[]>()!;
            Check(count == 32 && choice.PlayerId == "P1" && choice.ChoiceWindow == "DECK_EFFECT"
                && choice.ContextObjectIds.Count == 4 && choice.RequiredCount == 0 && choice.MaxCount == 1
                && choice.LegalObjectIds.SequenceEqual(spells), "Independent expected four-card optional spell choice, not registry-derived expectations.");
            Check(!JsonSerializer.Serialize(session.SnapshotFor("P2")).Contains(spells[0])
                && !JsonSerializer.Serialize(session.PromptFor("P2")).Contains(spells[0]), "Opponent cannot see either private spell identity.");
            var handCount = current.PlayerZones["P1"].Hand.Count;
            var tail = current.PlayerZones["P1"].MainDeck.Skip(4).ToArray();
            var rest = choice.ContextObjectIds.Except(draw ? new[] { spells[0] } : []).Order().ToArray();
            await Cold(); checkpoints.Add(new { stage = "pending-weaver-choice", commands = count });
            var invalid = choice.ContextObjectIds.First(id => !spells.Contains(id));
            var reordered = choice.ContextObjectIds.ToArray(); (reordered[1], reordered[2]) = (reordered[2], reordered[1]);
            var coordinated = current with {
                PendingCardChoice = choice with { ContextObjectIds = reordered },
                PlayerZones = new Dictionary<string, PlayerZones>(current.PlayerZones) {
                    ["P1"] = current.PlayerZones["P1"] with { MainDeck = reordered.Concat(tail).ToArray() } } };
            await RejectForgeries([
                ("non-spell-eligibility", current with { PendingCardChoice = choice with { LegalObjectIds = choice.LegalObjectIds.Append(invalid).ToArray() } }, false, "invalid private deck choice permissions"),
                ("maximum-two", current with { PendingCardChoice = choice with { MaxCount = 2 } }, false, "invalid private deck choice permissions"),
                ("coordinated-observed-order", coordinated, true, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1, "CN", "zh-CN", RuleInputIdentity.Current.AuthoritySha256,
                    RuleInputIdentity.Current.CatalogSha256, new string('a', 64)) }, false, null)]);
            afterCold = true;
            var selected = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? [spells[0]] : []);
            var resolved = await Submit("P1", "weaver-original-choice", JsonSerializer.SerializeToNode(selected, json)!.AsObject());
            Check(current.PendingCardChoice is null && current.StackItems.Count == 0 && current.PlayerExperience["P2"] == 1
                && current.PlayerZones["P1"].Hand.Count == handCount + (draw ? 1 : 0)
                && current.PlayerZones["P1"].MainDeck.Take(tail.Length).SequenceEqual(tail)
                && current.PlayerZones["P1"].MainDeck.Skip(tail.Length).Order().SequenceEqual(rest), "Draw exactly the selected spell and recycle only the remaining observed cards.");
            Check(resolved.Events.Count(e => e.Kind == "CARD_REVEALED") == (draw ? 1 : 0)
                && resolved.Events.Count(e => e.Kind == "CARD_DRAWN") == (draw ? 1 : 0)
                && Equals(resolved.Events.Single(e => e.Kind == "CARDS_RECYCLED").Payload["count"], draw ? 3 : 4), "Reveal/draw/recycle event semantics.");
            var final = await Cold(); checkpoints.Add(new { stage = "complete-original-replay", commands = count });
            Check(count == 33 && nextMatched == 2 && coldCount == 7 && attacks.Count == 4, "Keep both original next commands, all seven real cold restores and four independent attacks.");
            results.Add(new { draw, room, commands = final.Commands.Count, events = final.Events.Count, coldRestores = coldCount,
                originalNextCommandsMatched = nextMatched, checkpoints, attacks, finalHash = MatchStateHasher.Hash(current),
                originalBlankSeed = 0, completeOriginalReplay = true, productionRegistryRestored = true, ruleInputs = current.RuleInputs });

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
            status = "REAL_POSTGRES_FATE_WEAVER_OPTIONAL_SPELL_CHOICE_PASS", results,
            fixtureSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            scope = "Two fresh ordinary blank openings with legal decks, off-turn owner ordering, private Weaver spell selection/decline, actual SQL cold recovery, independent wrong-permission/identity and coordinated observation-history attacks. Native and physical Windows remain OPEN." }, json));
        Console.WriteLine("POSTGRES_FATE_WEAVER_PASS: two original blank branches,66 commands,14 cold restores,4 original next commands,8 forgery rejections and complete original replays.");
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string payload)
    {
        await using var command = new NpgsqlCommand($"update {table} set payload=cast(@payload as jsonb) where id=@id", connection, transaction);
        command.Parameters.AddWithValue("payload", payload); command.Parameters.AddWithValue("id", id);
        Check(await command.ExecuteNonQueryAsync() == 1, "Exactly one original SQL row must be updated.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
