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

internal static class PakaaProtectorSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pakaa-protector-openings.json")));
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var journalJson = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var results = new List<object>();
        foreach (var row in fixture.RootElement.GetProperty("branches").EnumerateArray())
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var option in row.GetProperty("profile").GetString() == "NOCTURNE" ? new[] { "KEEP", "BANISH_DECLINE", "BANISH_PLAY" } : new[] { "ORDINARY" })
        {
            var profile = row.GetProperty("profile").GetString()!; var branch = row.GetProperty("branch").GetString()!;
            var room = "postgres-paka-reveal-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original ordinary blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var current = initial; var count = 0; var coldCount = 0; var nextMatched = 0; var afterCold = false; var atomicRejected = 0;
            var attacks = new List<object>();
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, initial.Seats[player], ReconnectTokenHasher.Hash("paka-sql-" + player), default);
            foreach (var entry in row.GetProperty("openingIntents").EnumerateArray())
            {
                var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
                if (count == 6 || command is PlayCardCommand { CardNo: "VEN·033" }) await Cold();
                await Command(entry.GetProperty("playerId").GetString()!, "original-prefix-" + count, command);
            }
            var host = row.GetProperty("host").GetString()!; var top = row.GetProperty("top").GetString()!;
            Check(current.PlayerZones["P1"].Base.Contains(host) && current.CardObjects[host].CardNo == "VEN·033"
                && !current.CardObjects[host].IsExhausted && current.TurnPlayerId == "P1" && current.PlayerZones["P1"].MainDeck[0] == top,
                "Actual paid, naturally readied Paka and original desired deck top.");
            if (profile == "NOCTURNE" && current.RunePools["P1"].TotalPower < 1)
                await Command("P1", "original-rune-power", new RecycleRuneCommand(current.PlayerZones["P1"].Base.First(id => current.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard))));
            await Cold(); var deck = current.PlayerZones["P1"].MainDeck.ToArray(); var hand = current.PlayerZones["P1"].Hand.ToArray();
            var grave = current.PlayerZones["P1"].Graveyard.ToArray();
            var destination = current.PlayerZones["P1"].Battlefields.First(id => current.BattlefieldStates.ContainsKey(id));
            var move = new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination);
            await RejectIntent("P2", move); await Command("P1", "original-move", move); await Cold();
            var captured = current.TriggerQueue.Select(t => t.MovementObserver).Concat(current.StackItems.Select(i => i.MovementObserver))
                .Single(c => c is { CardNo: "VEN·033" })!;
            MatchState ForgedSource(MovementObserverContext value) => current with {
                TriggerQueue = current.TriggerQueue.Select(t => t.MovementObserver == captured ? t with { MovementObserver = value } : t).ToArray(),
                StackItems = current.StackItems.Select(t => t.MovementObserver == captured ? t with { MovementObserver = value } : t).ToArray() };
            var identity = RuleInputIdentity.Current;
            await RejectForgeries([
                ("source-generation", ForgedSource(captured with { SourceGeneration = captured.SourceGeneration + 1 }), false, null),
                ("wrong-movement-kind", ForgedSource(captured with { Kind = TriggerKinds.UnitMovedDraw }), false, null),
                ("wrong-authority", current with { RuleInputs = new RuleInputIdentity(1,"CN","zh-CN",new string('a',64),identity.CatalogSha256,identity.ImplementationSha256) }, false, null),
                ("wrong-catalog", current with { RuleInputs = new RuleInputIdentity(1,"CN","zh-CN",identity.AuthoritySha256,new string('a',64),identity.ImplementationSha256) }, false, null),
                ("wrong-implementation", current with { RuleInputs = new RuleInputIdentity(1,"CN","zh-CN",identity.AuthoritySha256,identity.CatalogSha256,new string('a',64)) }, false, null) ]);
            if (current.TriggerQueue.Count > 0) await Command("P1", "original-order", new OrderTriggersCommand(OrderedTriggerIds: current.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            await Top();
            if (profile == "NOCTURNE")
            {
                var choice = current.PendingRuleChoice ?? throw new InvalidOperationException("Actual public observation decision required.");
                Check(choice.Request.Kind == "DECK_OBSERVATION_BANISH" && choice.Request.Preview!.DeckObservationView!.Public, "One public reveal, not a private look.");
                await Cold(); await RejectIntent("P2", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BANISH"]));
                await RejectIntent("P1", new PayCostCommand("STALE", "RULE_REPLACEMENT", ["BANISH"]));
                await Command("P1", "original-observation", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [option == "KEEP" ? "KEEP" : "BANISH"]));
                if (option != "KEEP")
                {
                    var pending = current.PendingEffectPlay ?? throw new InvalidOperationException("Mandatory original play confirmation missing.");
                    var observed = current.ObservedDeckPlays!.Single();
                    Check(pending.Parent.ObservedPlayPermission!.Context == observed && observed.Instruction.Parent.MovementObserver?.CardNo == "VEN·033"
                        && observed.Instruction.Kind == "REVEAL" && observed.Instruction.Seen.Single().ObjectId == top,
                        "Exact original source and top identity survive observation and parent completion.");
                    await Cold();
                    await RejectForgeries([
                        ("look-for-public-reveal", WithObserved(current, observed with { Instruction = observed.Instruction with { Kind = "LOOK" } }), false, null),
                        ("wrong-observed-generation", WithObserved(current, observed with { Card = observed.Card with { Generation = observed.Card.Generation + 1 } }), false, null),
                        ("free-observed-unit", WithObserved(current, observed with { PowerCost = 0 }), false, null),
                        ("coordinated-earlier-observation-tick", WithObserved(current, observed with { CapturedTick = observed.CapturedTick - 1 }), true, null) ]);
                    await RejectIntent("P1", new EndTurnCommand()); await RejectIntent("P2", new PlayCardCommand(top, "OGN·194/298", []));
                    if (option == "BANISH_PLAY") await Command("P1", "original-observed-play", new PlayCardCommand(top, "OGN·194/298", []));
                    else await Command("P1", "original-observed-decline", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
                    Check((option == "BANISH_PLAY" ? current.PlayerZones["P1"].Base : current.PlayerZones["P1"].Banished).Contains(top), "The original observed unit completes the chosen permission.");
                }
            }
            Check(current.PlayerZones["P1"].MainDeck.SequenceEqual(deck.Skip(1))
                && current.PlayerZones["P1"].Graveyard.SequenceEqual(profile == "NONUNIT" ? grave.Append(top) : grave)
                && current.PlayerZones["P1"].Hand.SequenceEqual(profile == "UNIT" || profile == "NOCTURNE" && option == "KEEP" ? hand.Append(top) : hand),
                "Original top only: draw Unit, trash non-Unit, never draw a substituted card after static banishment.");
            Check(current.PendingEffectPlay is null && current.ObservedDeckPlays is null, "All original observation permissions complete.");
            await Cold();
            for (var index = 0; index < 12 && (current.TriggerQueue.Count > 0 || current.StackItems.Count > 0 || current.FocusPlayerId is not null); index++)
            {
                if (current.TriggerQueue.Count > 0) await Command(current.TriggerQueue[0].ControllerId, "original-order-"+count,
                    new OrderTriggersCommand(OrderedTriggerIds: current.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                else if (current.PriorityPlayerId is not null) await Command(current.PriorityPlayerId, "original-pass-"+count, new PassPriorityCommand());
                else await Command(current.FocusPlayerId!, "original-focus-"+count, new PassFocusCommand());
            }
            await Cold(); await Command("P1", "original-next-turn", new EndTurnCommand()); var final = await Cold();
            var path = Path.Combine(output, branch + "-" + format + "-" + option + "-original-frame.json.gz"); Check(!File.Exists(path), "Never overwrite an original SQL frame.");
            await using (var file = File.Create(path)) await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize)) await JsonSerializer.SerializeAsync(zip, final, journalJson);
            results.Add(new { branch, format = format.ToString(), profile, option, room, commands = count, events = final.Events.Count,
                coldRestores = coldCount, originalNextCommandsMatched = nextMatched, atomicEngineRejections = atomicRejected,
                attacks, originalBlankSeed = 0, originalFullReplayMatched = true, finalHash = MatchStateHasher.Hash(current), ruleInputs = current.RuleInputs });

            async Task Top()
            {
                await Command(current.PriorityPlayerId!, "original-pass-"+count, new PassPriorityCommand());
                await Command(current.PriorityPlayerId!, "original-pass-"+count, new PassPriorityCommand());
            }

            async Task RejectIntent(string actor, GameCommand command)
            {
                var hash = MatchStateHasher.Hash(current); var before = await Digest();
                var result = await engine.ResolveAsync(current, new("rejected-paka", actor, command.CmdType), command, default);
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
                    try { await registry.GetOrCreateAsync(room, default); throw new InvalidOperationException("Forged Paka reveal state was accepted."); }
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
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var connection = await source.OpenConnectionAsync();
            foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
            {
                digest.AppendData(Encoding.UTF8.GetBytes(table+"\n")); var any = false;
                await using var query = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
                query.Parameters.AddWithValue("room", room); await using var reader = await query.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { any = true; digest.AppendData(Encoding.UTF8.GetBytes(reader.GetString(0)+"\n")); }
                if (!any) digest.AppendData(Encoding.UTF8.GetBytes("\n"));
            }
            return Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
        }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_ORDINARY_PAKA_REVEAL_MATCHING_FORMAT_COLD_ORIGINAL_REPLAY_TAMPER_PASS", regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static MatchState WithObserved(MatchState state, ObservedDeckPlayContext context)
    {
        var original = state.ObservedDeckPlays!.Single();
        context = context with { Id = $"OBSERVED:{context.CapturedTick}:{context.Instruction.Parent.StackItemId}:{context.Instruction.ExecutionIndex}:{context.Card.ObjectId}" };
        var pending = state.PendingEffectPlay!;
        return state with { ObservedDeckPlays = [context],
            StackItems = state.StackItems.Select(item => item.ObservedDeckPlay?.Id == original.Id
                ? item with { StackItemId = context.Id, ObservedDeckPlay = context } : item).ToArray(),
            PendingEffectPlay = pending with { ChoiceId = "OBSERVED-PLAY:" + context.Id,
                Parent = pending.Parent with { StackItemId = "OBSERVED-PERMISSION:" + context.Id,
                    ObservedPlayPermission = pending.Parent.ObservedPlayPermission! with { Context = context } } } };
    }
    private static async Task Set(NpgsqlConnection connection, NpgsqlTransaction transaction, string table, long id, string value)
    {
        await using var update = new NpgsqlCommand($"update {table} set payload=@payload::jsonb where id=@id", connection, transaction);
        update.Parameters.AddWithValue("id", id); update.Parameters.AddWithValue("payload", value);
        Check(await update.ExecuteNonQueryAsync() == 1, "Update exactly the retained original row.");
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
