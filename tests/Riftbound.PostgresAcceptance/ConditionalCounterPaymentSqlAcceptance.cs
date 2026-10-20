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

internal static class ConditionalCounterPaymentSqlAcceptance
{
    public static async Task RunAsync(NpgsqlDataSource source, string output)
    {
        Check(!Directory.Exists(output), "Never overwrite an original conditional-counter SQL run.");
        Directory.CreateDirectory(output);
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "conditional-counter-openings.json")));
        var recipe = fixture.RootElement;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var results = new List<object>();
        foreach (var format in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
        foreach (var profile in new[] { "BASE_PAY", "BASE_DECLINE", "ECHO_PAY_BOTH", "ECHO_PAY_THEN_DECLINE" })
        {
            var room = "postgres-conditional-counter-" + Guid.NewGuid().ToString("N");
            var initial = MatchReplayInitialStateBuilder.FromSeats(room, new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
            Check(initial.Tick == 0 && initial.Seed == 0 && initial.CardObjects.Count == 0, "Original blank seed0 only.");
            var options = new MatchSessionOptions(AllowLegacyReadyWithoutDeck: false, DeckFormat: format);
            var engine = new CoreRuleEngine(); var journal = new PostgresMatchJournal(source);
            var players = new PostgresMatchPlayerStore(source); var store = new PostgresMatchRecoveryStore(source);
            IMatchSession session = new MatchSession(initial, engine, journal, players, options);
            var state = initial; var count = 0; var colds = 0; var originalNext = 0; var rejected = 0; var afterCold = false;
            foreach (var player in initial.Seats.Keys)
                await players.SavePlayerSessionAsync(room, player, player, ReconnectTokenHasher.Hash("conditional-counter-sql-" + player), default);
            foreach (var entry in recipe.GetProperty("openingIntents").EnumerateArray())
            {
                if (count == 6) await Cold();
                await Submit(entry.GetProperty("playerId").GetString()!, GameCommandJsonMapper.Map(entry.GetProperty("rawCommand")));
            }
            Check(count == 32 && state.TurnNumber == 12 && state.StackItems.Count == 2
                && state.RunePools["P1"].Mana == 4 && state.RunePools["P2"].Mana == 4
                && state.RunePools["P1"].TotalPower == 0 && state.RunePools["P2"].TotalPower == 0,
                "Both actual two-mana spells and eight/four real rune taps supply the original ordinary state.");
            var counter = recipe.GetProperty("sourceObjectId").GetString()!;
            var targets = recipe.GetProperty("targetStackItemIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            var spellSources = recipe.GetProperty("targetSourceObjectIds").EnumerateArray().Select(v => v.GetString()!).ToArray();
            Check(state.StackItems.Select(s => s.StackItemId).SequenceEqual(targets), "Exact ordinary target identities.");
            var hand = state.PlayerZones["P1"].Hand.Count; var deckCount = state.PlayerZones["P1"].MainDeck.Count;
            var prefixFrame = await Cold(); var firstEvent = prefixFrame.Events.Count;
            var echo = profile.StartsWith("ECHO_", StringComparison.Ordinal);
            await Submit("P2", new PlayCardCommand(counter, "SFD·136/221", [targets[0]],
                OptionalCosts: echo ? ["ECHO"] : null, RepeatChoices: echo ? [new("", [targets[1]])] : null));
            Check(state.RunePools["P2"].Mana == (echo ? 0 : 2) && state.RunePools["P2"].TotalPower == 0,
                "The actual printed base two mana and optional Echo two mana do not add a power cost.");
            await Cold(); await Top();
            Check(state.RunePools["P1"].Mana == 4, "The target controller is not charged during declaration.");
            await Decide(profile != "BASE_DECLINE");
            if (echo)
            {
                Check(state.PendingRuleChoice is { Request.Preview: { } preview } && preview.RunePools["P1"].Mana == 2,
                    "The second execution independently previews resources after the first payment.");
                await Decide(profile == "ECHO_PAY_BOTH");
            }
            var kept = profile is "BASE_PAY" or "ECHO_PAY_BOTH" ? 2 : 1;
            Check(state.PendingRuleChoice is null && state.StackItems.Count == kept
                && state.StackItems.All(s => s.SourceObjectId != counter), "Only the explicit refused execution counters its target.");
            for (var i = 0; state.StackItems.Count > 0 && i < 3; i++) await Top();
            Check(state.StackItems.Count == 0 && state.TriggerQueue.Count == 0 && state.PendingRuleChoice is null
                && state.PendingPayment is null && state.PendingEffectPlay is null && state.Status == MatchStatuses.InProgress,
                "The ordinary spells finish without game-state or history injection.");
            Check(state.PlayerZones["P1"].Hand.Count == hand + kept && state.PlayerZones["P1"].MainDeck.Count == deckCount - kept
                && spellSources.All(s => state.PlayerZones["P1"].Graveyard.Contains(s))
                && state.PlayerZones["P2"].Graveyard.Contains(counter), "Only surviving original spells draw and every physical spell ends in its owner's graveyard.");
            Check(state.RunePools["P1"].Mana == (profile == "BASE_DECLINE" ? 4 : profile == "ECHO_PAY_BOTH" ? 0 : 2),
                "Every accepted payment spends exactly two real mana; explicit decline remains allowed with resources.");
            var final = await Cold(); var events = final.Events.Skip(firstEvent).Select(e => e.Event).ToArray();
            var counters = events.Where(e => e.Kind == "STACK_ITEM_COUNTERED").ToArray();
            Check(kept == 2 ? counters.Length == 0 : counters.Length == 1
                && Text(counters[0], "stackItemId") == targets[profile == "BASE_DECLINE" ? 0 : 1],
                "Original event history identifies the exact countered target.");
            Check(events.Count(e => e.Kind == "COST_PAID" && Text(e, "reason") == "COUNTER_TARGET_MANA_PAYMENT")
                == (profile == "BASE_DECLINE" ? 0 : profile == "ECHO_PAY_BOTH" ? 2 : 1), "Independent payment event count.");
            if (echo) Check(events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"
                && Text(e, "effectKind") == "HARD_BARGAIN_COUNTER_SPELL_UNLESS_CONTROLLER_PAYS_2") == 2,
                "Printed Echo executes twice with independent choices.");
            var path = Path.Combine(output, $"conditional-counter-{format}-{profile}-original-frame.json.gz");
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var zip = new GZipStream(file, CompressionLevel.SmallestSize)) await JsonSerializer.SerializeAsync(zip, final, json);
            results.Add(new { room, format = format.ToString(), profile, commands = count, events = final.Events.Count,
                coldRestores = colds, originalNextCommandsMatched = originalNext, atomicRejections = rejected,
                originalOpeningCommands = 32, originalBlankSeed = 0, sourceCardNo = "SFD·136/221", targetCardNo = "UNL-061/219",
                sourceObjectId = counter, targetStackItemIds = targets, targetSourceObjectIds = spellSources,
                actualRuleInputs = state.RuleInputs, originalFullReplayMatched = true,
                bothOriginalPrivateViewsAndPromptsExact = true, sevenOriginalTablesUnchangedByColdAndInvalidDirectIntents = true,
                noStateSeedOrAcceptedHistoryInjection = true, completeGameOrWholeCardCertification = false });

            async Task Top() { await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); await Submit(state.PriorityPlayerId!, new PassPriorityCommand()); }
            async Task Decide(bool pay)
            {
                var request = state.PendingRuleChoice?.Request ?? throw new InvalidOperationException("Actual target-controller payment is required.");
                Check(request.PlayerId == "P1" && request.Kind == "COUNTER_TARGET_MANA_PAYMENT" && request.ManaCost == 2
                    && request.Options.Any(o => o.Id == "SPEND_MANA:2"), "Actual target-controller optional two-mana payment.");
                await Cold();
                await Reject("P2", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["DECLINE"]));
                await Reject("P1", new PayCostCommand("stale-counter", "RULE_REPLACEMENT", ["SPEND_MANA:2"]));
                await Reject("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_MANA:0"]));
                await Reject("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["SPEND_MANA:2", "DECLINE"]));
                await Submit("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "SPEND_MANA:2" : "DECLINE"]));
                await Cold();
            }
            async Task Submit(string actor, GameCommand command)
            {
                if (command is SubmitDeckCommand deck)
                    foreach (var checkFormat in new[] { OfficialDeckFormat.CoreRules, OfficialDeckFormat.ChinaStandard20260724 })
                        Check(OfficialDeckValidator.Validate(new(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields), catalog, checkFormat).IsValid,
                            "Both declared engine formats require legal original decks.");
                var fields = JsonSerializer.SerializeToNode(command, command.GetType(), json)!.AsObject();
                fields["promptId"] = session.PromptFor(actor).PromptId; fields["snapshotTick"] = state.Tick;
                var raw = JsonSerializer.SerializeToElement(fields, json); var intent = "original-conditional-counter-" + count;
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
                var result = await engine.ResolveAsync(state, new("rejected-conditional-counter", actor, command.CmdType), command, default);
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
            status = "REAL_POSTGRES_ORIGINAL_CONDITIONAL_COUNTER_EIGHT_BRANCHES_COLD_REPLAY_PASS",
            regionalTournamentEligibilityCertified = false, results }, json));
    }
    private static string? Text(GameEvent e, string key)
    {
        var payload = JsonSerializer.SerializeToElement(e.Payload);
        return payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }
    private static void Check(bool condition, string? message) { if (!condition) throw new InvalidOperationException(message); }
}
