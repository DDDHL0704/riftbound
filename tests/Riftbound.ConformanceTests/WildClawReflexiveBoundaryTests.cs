using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class WildClawReflexiveBoundaryTests
{
    [Theory]
    [InlineData("OGN·104/298", false)]
    [InlineData("UNL-184/219", true)]
    public async Task ActualRecallOrBanishReentryCannotMakeTheOldAbilityEmpowerANewObject(string response, bool reenter)
    {
        var initial = AttackDamageDistributionTests.Spell(WildClawPendingReflexiveTests.Position(), "RESPONSE", response, "P1");
        var driver = await Open(initial);
        var binding = Assert.Single(driver.State.StackItems).DeckPlayReflexive!.Played;
        Assert.Equal(new ObjectBinding("P1_TOP", 3), binding);
        await driver.Act("P1", new PlayCardCommand("RESPONSE", response, ["P1_TOP"])); await driver.Top();
        if (reenter) {
            Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", [], Destination: "BATTLEFIELD:F"));
            Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Battlefields);
        }
        else Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["P1_TOP"].ObjectGeneration > binding!.Generation);
        Assert.Equal(binding, Assert.Single(driver.State.StackItems).DeckPlayReflexive!.Played);
        await driver.Top(); Assert.False(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task InsufficientPrintedPowerReturnsTheRealPendingCardToBanishAndLeavesTheIndependentAbilityWithoutAnEffect()
    {
        const string card = "UNL-137/219";
        var initial = WildClawPendingReflexiveTests.Position(card);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(7, 0, new Dictionary<string, int> { ["orange"] = 1 }) } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await WildClawPendingReflexiveTests.Decide(driver, true);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        var before = MatchStateHasher.Hash(driver.State);
        var play = new PlayCardCommand("P1_TOP", card, []);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, play)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("unpayable", "P1", CommandTypes.PlayCard), play, default);
        Assert.False(rejected.Accepted); Assert.Equal(before, MatchStateHasher.Hash(rejected.State)); Assert.Empty(rejected.Events);
        var pending = driver.State.PendingEffectPlay!;
        await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Banished);
        Assert.Null(Assert.Single(driver.State.StackItems).DeckPlayReflexive!.Played);
        await driver.Top(); Assert.False(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SameExecutionObservationIsConfirmedFirstAndNeverBecomesTheNormalSelectionsReflexiveReference(bool playObserved)
    {
        var initial = DeckPlaySequenceTests.TopCard(WildClawPendingReflexiveTests.Position(DeckObservationPlayTests.Nocturne), "N1", "OGN·096/298");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [])); await driver.Top();
        Assert.Equal("DECK_OBSERVATION_BANISH", driver.State.PendingRuleChoice!.Request.Kind);
        await DeckObservationPlayTests.Decide(driver, true);
        await DeckPlaySequenceTests.Select(driver, "N1"); await WildClawPendingReflexiveTests.Decide(driver, true);
        Assert.Equal(new[] { "P1_TOP", "N1", "PARENT" }, driver.State.StackItems.Select(s => s.SourceObjectId));
        Assert.NotNull(driver.State.PendingEffectPlay!.Parent.ObservedPlayPermission);
        await FinishObserved(driver, "P1_TOP", playObserved);
        Assert.Equal("N1", Assert.Single(driver.State.PendingEffectPlay!.Sources).Key);
        await driver.Act("P1", new PlayCardCommand("N1", "OGN·096/298", []));
        Assert.Equal("N1", Assert.Single(driver.State.StackItems).DeckPlayReflexive!.Played!.ObjectId);
        await driver.Top(); Assert.True(driver.State.CardObjects["N1"].IsEmpowered);
        Assert.False(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LaterObservationCannotOvertakeAnEarlierReflexiveAndAnEmptySecondSelectionDoesNotEmpowerIt(bool playObserved)
    {
        var initial = DeckPlaySequenceTests.TopCard(WildClawPendingReflexiveTests.Position(), "TAIL", DeckObservationPlayTests.Nocturne)
            with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [], OptionalCosts: ["ECHO"])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await WildClawPendingReflexiveTests.Decide(driver, true);
        Assert.Equal("DECK_OBSERVATION_BANISH", driver.State.PendingRuleChoice!.Request.Kind);
        await DeckObservationPlayTests.Decide(driver, true);
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await WildClawPendingReflexiveTests.Decide(driver, true);
        Assert.Equal(new[] { "P1_TOP", "PARENT", "TAIL", "PARENT" }, driver.State.StackItems.Select(s => s.SourceObjectId));
        Assert.Null(driver.State.PendingEffectPlay!.Parent.ObservedPlayPermission);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", "OGN·096/298", []));
        Assert.True(driver.State.StackItems.First().DeckPlayReflexive!.Confirmed);
        Assert.NotNull(driver.State.PendingEffectPlay!.Parent.ObservedPlayPermission);
        await FinishObserved(driver, "TAIL", playObserved);
        Assert.All(driver.State.StackItems, s => Assert.True(s.DeckPlayReflexive!.Confirmed));
        await driver.Top(); Assert.False(driver.State.CardObjects["TAIL"].IsEmpowered);
        await driver.Top(); Assert.True(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.Single(driver.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·037/221")]
    [InlineData("SFD·042/221")]
    public async Task NonTargetedEmpowerDoesNotAskForWardOrAdditionalResources(string card)
    {
        var initial = WildClawPendingReflexiveTests.Position(card); var driver = await Open(initial);
        var pool = driver.State.RunePools["P1"];
        Assert.Empty(Assert.Single(driver.State.StackItems).TargetObjectIds);
        await driver.Top(); Assert.True(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        Assert.Equal(pool, driver.State.RunePools["P1"]);
        Assert.DoesNotContain(driver.Events().TakeLast(3), e => e.Event.Kind == "COST_PAID");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("OGN·064/298")]
    [InlineData("SFD·045/221")]
    [InlineData("UNL-106/219")]
    public async Task ASpellOnlyCounterOrCounterRequiringAChosenTargetCannotUseTheAbilitiesUntargetedReference(string counter)
    {
        var initial = AttackDamageDistributionTests.Spell(WildClawPendingReflexiveTests.Position(), "RESPONSE", counter, "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 20) } };
        var driver = await Open(initial); var id = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        var targets = counter == "UNL-106/219" ? new[] { "P1_TOP", id } : new[] { id };
        var play = new PlayCardCommand("RESPONSE", counter, targets);
        var before = MatchStateHasher.Hash(driver.State);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(driver.State, "P2", PlayCostPreviewTests.Request(driver.State, play)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("illegal-counter", "P2", CommandTypes.PlayCard), play, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await driver.Act("P2", new PassPriorityCommand()); Assert.True(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task FinishObserved(StandbyDefendObserverTests.Driver driver, string id, bool play)
    {
        var pending = driver.State.PendingEffectPlay!;
        if (play) await driver.Act("P1", new PlayCardCommand(id, DeckObservationPlayTests.Nocturne, []));
        else await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
    }

    [Fact]
    public async Task FiveManaReductionDoesNotWaiveTheSelectedUnitsRealMandatoryEquipmentReturnCost()
    {
        const string unit = "SFD·044/221";
        var initial = WildClawPendingReflexiveTests.Position(unit);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["COST_GEAR"] = new("COST_GEAR", cardNo: "SFD·042/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["COST_GEAR"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Append("COST_GEAR").ToArray() } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await WildClawPendingReflexiveTests.Decide(driver, true);
        var command = new PlayCardCommand("P1_TOP", unit, []); var before = MatchStateHasher.Hash(driver.State);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command)).CanPay);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State, new("missing-real-cost", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await driver.Act("P1", command with { OptionalCosts = ["RETURN_FRIENDLY_EQUIPMENT:COST_GEAR"] });
        Assert.Contains("COST_GEAR", driver.State.PlayerZones["P1"].Hand); Assert.Contains("P1_TOP", driver.State.PlayerZones["P1"].Base);
        await driver.Top(); Assert.True(driver.State.CardObjects["P1_TOP"].IsEmpowered);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static async Task<StandbyDefendObserverTests.Driver> Open(MatchState initial)
    {
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("PARENT", WildClawPendingReflexiveTests.WildClaw, [])); await driver.Top();
        await DeckPlaySequenceTests.Select(driver, "P1_TOP"); await WildClawPendingReflexiveTests.Decide(driver, true);
        await driver.Act("P1", new PlayCardCommand("P1_TOP", initial.CardObjects["P1_TOP"].CardNo!, []));
        return driver;
    }
}
