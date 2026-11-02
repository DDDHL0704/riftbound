using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadChronobreakTests
{
    private const string Card = "RAD·140/167";
    private const string Unit = "OGN·219/298";
    private static MatchState Position() => VenUnitEntryBasicTests.Position(Card);
    private static PlayCardCommand Cast(int amount, bool echo = false) => new("UNIT", Card, [],
        OptionalCosts: echo ? ["ADDITIONAL_POWER:" + amount, "ECHO"] : ["ADDITIONAL_POWER:" + amount]);
    private static async Task Choose(Driver g, string id)
    {
        var choice = Assert.IsType<RuleChoiceContinuation>(g.State.PendingRuleChoice);
        await g.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [id]));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task DeclarationPaysTwoManaAndTheExactPowerEvenWhenFewerUnitsRemain(int amount)
    {
        var g = new Driver(Position());
        var quote = new CoreRuleEngine().PreviewPlayCard(g.State, "P1", PlayCostPreviewTests.Request(g.State, Cast(amount)));
        Assert.True(quote.CanPay); Assert.Equal(2, quote.Cost!.Mana);
        await g.Act("P1", Cast(amount));
        Assert.Equal(18, g.State.RunePools["P1"].Mana);
        Assert.Equal(20 - amount, g.State.RunePools["P1"].TotalPower);
        await g.Top();
        Assert.Contains("UNIT", g.State.PlayerZones["P1"].Graveyard);
        if (amount == 0) { Assert.Null(g.State.PendingEffectPlay); Assert.Equal(0, g.State.CardObjects["ALLY"].ObjectGeneration); }
        else
        {
            Assert.Equal("ALLY", Assert.Single(g.State.StackItems).SourceObjectId);
            await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
            Assert.Contains("ALLY", g.State.PlayerZones["P1"].Base);
        }
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task BorrowedUnitIsActuallyPlayedByItsOwnerToTheOriginalFieldOrThatOwnersBase(string destination)
    {
        var initial = LocalDestructionRecallTests.At(Position(), "ENEMY", "P1", "BATTLEFIELD", "BF");
        var g = new Driver(initial);
        await g.Act("P1", Cast(1)); await g.Top();
        await Choose(g, "ENEMY");
        Assert.Equal("P2", g.State.PendingEffectPlay!.PlayerId);
        Assert.Equal("P2", Assert.Single(g.State.StackItems).ControllerId);
        Assert.Equal("STACK", g.State.ObjectLocations["ENEMY"].Zone);
        var resources = g.State.RunePools["P2"];
        await g.Act("P2", new PlayCardCommand("ENEMY", Unit, [], Destination: destination));
        Assert.Equal(resources, g.State.RunePools["P2"]);
        Assert.Equal("P2", g.State.CardObjects["ENEMY"].OwnerId);
        Assert.Equal("P2", g.State.CardObjects["ENEMY"].ControllerId);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", g.State.ObjectLocations["ENEMY"].Zone);
    }

    [Fact]
    public async Task OwnerChoosesOrderAllCardsEnterTheChainBeforeFirstConfirmationAndResponsesStayClosed()
    {
        var initial = LastBreathDrawBoundaryTests.Add(Position(), "SECOND", Unit, "P1", "BASE");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P1", "BASE");
        var g = new Driver(initial);
        await g.Act("P1", Cast(3)); await g.Top();
        Assert.Equal("P1", g.State.PendingRuleChoice!.Request.PlayerId);
        await Choose(g, "SECOND");
        Assert.Equal(["SECOND", "ALLY", "ENEMY"], g.State.StackItems.Select(i => i.SourceObjectId));
        Assert.All(g.State.StackItems, i => Assert.NotNull(i.DeckPlayPendingCard));
        Assert.Contains("UNIT", g.State.PlayerZones["P1"].Graveyard);
        await g.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        Assert.Contains("SECOND", g.State.PlayerZones["P1"].Base);
        Assert.Equal(["ALLY", "ENEMY"], g.State.StackItems.Select(i => i.SourceObjectId));
        var rejected = await new CoreRuleEngine().ResolveAsync(g.State, new("early", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(g.State), MatchStateHasher.Hash(rejected.State));
        await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        await g.Act("P2", new PlayCardCommand("ENEMY", Unit, []));
        Assert.Null(g.State.PendingEffectPlay);
    }

    [Fact]
    public async Task RealEchoSelectsRemainingFieldUnitsBeforeAnyPendingCardCanBePlayed()
    {
        var initial = LastBreathDrawBoundaryTests.Add(Position(), "SECOND", Unit, "P1", "BASE")
            with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var g = new Driver(initial);
        await g.Act("P1", Cast(1, echo: true)); await g.Top(); await Choose(g, "SECOND");
        Assert.Equal(["SECOND", "ALLY"], g.State.StackItems.Select(i => i.SourceObjectId));
        Assert.Equal([0, 1], g.State.PendingEffectPlay!.Parent.DeckPlaySequence!.Queue.Select(q => q.ExecutionIndex));
        await g.Act("P1", new PlayCardCommand("SECOND", Unit, []));
        await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
    }

    [Fact]
    public async Task ATokenCeasesToExistAfterBanishmentAndNeverBecomesAPlayPermission()
    {
        var initial = Position();
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("OGN·273/298", out var factory));
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ALLY"] = factory.CreateObject("ALLY", "P1", "P1") } };
        var g = new Driver(initial);
        await g.Act("P1", Cast(1)); await g.Top();
        Assert.False(g.State.CardObjects.ContainsKey("ALLY")); Assert.Null(g.State.PendingEffectPlay);
        Assert.Empty(g.State.StackItems); Assert.DoesNotContain("ALLY", g.State.PlayerZones["P1"].Banished);
        Assert.Single(g.Events(), e => e.Event.Kind == "UNIT_BANISHED");
    }

    [Fact]
    public async Task RealSpellControlChangeStillGroupsOwnersStartingWithTheTurnPlayer()
    {
        var initial = LastBreathDrawBoundaryTests.Add(Position(), "CAST", Card, "P2", "HAND");
        initial = AttackDamageDistributionTests.Spell(initial, "REFLECTION", "OGN·080/298", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P1", "BASE") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        var g = new Driver(initial);
        await g.Act("P2", new PlayCardCommand("CAST", Card, [], OptionalCosts: ["ADDITIONAL_POWER:2"]));
        await g.Act("P2", new PassPriorityCommand());
        await g.Act("P1", new PlayCardCommand("REFLECTION", "OGN·080/298", [Assert.Single(g.State.StackItems).StackItemId]));
        await g.Top(); await g.Top();
        Assert.Equal(["ENEMY", "ALLY"], g.State.StackItems.Select(i => i.SourceObjectId));
        Assert.Equal("P2", g.State.PendingEffectPlay!.PlayerId);
        await g.Act("P2", new PlayCardCommand("ENEMY", Unit, []));
        await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
    }

    [Fact]
    public async Task PrintedPlayTriggersWaitUntilEveryPendingUnitHasBeenConfirmed()
    {
        var initial = LastBreathDrawBoundaryTests.Add(Position(), "DRAW", "VEN·048", "P1", "BASE");
        var g = new Driver(initial);
        await g.Act("P1", Cast(2)); await g.Top(); await Choose(g, "DRAW");
        await g.Act("P1", new PlayCardCommand("DRAW", "VEN·048", []));
        Assert.Null(g.State.PendingCardChoice); Assert.Equal("ALLY", Assert.Single(g.State.PendingEffectPlay!.Sources).Key);
        Assert.DoesNotContain(g.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
        Assert.Null(g.State.PendingEffectPlay); await g.Top();
        Assert.Single(g.Events(), e => e.Event.Kind == "CARD_DRAWN");
    }

    [Theory]
    [InlineData("ENEMY")] [InlineData("GEAR")] [InlineData("GRAVE")] [InlineData("UNIT")]
    public async Task AnIllegalSelectionLeavesTheOriginalChoiceAndPaidSpellUnchanged(string target)
    {
        var g = new Driver(LastBreathDrawBoundaryTests.Add(Position(), "SECOND", Unit, "P1", "BASE"));
        await g.Act("P1", Cast(1)); await g.Top();
        var p = g.State.PendingRuleChoice!;
        var rejected = await new CoreRuleEngine().ResolveAsync(g.State, new("bad-selection", "P1", CommandTypes.PayCost),
            new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", [target]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(g.State), MatchStateHasher.Hash(rejected.State));
        await Choose(g, "SECOND"); await g.Act("P1", new PlayCardCommand("SECOND", Unit, []));
    }

    [Theory]
    [InlineData("destination")] [InlineData("extra-cost")]
    public async Task IllegalReplayIntentCannotSpendResourcesOrConsumeThePendingCard(string kind)
    {
        var g = new Driver(Position()); await g.Act("P1", Cast(1)); await g.Top();
        var play = new PlayCardCommand("ALLY", Unit, [], Destination: kind == "destination" ? "BATTLEFIELD:BF" : "BASE",
            OptionalCosts: kind == "extra-cost" ? ["ADDITIONAL_POWER:1"] : []);
        var rejected = await new CoreRuleEngine().ResolveAsync(g.State, new("bad-replay", "P1", CommandTypes.PlayCard), play, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(g.State), MatchStateHasher.Hash(rejected.State));
        await g.Act("P1", new PlayCardCommand("ALLY", Unit, []));
    }

    [Theory]
    [InlineData("field-generation")] [InlineData("location")] [InlineData("round")]
    [InlineData("fee")] [InlineData("owner")] [InlineData("frame")]
    public async Task RecoveryRejectsForgedReplayFactsAndDestinations(string mutation)
    {
        var g = new Driver(LocalDestructionRecallTests.At(Position(), "ALLY", "P1", "BATTLEFIELD", "BF"));
        await g.Act("P1", Cast(1)); await g.Top();
        var p = g.State.PendingEffectPlay!; var c = p.Parent.DeckPlaySequence!; var r = c.OwnedReplay!;
        var f = r.Units.Single();
        var changed = mutation switch {
            "field-generation" => f with { Battlefield = new("BF", 999) },
            "location" => f with { Location = new("P1", "BASE") },
            "round" => f with { ExecutionIndex = 99 },
            "owner" => f with { OwnerId = "P2" }, _ => f };
        var bad = p with { Parent = p.Parent with {
            OptionalCosts = mutation == "fee" ? ["ADDITIONAL_POWER:0"] : p.Parent.OptionalCosts,
            DeckPlaySequence = c with { OwnedReplay = r with {
                Units = [changed], Frames = mutation == "frame" ? [] : r.Frames } } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(g.State with { PendingEffectPlay = bad }), e => e.Contains("deck"));
    }
}
