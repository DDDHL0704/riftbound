using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalSacrificeOpeningTests
{
    private const string Atakhan = "UNL-170/219";
    private const string Grant = "SFD·029/221";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEmptyOpeningColdRecoversAndPaysTheSameOptionalSacrificeForHandOrGrantedHaste(bool outsideHand)
    {
        var (prefix, journal, source, grant) = await Opening(outsideHand);
        Assert.True(prefix.IsConsistent); Assert.Equal(0, prefix.ReplayInitialState!.Tick);
        Assert.Equal(0, prefix.ReplayInitialState.Seed); Assert.Empty(prefix.ReplayInitialState.CardObjects);
        var before = prefix.AuthoritativeState!;
        Assert.Equal(outsideHand ? "STACK" : "HAND", before.ObjectLocations[source].Zone);
        Assert.Equal(Grant, before.CardObjects[grant].CardNo);
        var engine = new CoreRuleEngine();
        var session = await new InMemoryMatchSessionRegistry(engine, journal, new Store(prefix)).GetOrCreateAsync(prefix.RoomId, default);
        var costs = new[] { "DESTROY_FRIENDLY_UNIT:" + grant }
            .Concat(outsideHand ? [HasteOptionalCostNames.HasteReady] : Array.Empty<string>()).ToArray();
        var command = new PlayCardCommand(source, Atakhan, [], OptionalCosts: costs);
        var quote = engine.PreviewPlayCard(before, "P1", new("sacrifice-quote", session.PromptFor("P1").PromptId!, before.Tick, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(outsideHand ? 8 : 7, quote.Cost!.Mana);
        Assert.Equal(outsideHand ? 4 : 3, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        GameCommand submitted = command; var raw = Raw(command); var native = false;
        string? nativeDirectory = null;
        if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_SACRIFICE_NATIVE_EVIDENCE") is { Length: > 0 } nativeRoot)
        {
            nativeDirectory = Path.Combine(nativeRoot, outsideHand ? "haste" : "hand"); Directory.CreateDirectory(nativeDirectory);
            void Export(string file, object value) => File.WriteAllText(Path.Combine(nativeDirectory, file),
                JsonSerializer.Serialize(value, value.GetType(), Json));
            Export("play-prompt.json", session.PromptFor("P1"));
            Export("play-snapshot.json", ResolutionResult.BuildSnapshots(before)["P1"]);
            Export("play-selection.json", command); Export("play-quote.json", quote);
            Export("steps.json", new[] { new { name = "play", action = CommandTypes.PlayCard } });
            var nativeCommand = Path.Combine(nativeDirectory, "play-command.json");
            if (File.Exists(nativeCommand))
            {
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(nativeCommand));
                raw = document.RootElement.Clone(); submitted = GameCommandJsonMapper.Map(raw); native = true;
            }
        }
        var result = await session.SubmitAsync("P1", "optional-sacrifice-confirm", submitted, raw, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(grant, result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(source, result.State.PlayerZones["P1"].Base);
        Assert.Equal(!outsideHand, result.State.CardObjects[source].IsExhausted);
        Assert.Equal(before.RunePools["P1"].Mana - (outsideHand ? 8 : 7), result.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].TotalPower - (outsideHand ? 4 : 3), result.State.RunePools["P1"].TotalPower);
        var final = Frame(prefix.ReplayInitialState, journal, result.State);
        Assert.NotNull(await new InMemoryMatchSessionRegistry(engine, NoopMatchJournal.Instance, new Store(final)).GetOrCreateAsync(final.RoomId, default));
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(final, engine, default));
        if (nativeDirectory is not null)
            await File.WriteAllTextAsync(Path.Combine(nativeDirectory, "replay.json"), JsonSerializer.Serialize(new {
                nativeCommands = native ? 1 : 0, accepted = 1, coldRestores = 2, originalOpeningTick = 0,
                fullOriginalActionEventStateReplayMatched = true, finalHash = MatchStateHasher.Hash(result.State),
                expectedMana = outsideHand ? 8 : 7, expectedPower = outsideHand ? 4 : 3, expectedReady = outsideHand
            }, Json));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_SACRIFICE_EVIDENCE") is { Length: > 0 } evidence)
        {
            Directory.CreateDirectory(evidence);
            var path = Path.Combine(evidence, "optional-sacrifice-" + (outsideHand ? "haste" : "hand") + ".json");
            Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new {
                sourceRoomId = prefix.RoomId, seats = prefix.ReplayInitialState.Seats, expectedSourceObjectId = source,
                expectedCardNo = Atakhan, expectedSacrificeObjectId = grant, expectedSacrificeCardNo = Grant,
                expectedMana = outsideHand ? 8 : 7, expectedPower = outsideHand ? 4 : 3,
                expectedReady = outsideHand, acceptedCommands = prefix.Commands,
                originalOpeningSeed = prefix.ReplayInitialState.Seed,
                scope = "New legal empty opening, ordinary hand/champion/rune/attack/reveal choices only; original143/110/seed424242 unchanged. No imported state, future deck inspection, developer commands or native-game migration."
            }, Json));
        }
    }

    private static JsonElement Raw(GameCommand command) => JsonSerializer.SerializeToElement(command, command.GetType(), Json);
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(room == frame.RoomId ? frame : null);
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private static MatchRecoveryFrame Frame(MatchState initial, Journal journal, MatchState state)
    {
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state));
    }

    private static async Task<(MatchRecoveryFrame, Journal, string, string)> Opening(bool outsideHand)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        string Rune(string trait) => allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains(trait))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·187/221", Grant, outsideHand ? new[] { Atakhan, Atakhan, Atakhan, "UNL-009/219", "UNL-009/219", "UNL-009/219" }
                : new[] { Atakhan, Atakhan, Atakhan }, new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" },
            Enumerable.Repeat(Rune("yellow"), 6).Concat(Enumerable.Repeat(Rune("red"), 6)).ToArray()])!;
        if (outsideHand)
        {
            // Submit a fixed interleaved decklist; the engine still shuffles it.
            // No future runtime deck order is read to construct or play this room.
            var main = deck.MainDeck.Where(card => card != Atakhan).ToList();
            // Independent seed0/P1 MAIN_DECK vector: these input positions
            // put copies at shuffled positions12/18/25, after ordinary setup draws.
            foreach (var position in new[] { 29, 31, 34 }) main.Insert(position, Atakhan);
            deck = deck with { MainDeck = main };
        }
        Assert.Equal(3, deck.MainDeck.Count(card => card == Atakhan));
        var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("normal-optional-sacrifice-" + (outsideHand ? "haste-case-3" : "hand-case-1"),
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new Journal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal); var state = initial;
        async Task Act(string actor, GameCommand command)
        {
            var id = "normal-optional-sacrifice-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, Raw(command), default),
                ReadyCommand => await session.ReadyAsync(actor, id, Raw(command), default),
                _ => await session.SubmitAsync(actor, id, command, Raw(command), default)
            };
            Assert.True(result.Accepted, command.CmdType + ": " + result.ErrorMessage);
            state = result.State; FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_SACRIFICE_EVIDENCE") is { Length: > 0 } evidence
                && state.PendingCardChoice is { } visible)
            {
                Directory.CreateDirectory(evidence);
                await File.AppendAllTextAsync(Path.Combine(evidence, "visible-choices-" + outsideHand + ".jsonl"),
                    JsonSerializer.Serialize(new { state.Tick, state.TurnNumber, state.Status, visible.ChoiceWindow,
                        legal = visible.LegalObjectIds.Select(id => state.CardObjects.GetValueOrDefault(id)?.CardNo ?? id),
                        knownDesiredInOwnHand = state.PlayerZones["P1"].Hand.Count(id => state.CardObjects[id].CardNo == Atakhan) }) + "\n");
            }
        }
        var submitted = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("P1", submitted); await Act("P2", submitted); await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await Act(first, new MulliganCommand(outsideHand ? state.PlayerZones[first].Hand.Where(id => state.CardObjects[id].CardNo == Atakhan).ToArray() : []));
        await Act(second, new MulliganCommand(outsideHand ? state.PlayerZones[second].Hand.Where(id => state.CardObjects[id].CardNo == Atakhan).ToArray() : []));
        for (var step = 0; state.TurnPlayerId != "P1" || Runes().Count() < 8 || Runes().Count(IsYellow) < 4
            || !outsideHand && !state.PlayerZones["P1"].Hand.Any(id => state.CardObjects[id].CardNo == Atakhan); step++)
        { Assert.True(step < 64); await Act(state.TurnPlayerId, new EndTurnCommand()); }
        await TapAll();
        var grant = state.PlayerZones["P1"].ChampionZone.Single();
        await Act("P1", new PlayCardCommand(grant, Grant, []));
        await NextOwnTurn();
        string? source = null;
        if (!outsideHand)
            source = state.PlayerZones["P1"].Hand.First(id => state.CardObjects[id].CardNo == Atakhan);
        else
        {
            var field = state.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First();
            for (var attempt = 0; source is null; attempt++)
            {
                Assert.True(attempt < 8 && state.Status == MatchStatuses.InProgress, "Only choose an actually revealed Atakhan");
                if (state.ObjectLocations[grant].Zone == "BATTLEFIELD")
                { await Act("P1", new MoveUnitCommand(grant, "BATTLEFIELD", "BASE", [])); await NextOwnTurn(); }
                await TapAll();
                await Act("P1", new MoveUnitCommand(grant, "BASE", "BATTLEFIELD:" + field, []));
                for (var step = 0; step < 50 && source is null; step++)
                {
                    if (state.PendingRuleChoice is { } rule)
                    {
                        var option = rule.Request.Options.FirstOrDefault(o => o.Id == "REVEAL") ?? rule.Request.Options.First(o => o.Id == "SKIP");
                        await Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [option.Id]));
                    }
                    else if (state.PendingCardChoice is { } choice)
                    {
                        var found = choice.LegalObjectIds.FirstOrDefault(id => state.CardObjects.TryGetValue(id, out var card) && card.CardNo == Atakhan);
                        if (found is not null)
                        { await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [found])); source = found; }
                        else if (choice.ChoiceWindow == "TRIGGER_COST_CONFIRMATION" && choice.LegalObjectIds.Contains(state.PlayerZones["P1"].LegendZone.Single()))
                            await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [state.PlayerZones["P1"].LegendZone.Single()]));
                        else if (choice.LegalObjectIds.Contains("PAY")) await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["PAY"]));
                        else if (choice.LegalObjectIds.Contains("DECLINE")) await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DECLINE"]));
                        else { Assert.Equal(0, choice.RequiredCount); await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); }
                    }
                    else if (state.StackItems.Count > 0) await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                    else if (ResolutionResult.BuildPrompts(state).FirstOrDefault(p => p.Value.EnabledActions().Contains(CommandTypes.PassFocus)).Key is { } actor)
                        await Act(actor, new PassFocusCommand());
                    else break;
                }
                if (source is null)
                {
                    // Use a genuinely drawn ready spell to return the attacker
                    // before the next holding score; never alter points or peek.
                    var ready = state.PlayerZones["P1"].Hand.FirstOrDefault(id => state.CardObjects[id].CardNo == "UNL-009/219");
                    if (ready is not null)
                    {
                        await Act("P1", new PlayCardCommand(ready, "UNL-009/219", [grant]));
                        for (var response = 0; state.StackItems.Count > 0; response++)
                        { Assert.True(response < 20); await Act(state.PriorityPlayerId!, new PassPriorityCommand()); }
                        Assert.False(state.CardObjects[grant].IsExhausted);
                        await Act("P1", new MoveUnitCommand(grant, "BATTLEFIELD", "BASE", []));
                    }
                    await NextOwnTurn();
                }
            }
        }
        await TapAll();
        while (state.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow) < (outsideHand ? 4 : 3))
            await Act("P1", new RecycleRuneCommand(Runes().First(IsYellow)));
        return (Frame(initial, journal, state), journal, source!, grant);

        IEnumerable<string> Runes() => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        bool IsYellow(string id) => PrintedPowerCostRules.ForCard(state.CardObjects[id].CardNo!).Traits.Contains(RuneTrait.Yellow);
        async Task TapAll()
        {
            var ready = Runes().Where(id => !state.CardObjects[id].IsExhausted).ToArray();
            if (ready.Length > 0) await Act("P1", new TapRuneCommand(ready[0], ready));
        }
        async Task NextOwnTurn()
        { do { await Act(state.TurnPlayerId, new EndTurnCommand()); } while (state.TurnPlayerId != "P1"); }
    }
}
