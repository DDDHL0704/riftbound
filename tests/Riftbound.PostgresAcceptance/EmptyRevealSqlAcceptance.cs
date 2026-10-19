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

internal static class EmptyRevealSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Never overwrite an original empty-reveal SQL run.");
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pakaa-protector-openings.json")));
        var recipe = fixture.RootElement.GetProperty("branches").EnumerateArray().Single(row => row.GetProperty("profile").GetString() == "UNIT");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var results = new List<object>();
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        {
            var room = "postgres-empty-reveal-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var state = initial; var count = 0; var colds = 0; var originalNext = 0; var rejected = 0; var afterCold = false;
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("empty-reveal-sql-" + player), default);
            foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            {
                var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
                if (count == 6 || command is PlayCardCommand { CardNo: "VEN·033" }) await Cold();
                await Submit(entry.GetProperty("playerId").GetString()!, command);
            }
            var host = recipe.GetProperty("host").GetString()!;
            Check(state.PlayerZones["P1"].Base.Contains(host) && state.CardObjects[host].CardNo == "VEN·033", "Original Paka was legally paid and placed in base.");
            var turns = 0;
            while (!(state.TurnPlayerId == "P1" && state.PlayerZones["P1"].MainDeck.Count == 0))
            {
                Check(state.Status == MatchStatuses.InProgress && turns++ < 90, "Reach the empty deck using original legal turns.");
                Check(state.StackItems.Count == 0 && state.TriggerQueue.Count == 0, "Do not bypass any pending effect.");
                await Submit(state.TurnPlayerId!, new EndTurnCommand());
            }
            Check(state.Status == MatchStatuses.InProgress && !state.CardObjects[host].IsExhausted, "The ordinary Paka is still in progress and naturally ready.");
            var before = await Cold();
            var hand = state.PlayerZones["P1"].Hand.ToArray(); var grave = state.PlayerZones["P1"].Graveyard.ToArray();
            var destination = state.PlayerZones["P1"].Battlefields.First(id => state.BattlefieldStates.ContainsKey(id));
            var move = new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + destination);
            await Reject("P2", move); await Submit("P1", move); await Cold();
            if (state.TriggerQueue.Count > 0)
                await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
            await Reject("P2", new EndTurnCommand());
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Cold();
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            var snapshot = JsonSerializer.SerializeToElement(session.SnapshotFor("P1"), json);
            Check(snapshot.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32() == 4,
                "CN432.1.c.1: an initial empty observation cancels the subsequent otherwise Might instruction.");
            Check(state.CardObjects[host].UntilEndOfTurnPowerModifiers.Count == 0 && state.PlayerZones["P1"].MainDeck.Count == 0
                && hand.SequenceEqual(state.PlayerZones["P1"].Hand) && grave.SequenceEqual(state.PlayerZones["P1"].Graveyard), "No phantom card movement or temporary Might.");
            Check(state.PendingRuleChoice is null && state.PendingEffectPlay is null && state.ObservedDeckPlays is null && state.DeckObservationView is null,
                "No impossible observation or deferred play permission.");
            var resolved = await Cold();
            Check(!resolved.Events.Skip(before.Events.Count).Any(entry => entry.Event.Kind is "BURNOUT_APPLIED" or "CARDS_REVEALED" or "CARDS_MILLED" or "CARD_DRAWN" or "POWER_MODIFIED"), "Empty observation causes no burnout, fabricated reveal, draw, mill or power event.");
            Check(state.FocusPlayerId == "P1", "Original initiating focus survives the empty skill.");
            await Submit("P1", new PassFocusCommand()); var final = await Cold();
            var path = Path.Combine(output, "empty-paka-" + format + "-original-frame.json.gz");
            Check(!File.Exists(path), "Never overwrite the original frame.");
            await using (var file = File.Create(path)) await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize))
                await JsonSerializer.SerializeAsync(zip, final, json);
            results.Add(new { room, format = format.ToString(), commands = count, events = final.Events.Count, coldRestores = colds,
                originalNextCommandsMatched = originalNext, atomicRejections = rejected, legalAdditionalTurns = turns,
                originalOpeningCommands = recipe.GetProperty("openingIntents").GetArrayLength(), originalBlankSeed = 0,
                initialObservedCards = 0, expectedFinalMight = 4, sourceCardNo = "VEN·033", ruleInputs = state.RuleInputs,
                originalFullReplayMatched = true, originalPrivateViewsAndPromptsExact = true, noStateSeedOrHistoryInjection = true });

            async Task Submit(string actor, GameCommand command)
            {
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid, "Both declared engine formats require legal original decks.");
                var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "original-empty-" + count;
                var expected = afterCold ? await engine.ResolveAsync(state, new(intent, actor, command.CmdType), command, default) : null;
                var result = command switch { SubmitDeckCommand deckCommand => await session.SubmitDeckAsync(actor, intent, deckCommand, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default), _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)), "Original next command/state/events match after cold recovery.");
                    originalNext++;
                }
                state = result.State; count++; afterCold = false;
            }
            async Task Reject(string actor, GameCommand command)
            {
                var digest = await Digest(); var hash = MatchStateHasher.Hash(state);
                var result = await engine.ResolveAsync(state, new("rejected-empty", actor, command.CmdType), command, default);
                Check(!result.Accepted && result.Events.Count == 0 && MatchStateHasher.Hash(result.State) == hash && await Digest() == digest, "Independent invalid actor intent cannot change state or any original SQL row.");
                rejected++;
            }
            async Task<MatchRecoveryFrame> Cold()
            {
                var digest = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("A real original SQL frame is required.");
                Check(frame.IsConsistent && frame.Commands.Count == count && frame.OpeningSeed == 0
                    && frame.ReplayInitialState is { Tick: 0 } blank && blank.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(state), "Original blank seed, complete command history and authoritative state match.");
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Full original action/event/state replay.");
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players, options).GetOrCreateAsync(room, default);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))), "Both original private views and prompts survive recovery exactly.");
                Check(await Digest() == digest, "Cold read/replay cannot modify any of the seven original tables.");
                session = restored; colds++; afterCold = true; return frame;
            }
            async Task<string> Digest()
            {
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using var connection = await source.OpenConnectionAsync();
                foreach (var table in new[] { "matches", "match_players", "command_log", "game_events", "state_snapshots", "snapshots", "action_prompts" })
                {
                    digest.AppendData(Encoding.UTF8.GetBytes(table + "\n")); var any = false;
                    await using var command = new NpgsqlCommand($"select row_to_json(t)::text from {table} t where match_id=@room order by row_to_json(t)::text", connection);
                    command.Parameters.AddWithValue("room", room); await using var rows = await command.ExecuteReaderAsync();
                    while (await rows.ReadAsync()) { any = true; digest.AppendData(Encoding.UTF8.GetBytes(rows.GetString(0) + "\n")); }
                    if (!any) digest.AppendData(Encoding.UTF8.GetBytes("\n"));
                }
                return Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "receipt.json"), JsonSerializer.Serialize(new {
            status = "REAL_POSTGRES_ORIGINAL_EMPTY_REVEAL_FOLLOWUP_TWO_ENGINE_FORMATS_COLD_REPLAY_PASS", regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
