using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class PakaaProtectorRevealTests
{
    [Fact]
    public async Task ExactCurrentCnPakaaProtectorPrintingHasExecutableOrdinaryUnitDefinition()
    {
        using var source = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "pakaa-protector-source.json")));
        var raw = source.RootElement.GetProperty("rawApiCard");
        Assert.Equal(37721, raw.GetProperty("id").GetInt64());
        Assert.Equal("VEN·033", raw.GetProperty("cardNo").GetString());
        Assert.False(raw.GetProperty("isPreview").GetBoolean());
        Assert.Equal("VEN·033/166", source.RootElement.GetProperty("printedImageCardNo").GetString());
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("VEN·033", out var definition),
            "Exact current CN printing VEN·033 is absent from the executable unit registry.");
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == "VEN·033");
        Assert.Equal("帕卡监护者", card.CardName);
        Assert.Equal(raw.GetProperty("cardEffect").GetString(), card.CardEffect);
        Assert.Equal(5, card.Energy); Assert.Equal(4, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.True(definition.PlaysSourceToBaseAsUnit);
        Assert.Equal(5, definition.ManaCost); Assert.Equal(4, definition.SourceUnitPower);
    }

    [Fact]
    public async Task ControlledUnitPlayThenStandardMoveCapturesItsOwnRevealTrigger()
    {
        var state = await EmpowerTests.Enter("VEN·033");
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "HOST" ? p.Value with { IsExhausted = false } : p.Value) };
        var deck = state.PlayerZones["P1"].MainDeck.ToArray();
        var field = state.BattlefieldStates.Keys.First();
        var moved = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:" + field));
        var captured = Assert.Single(moved.State.TriggerQueue.Select(t => t.MovementObserver)
            .Concat(moved.State.StackItems.Select(i => i.MovementObserver)), c => c is { CardNo: "VEN·033" });
        Assert.Equal("UNIT_MOVED_REVEAL_TOP_DRAW_UNIT_OR_TRASH_POWER", captured!.Kind);
        Assert.Equal("P1", captured.Unit.ControllerId);
        Assert.Equal(state.CardObjects["HOST"].ObjectGeneration, captured.SourceGeneration);
        Assert.Equal(deck, moved.State.PlayerZones["P1"].MainDeck);
    }

    [Theory]
    [InlineData("OGN·103/298", 1, true)]
    [InlineData("OGN·103/298", 2, true)]
    [InlineData("OGN·104/298", 1, false)]
    [InlineData("OGN·104/298", 2, false)]
    [InlineData("SFD·022/221", 1, false)]
    [InlineData("SFD·022/221", 2, false)]
    [InlineData("OGN·103/298", 0, false)]
    public async Task RevealUsesOnlyTheOriginalTopAndDrawsAUnitOrTrashesNonunitWithTemporaryMight(
        string topNo, int deckCount, bool draws)
    {
        var state = await Position(topNo, deckCount);
        Assert.Equal(4, ArmamentMightTests.Might(state));
        var deck = state.PlayerZones["P1"].MainDeck.ToArray();
        var hand = state.PlayerZones["P1"].Hand.ToArray();
        var grave = state.PlayerZones["P1"].Graveyard.ToArray();
        var moved = await Move(state);
        Restore(moved.State); ArmamentMightTests.PublicRecovery(moved.State);
        var resolved = await Top(moved.State);
        Assert.Equal(deck.Skip(1), resolved.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(draws ? hand.Append(deck[0]) : hand, resolved.State.PlayerZones["P1"].Hand);
        Assert.Equal(!draws && deckCount > 0 ? grave.Append(deck[0]) : grave, resolved.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(draws || deckCount == 0 ? 4 : 6, ArmamentMightTests.Might(resolved.State));
        Assert.DoesNotContain(resolved.Events, e => e.Kind == "BURNOUT_APPLIED");
        Assert.Equal(draws ? 1 : 0, resolved.Events.Count(e => e.Kind == "CARD_DRAWN"));
        Assert.Equal(!draws && deckCount > 0 ? 1 : 0, resolved.Events.Count(e => e.Kind == "CARDS_MILLED"));
        if (deckCount == 0) Assert.DoesNotContain(resolved.Events, e => e.Kind == "CARDS_REVEALED");
        else
        {
            var reveal = Assert.Single(resolved.Events, e => e.Kind == "CARDS_REVEALED");
            var payload = JsonSerializer.SerializeToElement(reveal.Payload);
            Assert.Equal(new[] { deck[0] }, payload.GetProperty("cardIds").EnumerateArray().Select(v => v.GetString()));
            Assert.Equal(1, payload.GetProperty("count").GetInt32());
            if (deckCount > 1)
            {
                Assert.DoesNotContain('"' + deck[1] + '"', JsonSerializer.Serialize(resolved.Snapshots["P2"]));
                Assert.DoesNotContain('"' + deck[1] + '"', JsonSerializer.Serialize(resolved.Events));
            }
            if (draws) Assert.DoesNotContain('"' + deck[0] + '"', JsonSerializer.Serialize(resolved.Snapshots["P2"]));
        }
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RevealedNocturneUsesTheSharedBanishAndMandatoryPlayConfirmationWithoutSubstitutingTheNextCard(bool banish, bool play)
    {
        var initial = ControlledJournalPosition(DeckObservationPlayTests.Nocturne);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Move(driver); await driver.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", choice.Request.Kind);
        Assert.Equal(4, ArmamentMightTests.Might(driver.State));
        var view = Assert.IsType<DeckObservationView>(choice.Request.Preview!.DeckObservationView);
        Assert.True(view.Public); Assert.Equal(new[] { "D0" }, view.Cards.Keys);
        foreach (var player in new[] { "P1", "P2" })
            Assert.Contains("\"D0\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)[player]));
        Assert.DoesNotContain("\"D1\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        await DeckObservationPlayTests.Decide(driver, banish);
        Assert.Equal(new[] { "D1" }, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(4, ArmamentMightTests.Might(driver.State));
        if (banish)
        {
            Assert.Empty(driver.State.PlayerZones["P1"].Hand);
            var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
            var observed = pending.Parent.ObservedPlayPermission!.Context;
            Assert.Equal("VEN·033", observed.Instruction.Parent.CardNo);
            Assert.Equal(TriggerKinds.UnitMovedRevealTopDrawUnitOrTrashPower, observed.Instruction.Parent.MovementObserver!.Kind);
            Assert.Equal("REVEAL", observed.Instruction.Kind);
            Assert.Equal(new[] { "D0" }, observed.Instruction.Seen.Select(b => b.ObjectId));
            var blocked = await new CoreRuleEngine().ResolveAsync(driver.State,
                new("blocked-paka-turn", "P1", CommandTypes.EndTurn), new EndTurnCommand(), default);
            Assert.False(blocked.Accepted); Assert.Empty(blocked.Events);
            Assert.Equal(MatchStateHasher.Hash(driver.State), MatchStateHasher.Hash(blocked.State));
            if (play) await driver.Act("P1", new PlayCardCommand("D0", DeckObservationPlayTests.Nocturne, []));
            else await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("D0", play ? driver.State.PlayerZones["P1"].Base : driver.State.PlayerZones["P1"].Banished);
        }
        else Assert.Contains("D0", driver.State.PlayerZones["P1"].Hand);
        Assert.Null(driver.State.PendingEffectPlay); Assert.Null(driver.State.ObservedDeckPlays);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN" && banish);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARDS_MILLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task PublicRevealPreviewRejectsPrivateViewWrongGenerationAndUnseenCards()
    {
        var driver = new StandbyDefendObserverTests.Driver(ControlledJournalPosition(DeckObservationPlayTests.Nocturne));
        await Move(driver); await driver.Top();
        var state = driver.State; var pending = state.PendingRuleChoice!;
        var preview = pending.Request.Preview!; var original = preview.DeckObservationView!;
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var forgeries = new[] {
            original with { Public = false },
            original with { Cards = new Dictionary<string, long> { ["D1"] = state.CardObjects["D1"].ObjectGeneration } },
            original with { Cards = new Dictionary<string, long>(original.Cards) { ["D1"] = state.CardObjects["D1"].ObjectGeneration } },
            original with { Cards = new Dictionary<string, long> { ["D0"] = original.Cards["D0"] + 1 } }
        };
        foreach (var forged in forgeries)
        {
            var bad = state with { PendingRuleChoice = pending with { Request = pending.Request with {
                Preview = preview with { DeckObservationView = forged } } } };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(bad));
        }
    }

    [Theory]
    [InlineData("LOOK")]
    [InlineData("EXTRA_SEEN")]
    [InlineData("SEEN_GENERATION")]
    [InlineData("CARD_GENERATION")]
    [InlineData("POWER_COST")]
    [InlineData("EXECUTION")]
    [InlineData("SOURCE_GENERATION")]
    [InlineData("FUTURE_TICK")]
    public async Task ObservedPermissionRejectsForgedMovementSourceAndObservationBindings(string attack)
    {
        var driver = new StandbyDefendObserverTests.Driver(ControlledJournalPosition(DeckObservationPlayTests.Nocturne));
        await Move(driver); await driver.Top(); await DeckObservationPlayTests.Decide(driver, true);
        var state = driver.State; var original = Assert.Single(state.ObservedDeckPlays!);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var instruction = original.Instruction;
        var forged = attack switch {
            "LOOK" => original with { Instruction = instruction with { Kind = "LOOK" } },
            "EXTRA_SEEN" => original with { Instruction = instruction with {
                Seen = instruction.Seen.Append(new ObjectBinding("D1", state.CardObjects["D1"].ObjectGeneration)).ToArray() } },
            "SEEN_GENERATION" => original with { Instruction = instruction with {
                Seen = [instruction.Seen[0] with { Generation = instruction.Seen[0].Generation + 1 }] } },
            "CARD_GENERATION" => original with { Card = original.Card with { Generation = original.Card.Generation + 1 } },
            "POWER_COST" => original with { PowerCost = 0 },
            "EXECUTION" => original with { Instruction = instruction with { ExecutionIndex = 1 } },
            "SOURCE_GENERATION" => original with { Instruction = instruction with { Parent = instruction.Parent with {
                MovementObserver = instruction.Parent.MovementObserver! with {
                    SourceGeneration = instruction.Parent.MovementObserver!.SourceGeneration + 1 } } } },
            "FUTURE_TICK" => original with { CapturedTick = state.Tick + 1 },
            _ => throw new InvalidOperationException(attack)
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(WithObservedContext(state, forged)));
    }

    [Fact]
    public async Task CoordinatedEarlierObservationTickStillFailsTheOriginalActionLogReplay()
    {
        var initial = ControlledJournalPosition(DeckObservationPlayTests.Nocturne);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Move(driver); await driver.Top(); await DeckObservationPlayTests.Decide(driver, true);
        var original = Assert.Single(driver.State.ObservedDeckPlays!);
        Assert.True(original.CapturedTick > 0);
        var forged = WithObservedContext(driver.State, original with { CapturedTick = original.CapturedTick - 1 });
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(forged));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState WithObservedContext(MatchState state, ObservedDeckPlayContext context)
    {
        var original = Assert.Single(state.ObservedDeckPlays!);
        context = context with { Id = $"OBSERVED:{context.CapturedTick}:{context.Instruction.Parent.StackItemId}:{context.Instruction.ExecutionIndex}:{context.Card.ObjectId}" };
        var pending = state.PendingEffectPlay!;
        return state with { ObservedDeckPlays = [context],
            StackItems = state.StackItems.Select(item => item.ObservedDeckPlay?.Id == original.Id
                ? item with { StackItemId = context.Id, ObservedDeckPlay = context } : item).ToArray(),
            PendingEffectPlay = pending with { ChoiceId = "OBSERVED-PLAY:" + context.Id,
                Parent = pending.Parent with { StackItemId = "OBSERVED-PERMISSION:" + context.Id,
                    ObservedPlayPermission = pending.Parent.ObservedPlayPermission! with { Context = context } } } };
    }

    internal static async Task<MatchState> Position(string topNo = "OGN·104/298", int deckCount = 2)
        => WithDeck(await EmpowerTests.Enter("VEN·033"), topNo, deckCount);

    internal static MatchState ControlledJournalPosition(string topNo)
    {
        // The journal begins with a tick-zero controlled fixture.
        var state = AttachedRuleTextTests.Position("VEN·033", 0);
        Assert.Equal(0, state.Tick);
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "HOST" ? p.Value with { ManaCost = 5 } : p.Value) };
        return WithDeck(state, topNo, 2);
    }

    private static MatchState WithDeck(MatchState state, string topNo, int deckCount)
    {
        var oldDeck = state.PlayerZones["P1"].MainDeck.ToArray();
        if (deckCount > 0) state = DeckPlaySequenceTests.TopCard(state, oldDeck[0], topNo);
        var kept = oldDeck.Take(deckCount).ToArray();
        var removed = oldDeck.Skip(deckCount).ToArray();
        var locations = state.ObjectLocations.ToDictionary();
        foreach (var id in removed) locations[id] = new("P1", "GRAVEYARD");
        return state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key,
                p => p.Key == "HOST" ? p.Value with { IsExhausted = false } : p.Value),
            ObjectLocations = locations, PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { MainDeck = kept,
                    Graveyard = state.PlayerZones["P1"].Graveyard.Concat(removed).ToArray() } } };
    }

    internal static async Task<ResolutionResult> Move(MatchState state)
    {
        var moved = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:" + state.BattlefieldStates.Keys.First()));
        if (moved.State.TriggerQueue.Count > 0)
            moved = await Act(moved.State, "P1", new OrderTriggersCommand(OrderedTriggerIds:
                moved.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        return moved;
    }

    internal static async Task Move(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:" + driver.State.BattlefieldStates.Keys.First()));
        if (driver.State.TriggerQueue.Count > 0)
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }
}
