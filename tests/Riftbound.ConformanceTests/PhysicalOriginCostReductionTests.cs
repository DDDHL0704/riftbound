using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.CardCatalog;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PhysicalOriginCostReductionTests
{
    private const string VoidDrone = "SFD·010/221";
    private const string DragUnder = "SFD·164/221";
    private const string Source = "ATK_TOP";

    [Theory]
    [InlineData(VoidDrone, 1)]
    [InlineData(DragUnder, 3)]
    public async Task RealRevealedDeckPlayQuotesOffersAndPaysTheNonHandReduction(string card, int mana)
    {
        var initial = Position(card, mana);
        var driver = await RevealSource(initial, "exact-" + mana);
        Assert.Equal("STACK", driver.State.ObjectLocations[Source].Zone);
        Assert.DoesNotContain(Source, driver.State.PlayerZones["P1"].Hand);
        var command = Command(card);
        var before = MatchStateHasher.Hash(driver.State);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1",
            PlayCostPreviewTests.Request(driver.State, command));
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(before, MatchStateHasher.Hash(driver.State));
        var play = ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!
            .Single(candidate => candidate.Action == CommandTypes.PlayCard);
        Assert.Contains(play.Sources!, source => source.Id == Source);
        var paid = await driver.Act("P1", command);
        Assert.Equal(mana, Assert.Single(paid.Events, ev => ev.Kind == "COST_PAID").Payload["mana"]);
        Assert.Equal(0, paid.State.RunePools["P1"].Mana);
        if (card == VoidDrone) Assert.Contains(Source, paid.State.PlayerZones["P1"].Base);
        else
        {
            await driver.Top();
            Assert.Contains("ENEMY1", driver.State.PlayerZones["P2"].Graveyard);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(VoidDrone, 3)]
    [InlineData(DragUnder, 5)]
    public async Task ARealHandPlayKeepsTheFullPrintedManaCost(string card, int printedMana)
    {
        var initial = Position(card, printedMana);
        var own = initial.PlayerZones["P1"];
        initial = initial with
        {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones)
            { ["P1"] = own with { MainDeck = own.MainDeck.Where(id => id != Source).ToArray(), Hand = [..own.Hand, Source] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations)
            { [Source] = new("P1", "HAND") }
        };
        var driver = AttackDeckPlayTests.Driver(initial, "hand-" + printedMana);
        var command = Command(card);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1",
            PlayCostPreviewTests.Request(driver.State, command));
        Assert.Equal(printedMana, quote.Cost!.Mana);
        Assert.True(quote.CanPay, quote.Message);
        var result = await driver.Act("P1", command);
        Assert.Equal(printedMana, Assert.Single(result.Events, ev => ev.Kind == "COST_PAID").Payload["mana"]);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        if (card == DragUnder) await driver.Top();
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(VoidDrone, 1)]
    [InlineData(DragUnder, 3)]
    public async Task ReducedCostStillRejectsOneMissingManaWithoutMutatingThePendingPlay(string card, int mana)
    {
        var initial = Position(card, mana - 1);
        var driver = await RevealSource(initial, "insufficient-" + mana);
        var command = Command(card);
        var engine = new CoreRuleEngine();
        var before = MatchStateHasher.Hash(driver.State);
        var quote = engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.MissingMana);
        Assert.False(quote.CanPay);
        var rejected = await engine.ResolveAsync(driver.State,
            new("insufficient-origin", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        Assert.Empty(rejected.Events);
        Assert.Equal(driver.State.PendingEffectPlay, rejected.State.PendingEffectPlay);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static PlayCardCommand Command(string card)
        => new(Source, card, card == DragUnder ? ["ENEMY1"] : []);

    private static MatchState Position(string card, int mana)
    {
        var state = DeckPlaySequenceTests.TopCard(AttackDeckPlayTests.Position(), Source, card);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(mana, 20) } };
    }

    private static async Task<StandbyDefendObserverTests.Driver> RevealSource(MatchState initial, string branch)
    {
        var driver = AttackDeckPlayTests.Driver(initial, "physical-origin-" + branch);
        await AttackDeckPlayTests.Attack(driver);
        await driver.Top();
        await AttackDeckPlayTests.Reveal(driver);
        await DeckPlaySequenceTests.Select(driver, Source);
        Assert.NotNull(driver.State.PendingEffectPlay);
        return driver;
    }

    [Theory]
    [InlineData(VoidDrone, 1)]
    [InlineData(DragUnder, 3)]
    public async Task ARealEmptyOpeningRecoversThePhysicalSourceAndQuotedCost(string card, int mana)
    {
        var (frame, source, target) = await NormalOpening(card);
        Assert.True(frame.IsConsistent);
        Assert.Equal(0, frame.ReplayInitialState!.Tick);
        Assert.Equal(0, frame.ReplayInitialState.Seed);
        Assert.Empty(frame.ReplayInitialState.CardObjects);
        var session = await new InMemoryMatchSessionRegistry(new CoreRuleEngine(), NoopMatchJournal.Instance,
            new SourceStore(frame)).GetOrCreateAsync(frame.RoomId, default);
        Assert.NotNull(session);
        var state = frame.AuthoritativeState!;
        Assert.Equal("STACK", state.ObjectLocations[source].Zone);
        Assert.DoesNotContain(source, state.PlayerZones["P1"].Hand);
        var command = new PlayCardCommand(source, card, card == DragUnder ? [target] : [], OptionalCosts: []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(frame.ReplayInitialState, frame.Commands,
            state, new CoreRuleEngine(), default, frame.Events);
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_NONHAND_COST_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence);
            var path = Path.Combine(evidence, "physical-origin-cost-" + (card == VoidDrone ? "unit" : "spell") + ".json");
            Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new {
                sourceRoomId = frame.RoomId, seats = frame.ReplayInitialState.Seats,
                expectedCardNo = card, expectedSourceObjectId = source, expectedTargetObjectId = target,
                expectedMana = mana, expectedCardReduction = 2, acceptedCommands = frame.Commands,
                originalOpeningSeed = frame.ReplayInitialState.Seed,
                scope = "New empty-seating ordinary commands with a fixed case-specific room/deck recipe; original regression seeds unchanged. No developer state command, future-deck inspection or imported snapshot."
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }

    private sealed class SourceStore(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string room, CancellationToken token) => ValueTask.FromResult<MatchRecoveryFrame?>(frame);
    }

    private sealed class OpeningJournal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { Entries.Add(entry); return ValueTask.CompletedTask; }
    }

    private static async Task<(MatchRecoveryFrame Frame, string Source, string Target)> NormalOpening(string desired)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var allowed = catalog with { Cards = catalog.Cards.Where(c => ChinaStandardBanList.IsAllowed(c, OfficialDeckFormat.ChinaStandard20260724)).ToArray() };
        var yellow = allowed.Cards.Where(c => c.CardCategory == "rune" && c.CardColorList.Contains("yellow"))
            .OrderBy(c => c.CardNo, StringComparer.Ordinal).First().CardNo;
        var builder = typeof(PreconstructedDeckCatalog).GetMethod("BuildLowCurveDeck", BindingFlags.Static | BindingFlags.NonPublic)!;
        var deck = (OfficialDecklist)builder.Invoke(null, [allowed, allowed.Cards.ToDictionary(c => c.CardNo, StringComparer.Ordinal),
            "SFD·187/221", AttackDeckPlayTests.Queen, new[] { desired, desired, desired },
            new[] { "OGN·277/298", "OGN·278/298", "OGN·294/298" }, Enumerable.Repeat(yellow, 12).ToArray()])!;
        var legal = OfficialDeckValidator.Validate(deck, catalog, OfficialDeckFormat.ChinaStandard20260724);
        Assert.True(legal.IsValid, string.Join("; ", legal.Errors));
        var initial = MatchReplayInitialStateBuilder.FromSeats("normal-physical-cost-" + (desired == VoidDrone ? "unit" : "spell-case-1"),
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" });
        var journal = new OpeningJournal(); var session = new MatchSession(initial, new CoreRuleEngine(), journal); var state = initial;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
        async Task Act(string actor, GameCommand command)
        {
            var raw = JsonSerializer.SerializeToElement(command, command.GetType(), json);
            var id = "normal-physical-cost-" + journal.Entries.Count;
            var result = command switch {
                SubmitDeckCommand submitted => await session.SubmitDeckAsync(actor, id, submitted, raw, default),
                ReadyCommand => await session.ReadyAsync(actor, id, raw, default),
                _ => await session.SubmitAsync(actor, id, command, raw, default)
            };
            Assert.True(result.Accepted, command.CmdType + ": " + result.ErrorMessage); state = result.State;
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_NONHAND_COST_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence) && state.PendingCardChoice is { PlayerId: "P1" } visible)
            {
                Directory.CreateDirectory(evidence);
                await File.AppendAllTextAsync(Path.Combine(evidence, desired == VoidDrone ? "unit-choice-trace.jsonl" : "spell-choice-trace.jsonl"),
                    JsonSerializer.Serialize(new { state.Tick, state.TurnNumber, state.PlayerScores, visible.ChoiceWindow,
                        legal = visible.LegalObjectIds.Select(id => state.CardObjects.GetValueOrDefault(id)?.CardNo ?? id),
                        context = visible.ContextObjectIds.Select(id => state.CardObjects.GetValueOrDefault(id)?.CardNo ?? id),
                        knownDesiredInOwnHand = state.PlayerZones["P1"].Hand.Count(id => state.CardObjects[id].CardNo == desired) }) + "\n");
            }
        }
        var submit = new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields);
        await Act("P1", submit); await Act("P2", submit); await Act("P1", new ReadyCommand()); await Act("P2", new ReadyCommand());
        var first = state.ActivePlayerId; var second = state.OpeningSecondActionPlayerId!;
        await Act(first, new MulliganCommand(state.PlayerZones[first].Hand.Where(id => state.CardObjects[id].CardNo == desired).ToArray()));
        await Act(second, new MulliganCommand(state.PlayerZones[second].Hand.Where(id => state.CardObjects[id].CardNo == desired).ToArray()));
        for (var turn = 0; state.TurnPlayerId != "P1" || Runes().Count() < 6; turn++)
        { Assert.True(turn < 40); await Act(state.TurnPlayerId, new EndTurnCommand()); }
        await Resources(true);
        var queen = state.PlayerZones["P1"].ChampionZone.Single();
        await Act("P1", new PlayCardCommand(queen, AttackDeckPlayTests.Queen, [], OptionalCosts: []));
        await NextOwnTurn();
        var field = state.BattlefieldStates.Keys.Order(StringComparer.Ordinal).First();
        string? source = null;
        for (var attempt = 0; attempt < 12 && source is null; attempt++)
        {
            Assert.Equal("IN_PROGRESS", state.Status);
            await Resources(desired == DragUnder);
            if (state.ObjectLocations[queen].Zone == "BATTLEFIELD")
            { await Act("P1", new MoveUnitCommand(queen, "BATTLEFIELD", "BASE", [])); await NextOwnTurn(); await Resources(desired == DragUnder); }
            await Act("P1", new MoveUnitCommand(queen, "BASE", "BATTLEFIELD:" + field, []));
            for (var step = 0; step < 40 && source is null; step++)
            {
                if (state.PendingRuleChoice is { } rule)
                {
                    var option = rule.Request.Options.FirstOrDefault(o => o.Id == "REVEAL")
                        ?? rule.Request.Options.FirstOrDefault(o => o.Id == "SKIP");
                    Assert.NotNull(option);
                    await Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", [option.Id]));
                }
                else if (state.PendingCardChoice is { } choice)
                {
                    var found = choice.LegalObjectIds.FirstOrDefault(id => state.CardObjects.TryGetValue(id, out var c) && c.CardNo == desired);
                    if (found is not null)
                    { await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [found])); source = found; }
                    else if (choice.ChoiceWindow == "TRIGGER_COST_CONFIRMATION"
                        && choice.LegalObjectIds.Contains(state.PlayerZones["P1"].LegendZone.Single()))
                        await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                            [state.PlayerZones["P1"].LegendZone.Single()]));
                    else if (choice.LegalObjectIds.Contains("PAY"))
                        await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["PAY"]));
                    else if (choice.LegalObjectIds.Contains("DECLINE"))
                        await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DECLINE"]));
                    else
                    { Assert.Equal(0, choice.RequiredCount); await Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); }
                }
                else if (state.StackItems.Count > 0) await Act(state.PriorityPlayerId!, new PassPriorityCommand());
                else if (state.SpellDuelState is { IsActive: true })
                {
                    var actor = ResolutionResult.BuildPrompts(state).Single(p => p.Value.EnabledActions().Contains(CommandTypes.PassFocus)).Key;
                    await Act(actor, new PassFocusCommand());
                }
                else break;
            }
            if (source is null) await NextOwnTurn();
        }
        Assert.NotNull(source); Assert.NotNull(state.PendingEffectPlay);
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var sequence = journal.Entries[^1].CompletedEventSequence; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
        return (new(state.RoomId, state.Tick, sequence, commands, events, views, [], state, initial,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state)), source!, queen);

        IEnumerable<string> Runes() => state.PlayerZones["P1"].Base.Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard));
        async Task Resources(bool power)
        {
            foreach (var rune in Runes().Where(id => !state.CardObjects[id].IsExhausted).ToArray()) await Act("P1", new TapRuneCommand(rune));
            if (power && state.RunePools["P1"].PowerByTrait.GetValueOrDefault("yellow") == 0)
                await Act("P1", new RecycleRuneCommand(Runes().First()));
        }
        async Task NextOwnTurn()
        { do { await Act(state.TurnPlayerId, new EndTurnCommand()); } while (state.TurnPlayerId != "P1"); }
    }
}
