using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class WildClawPendingReflexiveTests
{
    internal const string WildClaw = "VEN·089";

    [Theory]
    [InlineData("OGN·096/298")]
    [InlineData("SFD·042/221")]
    public async Task PrivateUnitOrGearSelectionStagesTheCardBeforeTheParentOffersOptionalReflexiveCreation(string selectedCard)
    {
        var known = DeckPlaySequenceTests.TopCard(SingleDeckPlaySequenceTests.Position(SingleDeckPlaySequenceTests.Reinforce), "P1_TOP", selectedCard);
        known = DeckPlaySequenceTests.TopCard(known, "N1", "OGN·048/298");
        var control = await new CoreRuleEngine().ResolveAsync(known, new("known-consumer-control", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("PARENT", SingleDeckPlaySequenceTests.Reinforce, []), default);
        Assert.True(control.Accepted, control.ErrorMessage);
        var initial = known with { CardObjects = new Dictionary<string, CardObjectState>(known.CardObjects) {
            ["PARENT"] = known.CardObjects["PARENT"] with { CardNo = WildClaw, ManaCost = 7 } } };
        var viewed = initial.PlayerZones["P1"].MainDeck.Take(5).ToArray();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClaw, []));
        await driver.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(viewed, choice.ContextObjectIds);
        Assert.Contains("P1_TOP", choice.LegalObjectIds);
        Assert.DoesNotContain("N1", choice.LegalObjectIds);
        Assert.Equal(0, choice.RequiredCount);
        await DeckPlaySequenceTests.Select(driver, "P1_TOP");
        var creation = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("P1", creation.Request.PlayerId);
        Assert.Equal("DECK_REFLEXIVE_CREATE", creation.Request.Kind);
        Assert.Equal(new[] { "CREATE", "DECLINE" }, creation.Request.Options.Select(o => o.Id));
        var staged = Assert.IsType<MatchState>(creation.Request.Preview);
        Assert.Contains(staged.StackItems, item => item.SourceObjectId == "PARENT");
        Assert.NotNull(Assert.Single(staged.StackItems, item => item.SourceObjectId == "P1_TOP").DeckPlayPendingCard);
        Assert.Equal("STACK", staged.ObjectLocations["P1_TOP"].Zone);
        Assert.Equal(2, staged.CardObjects["P1_TOP"].ObjectGeneration);
        Assert.False(staged.CardObjects["P1_TOP"].IsEmpowered);
        Assert.Null(staged.PendingEffectPlay);
        Assert.All(viewed.Where(id => id != "P1_TOP"), id => Assert.Contains(id, staged.PlayerZones["P1"].MainDeck));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("OGN·096/298", true)]
    [InlineData("OGN·096/298", false)]
    [InlineData("SFD·042/221", true)]
    [InlineData("SFD·042/221", false)]
    public async Task OptionalCreationIsIndependentAndEmpowersOnlyAfterTheActualPermanentPlay(string card, bool create)
    {
        var initial = Position(card); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClaw, [])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await Decide(driver, create);
        Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(create ? 2 : 1, driver.State.StackItems.Count);
        Assert.False(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.Equal("P1_TOP", Assert.Single(driver.State.PendingEffectPlay!.Sources).Key);
        if (create) Assert.False(driver.State.StackItems.Single(i => i.DeckPlayReflexive is not null).DeckPlayReflexive!.Confirmed);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        await driver.Act("P1", new PlayCardCommand("P1_TOP", card, []));
        Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(3, driver.State.CardObjects["P1_TOP"].ObjectGeneration);
        Assert.False(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        if (create) {
            var ability = Assert.Single(driver.State.StackItems, i => i.DeckPlayReflexive is not null);
            Assert.True(ability.DeckPlayReflexive!.Confirmed);
            Assert.Equal(new ObjectBinding("P1_TOP", 3), ability.DeckPlayReflexive.Played);
            Assert.Empty(ability.TargetObjectIds);
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
            await driver.Top();
        }
        Assert.Equal(create, driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.Equal(create ? 1 : 0, driver.Events().Count(e => e.Event.Kind == "OBJECT_EMPOWERED"));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static MatchState Position(string card = "OGN·096/298")
    {
        var state = DeckPlaySequenceTests.TopCard(SingleDeckPlaySequenceTests.Position(SingleDeckPlaySequenceTests.Reinforce), "P1_TOP", card);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PARENT"] = state.CardObjects["PARENT"] with { CardNo = WildClaw, ManaCost = 7 } } };
    }

    [Fact]
    public async Task EchoInterleavesPendingCardsAndReflexivesButFinishesAllConfirmationBeforeAnyEmpowerResolves()
    {
        var initial = DeckPlaySequenceTests.TopCard(Position(), "TAIL", "OGN·096/298")
            with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClaw, [], OptionalCosts: ["ECHO"])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await Decide(driver, true);
        Assert.Equal(new[] { "PARENT", "P1_TOP", "PARENT" }, driver.State.StackItems.Select(i => i.SourceObjectId));
        Assert.False(driver.State.StackItems.Last().DeckPlayReflexive!.Confirmed);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(driver.State));
        await DeckPlaySequenceTests.Select(driver, "TAIL"); await Decide(driver, true);
        Assert.Equal(new[] { "P1_TOP", "PARENT", "TAIL", "PARENT" }, driver.State.StackItems.Select(i => i.SourceObjectId));
        foreach (var id in new[] { "P1_TOP", "TAIL" }) {
            Assert.Equal(id, Assert.Single(driver.State.PendingEffectPlay!.Sources).Key);
            await driver.Act("P1", new PlayCardCommand(id, "OGN·096/298", []));
            Assert.False(driver.State.CardObjects[id].IsEmpowered);
        }
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.All(driver.State.StackItems, i => Assert.True(i.DeckPlayReflexive!.Confirmed));
        await driver.Top(); Assert.True(driver.State.CardObjects["TAIL"].IsEmpowered);
        Assert.False(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        await driver.Top(); Assert.True(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.Equal(new[] { "TAIL", "P1_TOP" }, driver.Events().Where(e => e.Event.Kind == "OBJECT_EMPOWERED").Select(e => e.Event.Payload["sourceObjectId"]));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task EmptyDeckOrDeclinedCardStillMakesOnlyTheSeparatelyChosenReflexiveWithoutBurnout(bool empty, bool create)
    {
        var initial = Position();
        if (empty) {
            var removed = initial.PlayerZones["P1"].MainDeck.ToHashSet();
            initial = initial with {
                CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(),
                ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(),
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } } };
        }
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClaw, [])); await driver.Top();
        if (!empty) {
            var choice = driver.State.PendingCardChoice!;
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        await Decide(driver, create);
        Assert.Null(driver.State.PendingEffectPlay); Assert.Contains("PARENT", driver.State.PlayerZones["P1"].Graveyard);
        if (create) {
            Assert.Null(Assert.Single(driver.State.StackItems).DeckPlayReflexive!.Selected);
            await driver.Top();
        }
        Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "OBJECT_EMPOWERED" or "BURNOUT_APPLIED" or "CARD_DRAWN" or "UNIT_PLAYED_TO_BASE");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static async Task Decide(StandbyDefendObserverTests.Driver driver, bool create)
    {
        var choice = driver.State.PendingRuleChoice!;
        await driver.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [create ? "CREATE" : "DECLINE"]));
    }
}
