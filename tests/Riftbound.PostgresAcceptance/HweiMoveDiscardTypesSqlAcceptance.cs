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

internal static class HweiMoveDiscardTypesSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Never overwrite an original Hwei SQL run.");
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "hwei-move-discard-openings.json")));
        var recipe = fixture.RootElement;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var results = new List<object>();
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var reverse in new[] { false, true })
        {
            var room = "postgres-hwei-discard-types-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original blank seed0.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var state = initial; var count = 0; var colds = 0; var originalNext = 0; var rejected = 0; var afterCold = false;
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("hwei-types-sql-" + player), default);
            foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            {
                var command = GameCommandJsonMapper.Map(entry.GetProperty("rawCommand"));
                if (count == 6 || command is PlayCardCommand { CardNo: "UNL-080/219" }) await Cold();
                await Submit(entry.GetProperty("playerId").GetString()!, command);
            }
            var host = recipe.GetProperty("host").GetString()!;
            Check(state.PlayerZones["P1"].Base.Contains(host) && state.CardObjects[host].CardNo == "UNL-080/219"
                && !state.CardObjects[host].IsExhausted, "Original paid Hwei is naturally ready in base.");
            var runes = state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard)).Take(2).ToArray();
            Check(runes.Length == 2, "Two actual original runes required.");
            foreach (var id in runes) await Submit("P1", new TapRuneCommand(id));
            var before = await Cold(); var top = state.PlayerZones["P1"].MainDeck[0];
            Check(top == recipe.GetProperty("top").GetString() && state.CardObjects[top].CardNo == "VEN·058"
                && state.CardObjects[top].Tags.Contains(CardObjectTags.UnitCard)
                && state.CardObjects[top].Tags.Contains(CardObjectTags.EquipmentCard), "The real ordinary factory preserves both printed Porobot types.");
            var hand = state.PlayerZones["P1"].Hand.ToArray(); var grave = state.PlayerZones["P1"].Graveyard.ToArray();
            var deck = state.PlayerZones["P1"].MainDeck.ToArray(); var plays = JsonSerializer.Serialize(state.CardPlays);
            var field = state.PlayerZones["P1"].Battlefields.First(state.BattlefieldStates.ContainsKey);
            var move = new MoveUnitCommand(host, "BASE", "BATTLEFIELD:" + field);
            await Reject("P2", move); await Submit("P1", move); await Cold();
            if (state.TriggerQueue.Count > 0)
                await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            await Submit(state.PriorityPlayerId!, new PassPriorityCommand());
            var discard = state.PendingHandChoice ?? throw new InvalidOperationException("Draw must precede discard confirmation.");
            Check(discard.DrawCount == 0 && discard.RequiredCount == 1 && discard.MaxCount == 1
                && hand.Append(top).SequenceEqual(state.PlayerZones["P1"].Hand) && discard.LegalObjectIds.Contains(top),
                "Actual just-drawn card is a legal mandatory discard, without a second parent draw.");
            Check(!JsonSerializer.Serialize(session.SnapshotFor("P2")).Contains('"' + top + '"', StringComparison.Ordinal)
                && !JsonSerializer.Serialize(session.PromptFor("P2")).Contains('"' + top + '"', StringComparison.Ordinal), "Opponent cannot see the drawn card or private choices.");
            await Cold();
            await Reject("P2", new ChooseHandCardsCommand(discard.ChoiceId, discard.ChoiceWindow, [top]));
            await Reject("P1", new ChooseHandCardsCommand(discard.ChoiceId, discard.ChoiceWindow, []));
            await Reject("P1", new ChooseHandCardsCommand("expired-choice", discard.ChoiceWindow, [top]));
            await Submit("P1", new ChooseHandCardsCommand(discard.ChoiceId, discard.ChoiceWindow, [top]));
            Check(state.TriggerQueue.Count == 2 && state.TriggerQueue.All(t => t.ControllerId == "P1"
                && t.MovementObserver?.DiscardInline is { Types: 3 } inline && inline.DiscardedCard.ObjectId == top), "Real unit/gear creates both independent inline triggers.");
            await Cold();
            var ordered = state.TriggerQueue.OrderBy(t => t.MovementObserver!.DiscardInline!.BranchType).ToArray();
            if (reverse) Array.Reverse(ordered);
            await Submit("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered.Select(t => t.TriggerId).ToArray()));
            var byStack = state.StackItems.ToDictionary(s => s.StackItemId, s => s.MovementObserver!.DiscardInline!.BranchType);
            for (var i = 0; i < 40 && (state.TriggerQueue.Count > 0 || state.StackItems.Count > 0 || state.PendingCardChoice is not null
                || state.PriorityPlayerId is not null || state.FocusPlayerId is not null); i++)
            {
                if (state.PendingCardChoice is { } choice)
                {
                    Check(choice.ChoiceWindow == "MOVEMENT_DISCARD_RUNE_TARGETS" && runes.All(choice.LegalObjectIds.Contains), "Exact current rune window and original targets required.");
                    await Reject("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, runes));
                    await Submit("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, runes)); await Cold();
                }
                else if (state.TriggerQueue.Count > 0)
                    await Submit(state.TriggerQueue[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: state.TriggerQueue.Select(t => t.TriggerId).ToArray()));
                else if (state.PriorityPlayerId is { } priority) await Submit(priority, new PassPriorityCommand());
                else await Submit(state.FocusPlayerId!, new PassFocusCommand());
            }
            Check(state.StackItems.Count == 0 && state.TriggerQueue.Count == 0 && state.PendingHandChoice is null && state.PendingCardChoice is null
                && state.TimingState == TimingStates.NeutralOpen, "Resolve both inlines and both original focus windows.");
            Check(hand.SequenceEqual(state.PlayerZones["P1"].Hand) && deck.Skip(1).SequenceEqual(state.PlayerZones["P1"].MainDeck)
                && grave.Append(top).SequenceEqual(state.PlayerZones["P1"].Graveyard)
                && JsonSerializer.Serialize(state.CardPlays) == plays && runes.All(id => !state.CardObjects[id].IsExhausted), "Only original draw/discard and two rune ready effects; no card play.");
            Check(Might() == 8, "Unit inline gives this original Hwei Might8.");
            var resolved = await Cold();
            var events = resolved.Events.Skip(before.Events.Count).Select(e => e.Event).ToArray();
            Check(events.Count(e => e.Kind == "CARD_DRAWN") == 1 && events.Count(e => e.Kind == "CARD_DISCARDED") == 1
                && events.Count(e => e.Kind == "RUNE_READIED") == 2
                && !events.Any(e => e.Kind is "CARDS_REVEALED" or "BURNOUT_APPLIED" or "UNIT_PLAYED_TO_BASE"), "Independent draw, discard and ready counts.");
            Check(events.Where(e => e.Kind == "TRIGGER_RESOLVED" && JsonSerializer.SerializeToElement(e.Payload).GetProperty("effectKind").GetString() == "MOVEMENT_DISCARD_TYPE_INLINE")
                .Select(e => byStack[JsonSerializer.SerializeToElement(e.Payload).GetProperty("triggerId").GetString()!])
                .SequenceEqual(reverse ? new[] { 2, 1 } : new[] { 1, 2 }), "Controller's two actual inline orders are preserved.");
            Check(!JsonSerializer.Serialize(events).Contains('"' + deck[1] + '"', StringComparison.Ordinal), "Next hidden top is never disclosed.");
            await Submit("P1", new EndTurnCommand());
            Check(Might() == 5 && state.CardObjects[host].UntilEndOfTurnPowerModifiers.Count == 0, "Temporary Might expires at the real turn boundary.");
            var final = await Cold();
            var path = Path.Combine(output, $"hwei-{format}-{reverse}-original-frame.json.gz");
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize)) await JsonSerializer.SerializeAsync(zip, final, json);
            results.Add(new { room, format = format.ToString(), reverse, commands = count, events = final.Events.Count,
                coldRestores = colds, originalNextCommandsMatched = originalNext, atomicRejections = rejected,
                originalOpeningCommands = recipe.GetProperty("openingIntents").GetArrayLength(), originalBlankSeed = 0,
                expectedDraws = 1, expectedDiscards = 1, inlineCount = 2, readiedRunes = 2, expectedMight = 8, nextTurnMight = 5,
                sourceCardNo = "UNL-080/219", discardedCardNo = "VEN·058", ruleInputs = state.RuleInputs,
                originalFullReplayMatched = true, originalPrivateViewsAndPromptsExact = true, noStateSeedOrHistoryInjection = true });

            int Might() => JsonSerializer.SerializeToElement(session.SnapshotFor("P1"), json).GetProperty("players").GetProperty("P1")
                .GetProperty("objects").GetProperty(host).GetProperty("effectivePower").GetInt32();
            async Task Submit(string actor, GameCommand command)
            {
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid, "Both declared engine formats require legal original decks.");
                var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "original-hwei-types-" + count;
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
                var result = await engine.ResolveAsync(state, new("rejected-hwei-types", actor, command.CmdType), command, default);
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
            status = "REAL_POSTGRES_ORIGINAL_HWEI_DRAW_DISCARD_TYPES_FOUR_BRANCHES_COLD_REPLAY_PASS",
            regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
