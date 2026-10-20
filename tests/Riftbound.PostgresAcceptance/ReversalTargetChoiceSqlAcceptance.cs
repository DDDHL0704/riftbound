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

internal static class ReversalTargetChoiceSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Never overwrite an original Reversal SQL run.");
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "reversal-target-openings.json")));
        var recipe = fixture.RootElement;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var results = new List<object>();
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var remake in new[] { false, true })
        {
            var room = "postgres-reversal-target-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original blank seed0 only.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var state = initial; var count = 0; var colds = 0; var originalNext = 0; var rejected = 0; var afterCold = false;
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("reversal-sql-" + player), default);
            foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            {
                if (count == 6) await Cold();
                await Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
            }
            var counter = recipe.GetProperty("sourceObjectId").GetString()!;
            var spellSource = recipe.GetProperty("targetSourceObjectId").GetString()!;
            var target = recipe.GetProperty("targetStackItemId").GetString()!;
            var oldUnit = recipe.GetProperty("originalTargetObjectId").GetString()!;
            var newUnit = recipe.GetProperty("newTargetObjectId").GetString()!;
            Check(count == 49 && state.TurnNumber == 28 && state.StackItems.Single().StackItemId == target
                && state.RunePools["P2"].Mana == 4 && state.RunePools["P2"].PowerByTrait.GetValueOrDefault("green") == 3
                && state.CardObjects[oldUnit].Power == 2 && state.CardObjects[newUnit].Power == 2,
                "Original explicit legal prefix, two ordinary base units, actual response mana and green power.");
            var hand = state.PlayerZones["P2"].Hand.Count;
            var p1Deck = state.PlayerZones["P1"].MainDeck.Count; var p2Deck = state.PlayerZones["P2"].MainDeck.Count;
            var firstEvent = (await Cold()).Events.Count;
            await Submit("P2", new PlayCardCommand(counter, "OGN·080/298", [target]));
            await Cold(); await Top(); await Cold(); await Save("target-choice");
            var request = state.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Actual optional target choice is required.");
            Check(request.PlayerId == "P2" && request.Kind == "SPELL_CHOICE_REMAKE"
                && request.Preview!.StackItems.Single(i => i.StackItemId == target).ControllerId == "P2",
                "Only the new controller receives the actual authoritative target choice.");
            await Reject("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["KEEP_ALL"]));
            await Reject("P2", new PayCostCommand(request.Id + "-old", "RULE_REPLACEMENT", ["KEEP_ALL"]));
            await Reject("P2", new PayCostCommand(request.Id, "WRONG", ["KEEP_ALL"]));
            await Reject("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["KEEP_ALL", "CONFIRM"]));
            if (remake)
            {
                await Choose(request.Options.Single(o => o.ObjectIds?.Contains(newUnit) == true).Id);
                await Cold(); await Save("draft"); await Choose("CONFIRM");
            }
            else await Choose("KEEP_ALL");
            var controlled = state.StackItems.Single();
            Check(controlled.ControllerId == "P2" && controlled.TargetObjectIds.SequenceEqual(new[] { remake ? newUnit : oldUnit })
                && state.RunePools["P2"].Mana == 0 && state.RunePools["P2"].TotalPower == 0
                && state.PlayerZones["P2"].Graveyard.Contains(counter) && !state.PlayerZones["P1"].Graveyard.Contains(spellSource),
                "Explicit target decision completes control without playing or paying for the controlled spell again.");
            await Cold(); await Save("after-control"); await Top();
            Check(state.StackItems.Count == 0 && state.TriggerQueue.Count == 0 && state.PendingRuleChoice is null
                && state.PendingCardChoice is null && state.Status == MatchStatuses.InProgress,
                "The original controlled spell completes normally, without history or state injection.");
            Check(state.CardObjects[oldUnit].Power == (remake ? 2 : 1) && state.CardObjects[newUnit].Power == (remake ? 1 : 2)
                && state.PlayerZones["P2"].Hand.Count == hand && state.PlayerZones["P1"].MainDeck.Count == p1Deck
                && state.PlayerZones["P2"].MainDeck.Count == p2Deck - 1 && state.PlayerZones["P1"].Graveyard.Contains(spellSource)
                && !state.PlayerZones["P2"].Graveyard.Contains(spellSource),
                "Only the explicitly chosen unit is reduced, the actual controller draws, and the physical source returns to its owner.");
            var final = await Cold(); var events = final.Events.Skip(firstEvent).Select(e => e.Event).ToArray();
            Check(events.Count(e => e.Kind == "STACK_ITEM_CONTROL_GAINED") == 1 && events.All(e => e.Kind != "STACK_ITEM_COUNTERED")
                && events.Count(e => e.Kind == "SPELL_CHOICES_REMADE") == (remake ? 1 : 0), "Original control and remake event identities.");
            var cost = JsonSerializer.SerializeToElement(events.Single(e => e.Kind == "COST_PAID").Payload);
            Check(cost.GetProperty("mana").GetInt32() == 4 && cost.GetProperty("powerByTrait").GetProperty("green").GetInt32() == 3,
                "Only the printed four-mana, three-green-power Reversal cost is paid after the prefix.");
            await Save("final");
            results.Add(new { room, format = format.ToString(), remake, commands = count, events = final.Events.Count,
                coldRestores = colds, originalNextCommandsMatched = originalNext, atomicRejections = rejected,
                originalOpeningCommands = 49, originalBlankSeed = 0, sourceCardNo = "OGN·080/298", targetCardNo = "OGN·095/298",
                sourceObjectId = counter, targetSourceObjectId = spellSource, targetStackItemId = target,
                originalTargetObjectId = oldUnit, newTargetObjectId = newUnit, actualRuleInputs = state.RuleInputs,
                originalFullReplayMatched = true, bothOriginalPrivateViewsAndPromptsExact = true,
                sevenOriginalTablesUnchangedByColdAndInvalidDirectIntents = true, noStateSeedOrAcceptedHistoryInjection = true,
                completeGameOrWholeCardCertification = false });

            async Task Top() { await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            async Task Choose(string option)
            {
                var current = state.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Actual current target choice required.");
                await Submit("P2", new PayCostCommand(current.Id, "RULE_REPLACEMENT", [option]));
            }
            async Task Save(string phase)
            {
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("Actual SQL frame required.");
                await using var file = new FileStream(Path.Combine(output, $"reversal-{format}-{remake}-{phase}-original-frame.json.gz"), FileMode.CreateNew);
                await using var zip = new GZipStream(file, CompressionLevel.SmallestSize);
                await JsonSerializer.SerializeAsync(zip, frame, json);
            }
            async Task Submit(string actor, GameCommand command)
            {
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid,
                            "Both declared engine formats require legal original decks.");
                var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "original-reversal-target-" + count;
                var expected = afterCold ? await engine.ResolveAsync(state, new(intent, actor, command.CmdType), command, default) : null;
                var result = command switch { SubmitDeckCommand deckCommand => await session.SubmitDeckAsync(actor, intent, deckCommand, raw, default),
                    ReadyCommand => await session.ReadyAsync(actor, intent, raw, default), _ => await session.SubmitAsync(actor, intent, command, raw, default) };
                Check(result.Accepted, result.ErrorMessage);
                if (afterCold)
                {
                    Check(expected!.Accepted && MatchStateHasher.Hash(expected.State) == MatchStateHasher.Hash(result.State)
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected.Events), JsonSerializer.SerializeToNode(result.Events)),
                        "Original next command, authoritative state and events match after cold recovery.");
                    originalNext++;
                }
                state = result.State; count++; afterCold = false;
            }
            async Task Reject(string actor, GameCommand command)
            {
                var digest = await Digest(); var hash = MatchStateHasher.Hash(state);
                var result = await engine.ResolveAsync(state, new("rejected-reversal-target", actor, command.CmdType), command, default);
                Check(!result.Accepted && result.Events.Count == 0 && MatchStateHasher.Hash(result.State) == hash
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(result.Snapshots), JsonSerializer.SerializeToNode(ResolutionResult.BuildSnapshots(state)))
                    && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(result.Prompts), JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state)))
                    && await Digest() == digest, "Invalid direct intent cannot change authoritative state, either private view or any original SQL row.");
                rejected++;
            }
            async Task<MatchRecoveryFrame> Cold()
            {
                var digest = await Digest();
                var frame = await store.LoadAsync(room, default) ?? throw new InvalidOperationException("A real original SQL frame is required.");
                Check(frame.IsConsistent && frame.Commands.Count == count && frame.OpeningSeed == 0
                    && frame.ReplayInitialState is { Tick: 0 } blank && blank.CardObjects.Count == 0
                    && MatchStateHasher.Hash(frame.AuthoritativeState!) == MatchStateHasher.Hash(state), "Original blank seed, complete history and actual state match.");
                Check((await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, engine, default)).Count == 0, "Full original action/event/state replay.");
                var restored = await new InMemoryMatchSessionRegistry(engine, journal, store, players, options).GetOrCreateAsync(room, default);
                foreach (var player in initial.Seats.Keys)
                    Check(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.SnapshotFor(player)), JsonSerializer.SerializeToNode(restored.SnapshotFor(player)))
                        && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(session.PromptFor(player)), JsonSerializer.SerializeToNode(restored.PromptFor(player))),
                        "Both original private views and prompts survive recovery exactly.");
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
            status = "REAL_POSTGRES_ORIGINAL_REVERSAL_FOUR_BRANCHES_COLD_REPLAY_PASS",
            regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
