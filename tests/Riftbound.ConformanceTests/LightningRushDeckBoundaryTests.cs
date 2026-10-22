using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushDeckBoundaryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheSelectedDrawCountsForAnActualSecondDrawObserverWhileDecliningDoesNot(bool draw)
    {
        var initial = LightningRushDeckDrawFlowTests.Position();
        var gem = OfficialDrawDefenseTests.Gem(1).CardObjects["G"];
        initial = initial with { DrawLedger = new(initial.TurnNumber, new Dictionary<string, int> { ["P1"] = 1 }),
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["G"] = gem,
                ["ALLY"] = new("ALLY", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", power: 2, tags: [CardObjectTags.UnitCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["G"] = new("P1", "BASE"), ["ALLY"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = ["G", "ALLY"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [])); await driver.Top();
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, draw ? ["SPELL3"] : []));
        Assert.Equal(draw ? 2 : 1, driver.State.DrawLedger.Counts["P1"]);
        Assert.Equal(draw, driver.State.PendingCardChoice?.ChoiceWindow == "TRIGGER_CONFIRMATION");
        if (draw) {
            Assert.Equal("SECOND_DRAW", Assert.Single(driver.State.StackItems).FieldContext!.Kind);
            var trigger = driver.State.PendingCardChoice!;
            await driver.Act("P1", new ChooseCardsCommand(trigger.ChoiceId, trigger.ChoiceWindow, ["ALLY"])); await driver.Top();
        }
        Assert.Empty(driver.State.StackItems);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    public async Task ShortOrEmptyDeckUsesOnlyAvailableCardsWithoutBurnoutOrShuffle(int count, bool choose)
    {
        var initial = LightningRushDeckDrawFlowTests.Position();
        var ids = initial.PlayerZones["P1"].MainDeck.Take(count).ToArray();
        var removed = initial.PlayerZones["P1"].MainDeck.Except(ids).ToHashSet();
        initial = initial with { CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = ids } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [])); await driver.Top();
        if (count > 0) {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal(ids, choice.ContextObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, choose ? [ids[^1]] : []));
        }
        Assert.Empty(driver.State.PlayerZones["P1"].MainDeck); Assert.Empty(driver.State.StackItems);
        Assert.Equal(count > 0 && choose ? new[] { ids[^1] } : [], driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(ids.Where(id => !choose || id != ids[^1]).Append("RUSH").Order(), driver.State.PlayerZones["P1"].Graveyard.Order());
        Assert.Equal(initial.RngCursor, driver.State.RngCursor);
        Assert.Equal(initial.PlayerScores, driver.State.PlayerScores);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind.Contains("BURN", StringComparison.Ordinal) || e.Event.Kind == "CARDS_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("UNIT4")]
    [InlineData("SPELL3")]
    [InlineData("SPELL4")]
    public async Task AnyLookedCardTypeMayBeDrawnButNeverBecomesACastTargetOrPubliclyRevealed(string selected)
    {
        var initial = LightningRushDeckDrawFlowTests.Position();
        initial = DeckPlaySequenceTests.TopCard(initial, "SPELL4", "SFD·042/221");
        var rejected = await new CoreRuleEngine().ResolveAsync(initial, new("not-a-casting-target", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [selected]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [])); await driver.Top();
        var choice = driver.State.PendingCardChoice!;
        Assert.Contains(selected, choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [selected]));
        Assert.Equal(new[] { selected }, driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"" + selected + "\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "CARD_REVEALED" or "UNIT_PLAYED_TO_BASE" or "EQUIPMENT_PLAYED_TO_BASE");
        Assert.Equal(1, driver.State.CardObjects[selected].ObjectGeneration);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("outside-three")]
    [InlineData("two")]
    [InlineData("duplicate")]
    [InlineData("opponent")]
    [InlineData("stale-choice")]
    public async Task IllegalChoiceRejectsWithoutLosingTheOriginalPrivateChoiceAndItStillCompletes(string branch)
    {
        var initial = LightningRushDeckDrawFlowTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [])); await driver.Top();
        var state = driver.State; var choice = state.PendingCardChoice!;
        var selected = branch switch { "outside-three" => new[] { "TAIL" }, "two" => ["UNIT4", "SPELL3"], "duplicate" => ["SPELL3", "SPELL3"], _ => ["SPELL3"] };
        var command = new ChooseCardsCommand(branch == "stale-choice" ? "EXPIRED" : choice.ChoiceId, choice.ChoiceWindow, selected);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-lightning-choice", branch == "opponent" ? "P2" : "P1", CommandTypes.ChooseCards), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL3"]));
        Assert.Equal(new[] { "SPELL3" }, driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "RUSH", "UNIT4", "SPELL4" }.Order(), driver.State.PlayerZones["P1"].Graveyard.Order());
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NocturnesObservedBanishFinishesBeforeChoosingAndItsPlayWaitsUntilTheParentDrawAndGraveInstructionsFinish(bool banish)
    {
        var initial = DeckPlaySequenceTests.TopCard(LightningRushDeckDrawFlowTests.Position(), "UNIT4", DeckObservationPlayTests.Nocturne);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, [])); await driver.Top();
        Assert.Equal("DECK_OBSERVATION_BANISH", driver.State.PendingRuleChoice!.Request.Kind);
        Assert.DoesNotContain("UNIT4", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        await DeckObservationPlayTests.Decide(driver, banish);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(banish ? new[] { "SPELL3", "SPELL4" } : ["UNIT4", "SPELL3", "SPELL4"], choice.ContextObjectIds);
        Assert.Null(driver.State.PendingEffectPlay);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SPELL3"]));
        Assert.Contains("RUSH", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(new[] { "SPELL5", "TAIL" }, driver.State.PlayerZones["P1"].MainDeck);
        if (banish) {
            Assert.NotNull(driver.State.PendingEffectPlay); Assert.DoesNotContain("UNIT4", driver.State.PlayerZones["P1"].Graveyard);
            await driver.Act("P1", new PlayCardCommand("UNIT4", DeckObservationPlayTests.Nocturne, []));
            Assert.Contains("UNIT4", driver.State.PlayerZones["P1"].Base);
        } else Assert.Contains("UNIT4", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARDS_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }
}
