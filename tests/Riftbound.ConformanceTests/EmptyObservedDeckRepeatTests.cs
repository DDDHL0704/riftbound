using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EmptyObservedDeckRepeatTests
{
    private const string Nocturne = "OGN·194/298";

    [Theory]
    [InlineData("SFD·122/221", 2, true)]
    [InlineData("UNL-032/219", 3, true)]
    [InlineData("UNL-032/219", 3, false)]
    public async Task EmptyFirstViewStillExecutesThePaidRepeatBeforeObservedUnitsPlay(string card, int look, bool choose)
    {
        var nocturnes = Ids("NOCT", look); var tail = Ids("TAIL", 4);
        var initial = Position(card, nocturnes.Concat(tail), nocturnes);
        var driver = await Open(initial, card, true);
        await Banish(driver, look);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(tail.Take(look), choice.ContextObjectIds);
        Assert.Equal(1, driver.State.StackItems.Single(item => item.SourceObjectId == "SOURCE").CompletedDeckExecutions);
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(card == "SFD·122/221" ? 1 : 0, choice.RequiredCount);
        foreach (var (player, selected) in new[] { ("P2", new[] { tail[0] }), ("P1", new[] { nocturnes[0] }) })
        {
            var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("invalid", player, CommandTypes.ChooseCards),
                new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected), default);
            Assert.False(rejected.Accepted);
            Assert.Equal(MatchStateHasher.Hash(driver.State), MatchStateHasher.Hash(rejected.State));
        }
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, choose ? [tail[0]] : []));
        Assert.Equal(choose, driver.State.PlayerZones["P1"].Hand.Contains(tail[0]));
        Assert.Equal(tail.Skip(look), driver.State.PlayerZones["P1"].MainDeck.Take(tail.Length - look));
        Assert.Equal(tail.Where(id => !choose || id != tail[0]).Order(), driver.State.PlayerZones["P1"].MainDeck.Order());
        Assert.Equal(choose ? 1 : 0, driver.State.DrawLedger.Counts.GetValueOrDefault("P1"));
        await DeclineObservedPlays(driver, look);
        Assert.Equal(nocturnes.Order(), driver.State.PlayerZones["P1"].Banished.Order());
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "SOURCE");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·122/221", 2, false)]
    [InlineData("SFD·122/221", 2, true)]
    [InlineData("UNL-032/219", 3, false)]
    [InlineData("UNL-032/219", 3, true)]
    public async Task EmptyLastOrBothViewsFinishOnePaidItemWithoutObservingTheUntouchedTail(string card, int look, bool firstEmpty)
    {
        // Both-empty rows deliberately use an explicit midgame state. Six Nocturnes
        // are not a legal ordinary deck and are not ordinary-opening evidence.
        var first = Ids("FIRST", look); var second = Ids("SECOND", look); var tail = Ids("TAIL", 4);
        var nocturnes = (firstEmpty ? first : []).Concat(second).ToArray();
        var initial = Position(card, first.Concat(second).Concat(tail), nocturnes);
        var driver = await Open(initial, card, true);
        if (firstEmpty) await Banish(driver, look);
        else
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [first[0]]));
        }
        await Banish(driver, look);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(tail, driver.State.PlayerZones["P1"].MainDeck.Take(tail.Length));
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "SOURCE");
        var captured = driver.State.ObservedDeckPlays!;
        Assert.Equal(look, captured.Count(context => context.Instruction.ExecutionIndex == 1));
        Assert.Equal(firstEmpty ? look : 0, captured.Count(context => context.Instruction.ExecutionIndex == 0));
        await DeclineObservedPlays(driver, nocturnes.Length);
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(firstEmpty ? 0 : 1, driver.State.DrawLedger.Counts.GetValueOrDefault("P1"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·122/221", 2)]
    [InlineData("UNL-032/219", 3)]
    [InlineData("OGN·183/298", 3)]
    public async Task WithoutEchoAnEmptyViewDoesNotConsumeTheNextCards(string card, int look)
    {
        var nocturnes = Ids("NOCT", look); var tail = Ids("TAIL", 4);
        var initial = Position(card, nocturnes.Concat(tail), nocturnes);
        var driver = await Open(initial, card, false);
        await Banish(driver, look);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(tail, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Single(driver.State.PlayerZones["P1"].Graveyard, id => id == "SOURCE");
        await DeclineObservedPlays(driver, look);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(0, driver.State.DrawLedger.Counts.GetValueOrDefault("P1"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·122/221")]
    [InlineData("UNL-032/219")]
    public async Task EmptyOriginalDeckDoesNotBurnOutOrLeaveAnEchoContinuation(string card)
    {
        var initial = Position(card, [], []);
        initial = initial with { CardObjects = initial.CardObjects.Append(new KeyValuePair<string, CardObjectState>("GRAVE",
            new("GRAVE", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]))).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Append(new KeyValuePair<string, ObjectLocationState>("GRAVE", new("P1", "GRAVEYARD"))).ToDictionary(),
            PlayerZones = initial.PlayerZones.ToDictionary(pair => pair.Key,
                pair => pair.Key == "P1" ? pair.Value with { Graveyard = ["GRAVE"] } : pair.Value) };
        var driver = await Open(initial, card, true);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Empty(driver.State.StackItems);
        Assert.Empty(driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(new[] { "GRAVE", "SOURCE" }.Order(), driver.State.PlayerZones["P1"].Graveyard.Order());
        Assert.DoesNotContain(driver.Events(), entry => entry.Event.Kind is "BURNOUT" or "CARD_DRAWN" or "CARDS_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task PendingSecondViewRejectsIndependentCounterAndCompletionForgeries()
    {
        var nocturnes = Ids("NOCT", 2);
        var driver = await Open(Position("SFD·122/221", nocturnes.Concat(Ids("TAIL", 4)), nocturnes), "SFD·122/221", true);
        await Banish(driver, 2);
        Assert.NotNull(driver.State.PendingCardChoice);
        var item = driver.State.StackItems.Single(slot => slot.SourceObjectId == "SOURCE");
        foreach (var forged in new[] { item with { CompletedDeckExecutions = 0 }, item with { CompletedDeckExecutions = 2 }, item with { DeckChoiceCompleted = true } })
        {
            var state = driver.State with { StackItems = driver.State.StackItems.Select(slot => slot.StackItemId == item.StackItemId ? forged : slot).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), error => error.Contains("deck", StringComparison.Ordinal));
        }
    }

    private static string[] Ids(string prefix, int count) => Enumerable.Range(1, count).Select(i => prefix + i).ToArray();
    private static MatchState Position(string card, IEnumerable<string> deck, IReadOnlyList<string> nocturnes)
    {
        var ids = deck.ToArray();
        var cards = ids.ToDictionary(id => id, id => new CardObjectState(id,
            cardNo: nocturnes.Contains(id) ? Nocturne : "SFD·125/221", ownerId: "P1", controllerId: "P1",
            manaCost: nocturnes.Contains(id) ? 4 : 1, power: nocturnes.Contains(id) ? 4 : 1, tags: [CardObjectTags.UnitCard]));
        cards["SOURCE"] = new("SOURCE", cardNo: card, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard]);
        return new("EMPTY-OBSERVED-REPEAT", 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["SOURCE"], MainDeck = ids }, ["P2"] = PlayerZones.Empty }, cardObjects: cards);
    }
    private static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial, string card, bool echo)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("SOURCE", card, [], OptionalCosts: echo ? ["ECHO"] : []));
        Assert.Equal(echo ? 2 : 1, driver.State.StackItems.Single().EffectRepeatCount);
        await driver.Top(); return driver;
    }
    private static async Task Banish(StandbyDefendObserverTests.Driver driver, int count)
    {
        for (var i = 0; i < count; i++) await DeckObservationPlayTests.Decide(driver, true);
    }
    private static async Task DeclineObservedPlays(StandbyDefendObserverTests.Driver driver, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
            await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        }
        Assert.Null(driver.State.PendingEffectPlay);
    }
}
