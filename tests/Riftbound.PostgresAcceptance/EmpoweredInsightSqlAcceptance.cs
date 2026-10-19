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

internal static class EmpoweredInsightSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "empowered-insight-openings.json")));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var journalJson = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var results = new List<object>();
        foreach (var row in fixture.RootElement.GetProperty("branches").EnumerateArray())
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var profile in new[] { "KEEP", "RECYCLE" })
        {
            var branch = row.GetProperty("branch").GetString()!; var songs = row.GetProperty("songs").GetInt32();
            var room = "postgres-empowered-insight-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original ordinary blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false; var atomicRejected = 0;
            var attacks = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("duel-sql-" + player), default);
            var entries = row.GetProperty("openingIntents").EnumerateArray().ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                var command = GameCommandJsonMapper.Map(entries[i].GetProperty("rawCommand"));
                if (i == 6 || command is AssembleEquipmentCommand or MoveUnitCommand or PlayCardCommand { CardNo: "VEN·047" or "SFD·059/221" }) await Cold();
                await Command(entries[i].GetProperty("playerId").GetString()!, "prefix-" + count, command);
            }
            var host = row.GetProperty("host").GetString()!;
            Check(current.TurnPlayerId == "P1" && current.PlayerZones["P1"].Base.Contains(host)
                && !current.CardObjects[host].IsEmpowered, "Actual ordinary unempowered Mage opening.");
            var needed = Math.Max(0, 2-current.RunePools["P1"].Mana);
            var ready = current.PlayerZones["P1"].Base.Where(id => current.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)
                && !current.CardObjects[id].IsExhausted).Take(needed).ToArray();
            Check(ready.Length == needed, "Use only actually available field runes.");
            foreach (var id in ready) await Command("P1", "original-tap-"+count, new TapRuneCommand(id));
            await Cold(); var mana = current.RunePools["P1"].Mana;
            var activation = new ActivateAbilityCommand(host, "EMPOWER:0:0", []);
            await RejectIntent("P2", activation); await Command("P1", "original-empower", activation);
            Check(current.RunePools["P1"].Mana == mana-2 && !current.CardObjects[host].IsEmpowered
                && current.TriggerQueue.Count == 0 && current.PendingCardChoice is null, "Real cost paid before the status transition.");
            await Cold(); await Top();
            var all = current.TriggerQueue.Select(t => t.InsightContext!).Concat(current.StackItems
                .Where(t => t.InsightContext is not null).Select(t => t.InsightContext!)).ToArray();
            Check(current.CardObjects[host].IsEmpowered && all.Length == songs+1
                && all.Count(c => c.Origin is not null) == songs && all.All(c => c is { Kind: "EMPOWERED", Count: 2, ControllerId: "P1" }),
                "Each actual current printed text instance captures a separate mandatory private Insight2.");
            await Cold(); var captured = all.FirstOrDefault(c => c.Origin is not null) ?? all.Single();
            MatchState Forged(InsightTriggerContext changed) => current with {
                TriggerQueue = current.TriggerQueue.Select(t => t.InsightContext == captured ? t with { InsightContext = changed } : t).ToArray(),
                StackItems = current.StackItems.Select(t => t.InsightContext == captured ? t with { InsightContext = changed } : t).ToArray() };
            var identity = RuleInputIdentity.Current;
            var forgeries = new List<(string, MatchState, bool, string?)> {
                ("wrong-count", Forged(captured with { Count = 1 }), false, null),
                ("future-source-generation", Forged(captured with { SourceGeneration = 99 }), false, null),
                ("false-token-factory", Forged(captured with { SourceTokenFactoryCardNo = P6TokenFactoryCatalog.SandSoldierTokenCardNo }), false, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1,"CN","zh-CN",identity.AuthoritySha256,identity.CatalogSha256,new string('a',64)) }, false, null),
                ("wrong-catalog", current with { RuleInputs = new RuleInputIdentity(1,"CN","zh-CN",identity.AuthoritySha256,new string('a',64),identity.ImplementationSha256) }, false, null),
                ("wrong-authority", current with { RuleInputs = new RuleInputIdentity(1,"CN","zh-CN",new string('a',64),identity.CatalogSha256,identity.ImplementationSha256) }, false, null) };
            if (captured.Origin is { } origin) forgeries.AddRange([
                ("unknown-copied-equipment", Forged(captured with { Origin = origin with { Equipment = origin.Equipment with { ObjectId = "UNKNOWN" } } }), false, null),
                ("future-copy-revision", Forged(captured with { Origin = origin with { Revision = 99 } }), false, null),
                ("future-copy-capture", Forged(captured with { Origin = origin with { CapturedTick = current.Tick+1 } }), false, null),
                ("coordinated-copied-origin-removal", Forged(captured with { Origin = null }), true, null)]);
            await RejectForgeries(forgeries);
            if (current.TriggerQueue.Count > 0) await Command("P1", "original-order", new OrderTriggersCommand(OrderedTriggerIds:
                current.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
            var hand = current.PlayerZones["P1"].Hand.ToArray(); string? previous = null;
            for (var i=0; i<=songs; i++)
            {
                var deck = current.PlayerZones["P1"].MainDeck.ToArray(); Check(deck.Length >= 3,"Ordinary current deck retains enough cards.");
                await Cold(); await Top(); await KeepObservation();
                var choice = current.PendingCardChoice ?? throw new InvalidOperationException("Missing private empowered Insight.");
                Check(choice.PlayerId == "P1" && choice.ChoiceWindow == "INSIGHT" && choice.ContextObjectIds.SequenceEqual(deck.Take(2))
                    && current.PendingPayment is null, "Mandatory private look uses the actual remaining top two without optional payment.");
                var spectator = MatchReplayRedactor.BuildSpectatorFrame(room,current.Tick,0,[],current);
                foreach(var id in deck.Take(2)) Check(!JsonSerializer.Serialize(session.SnapshotFor("P2"),json).Contains("\""+id+"\"",StringComparison.Ordinal)
                    && !JsonSerializer.Serialize(session.PromptFor("P2"),json).Contains("\""+id+"\"",StringComparison.Ordinal)
                    && !JsonSerializer.Serialize(spectator.SpectatorSnapshot,json).Contains("\""+id+"\"",StringComparison.Ordinal),
                    "Opponent and spectator do not receive the private observed identities.");
                await RejectIntent("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
                await RejectIntent("P1", new ChooseCardsCommand("STALE", choice.ChoiceWindow, []));
                await RejectIntent("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [deck[2]]));
                if(previous is not null) await RejectIntent("P1",new ChooseCardsCommand(previous,choice.ChoiceWindow,[]));
                await Cold(); var selected = profile == "RECYCLE" && i==0 ? deck.Take(2).ToArray() : [];
                await Command("P1", "original-private-choice-"+count, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
                if(selected.Length == 0)
                {
                    var order = current.PendingCardChoice ?? throw new InvalidOperationException("Missing retained top ordering.");
                    Check(order.ChoiceWindow == "INSIGHT_ORDER", "Exact private retained-top order window.");
                    await RejectIntent("P1",new ChooseCardsCommand(order.ChoiceId,order.ChoiceWindow,[deck[0],deck[0]]));
                    await Cold(); await Command("P1","original-top-order-"+count,new ChooseCardsCommand(order.ChoiceId,order.ChoiceWindow,[deck[1],deck[0]]));
                    Check(current.PlayerZones["P1"].MainDeck.SequenceEqual(new[]{deck[1],deck[0]}.Concat(deck.Skip(2))), "Retained cards follow the chosen order; the unseen suffix is untouched.");
                }
                else Check(current.PlayerZones["P1"].MainDeck.Take(deck.Length-2).SequenceEqual(deck.Skip(2))
                    && current.PlayerZones["P1"].MainDeck.TakeLast(2).Order(StringComparer.Ordinal).SequenceEqual(selected.Order(StringComparer.Ordinal)),
                    "Core417.5 recycles a simultaneous subset in random order at bottom; independent membership and untouched prefix.");
                Check(current.PlayerZones["P1"].Hand.SequenceEqual(hand),"Insight does not draw."); previous=choice.ChoiceId; await Cold();
            }
            Check(current.StackItems.Count==0 && current.TriggerQueue.Count==0 && current.PendingCardChoice is null,"All mandatory instances finish.");
            await RejectIntent("P1",activation); await Command("P1","original-next-turn",new EndTurnCommand());
            Check(current.CardObjects[host].IsEmpowered,"Normal next command preserves the status.");
            var final = await Cold(); Check(attacks.Count == (songs == 0 ? 6 : 10),"Every original independent forgery branch executed.");
            var path = Path.Combine(output, branch + "-" + format + "-" + profile + "-original-frame.json.gz"); Check(!File.Exists(path), "Never overwrite an original SQL frame.");
            await using (var file = File.Create(path)) await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize)) await JsonSerializer.SerializeAsync(zip, final, journalJson);
            results.Add(new { branch, songs, format = format.ToString(), profile, room, commands = count, events = final.Events.Count, coldRestores = coldCount,
                originalNextCommandsMatched = nextMatched, atomicEngineRejections = atomicRejected, attacks, originalBlankSeed = 0, originalFullReplayMatched = true,
                finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs });

            async Task Top()
            {
                await Command(current.PriorityPlayerId!, "original-pass-"+count, new PassPriorityCommand());
                await Command(current.PriorityPlayerId!, "original-pass-"+count, new PassPriorityCommand());
            }
            async Task KeepObservation()
            {
                while (current.PendingRuleChoice is { Request.Kind: "DECK_OBSERVATION_BANISH" } observation)
                    await Command("P1", "original-observation-keep-" + count, new PayCostCommand(observation.Request.Id, "RULE_REPLACEMENT", ["KEEP"]));
            }
            async Task RejectIntent(string actor, GameCommand command)
            {
                var hash = MatchStateHasher.Hash(current); var before = await Digest();
                var result = await engine.ResolveAsync(current, new("rejected-duel", actor, command.CmdType), command, default);
                Check(!result.Accepted && result.Events.Count == 0 && hash == MatchStateHasher.Hash(result.State) && before == await Digest(), "Rejected engine intent cannot mutate authoritative state or any SQL row."); atomicRejected++;
            }
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
                session = restored; coldCount++; afterCold = true; return frame;
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
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged empowered Insight state was accepted."); }
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
            status = "REAL_POSTGRES_ORDINARY_EMPOWERED_INSIGHT_MATCHING_FORMAT_COST_CHOICES_COLD_ORIGINAL_REPLAY_TAMPER_PASS", regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string value)
    {
        await using var update = new NpgsqlCommand($"update {table} set payload=@payload::jsonb where id=@id", connection, transaction);
        update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("payload", value);
        Check(await update.ExecuteNonQueryAsync() == 1, "Update exactly the retained original row.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
