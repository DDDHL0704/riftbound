using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BaitDeclaredMovementTests
{
    private const string Card = "SFD·129/221";

    [Theory]
    [InlineData("")] [InlineData("BASE:P1")] [InlineData("BASE:P2")]
    [InlineData("BATTLEFIELD:BF")] [InlineData("BATTLEFIELD:ABSENT")] [InlineData("HAND:P2")]
    public async Task MissingEmptyUnchangedAndForgedLocationsRejectAtomically(string destination)
        => await RadGearTestPosition.Reject(Position(), "P1", Play(destination));

    [Fact]
    public async Task AnExtraReferenceUnitIsNotASecondTarget()
        => await RadGearTestPosition.Reject(Position(), "P1", Play("BATTLEFIELD:OTHER") with { TargetObjectIds = ["ENEMY", "ANCHOR"] });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DestinationOccupantsAreNotTargetsAndTheirWardDoesNotIncreaseTheQuote(bool toBase)
    {
        var initial = Position();
        if (toBase) initial = RadUnitEffectTestFixture.Place(initial, "ENEMY", "P2", "BATTLEFIELD", "BF");
        initial = RadUnitEffectTestFixture.Place(initial, "ANCHOR", "P2", toBase ? "BASE" : "BATTLEFIELD", toBase ? null : "OTHER");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ANCHOR"] = initial.CardObjects["ANCHOR"] with { Tags = [CardObjectTags.UnitCard, "法盾9"], OwnerId = "P1" } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(2, 0) } };
        var game = new Driver(initial);
        await game.Act("P1", Play(toBase ? "BASE:P2" : "BATTLEFIELD:OTHER"));
        Assert.Equal(new[] { "ENEMY" }, Assert.Single(game.State.StackItems).TargetObjectIds);
        await game.Top();
        Assert.Equal(toBase ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal(toBase ? null : "OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AReferenceWithTheWrongControllerOrAnEquipmentCannotProvideTheDestination(bool equipment)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ANCHOR"] = initial.CardObjects["ANCHOR"] with {
                ControllerId = equipment ? "P2" : "P1", OwnerId = "P2",
                Tags = equipment ? [CardObjectTags.EquipmentCard] : [CardObjectTags.UnitCard] } } };
        await RadGearTestPosition.Reject(initial, "P1", Play("BATTLEFIELD:OTHER"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EachEchoExecutionKeepsItsOwnTargetAndPaysOrDeclinesItsMovement(bool pay)
    {
        var initial = Tax(Position());
        initial = RadGearTestPosition.Put(initial, "SECOND", "OGN·219/298", "P2", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", Play("BATTLEFIELD:OTHER") with { OptionalCosts = ["ECHO"],
            RepeatChoices = [new("", ["SECOND"], Destination: "BATTLEFIELD:OTHER")] });
        Assert.Equal(2, Assert.Single(game.State.StackItems).RepeatExecutions!.Count);
        await game.Top();
        Fee(game); await Decide(game, pay ? "PAY" : "DECLINE");
        Fee(game); await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "OTHER" : null, game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(pay ? "OTHER" : null, game.State.ObjectLocations["SECOND"].BattlefieldObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task AResourceActionCanFundTheImposedMixedFeeWithoutChangingTheDestination()
    {
        var initial = RadGearTestPosition.Put(Tax(Position()), "RUNE", "OGN·007/298", "P1", "BASE");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(2, 0) } };
        var game = new Driver(initial);
        await game.Act("P1", Play("BATTLEFIELD:OTHER")); await game.Top();
        Fee(game); await Decide(game, "TAP_RUNE:RUNE"); await Decide(game, "RECYCLE_RUNE:RUNE"); await Decide(game, "PAY");
        Assert.Equal("OTHER", game.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await game.Replay();
    }

    [Fact]
    public async Task AnInvalidEchoDestinationRejectsTheWholePlayBeforeAnyCost()
    {
        var initial = RadGearTestPosition.Put(Position(), "SECOND", "OGN·219/298", "P2", "BASE");
        await RadGearTestPosition.Reject(initial, "P1", Play("BATTLEFIELD:OTHER") with {
            OptionalCosts = ["ECHO"], RepeatChoices = [new("", ["SECOND"], Destination: "BATTLEFIELD:BF")] });
    }

    [Fact]
    public async Task AnActualReturnResponseCannotRedirectTheMoveToTheReferenceUnitsNewLocation()
    {
        var game = new Driver(RadGearTestPosition.Put(Tax(Position()), "RETURN", "OGN·104/298", "P2", "HAND"));
        await game.Act("P1", Play("BATTLEFIELD:OTHER"));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ANCHOR"]));
        await game.Top(); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["ANCHOR"].Zone);
        Assert.Equal("BASE", game.State.ObjectLocations["ENEMY"].Zone);
        Assert.Null(game.State.PendingRuleChoice);
        await game.Replay();
    }

    [Fact]
    public async Task ANewControllerRemakesTheUnitAndLocationUsingTheSameConstraint()
    {
        var initial = RadGearTestPosition.Put(Position(), "FRIEND_REFERENCE", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(RadGearTestPosition.Put(initial, "REBUTTAL", RebuttalRulesTests.Card, "P2", "HAND"));
        await game.Act("P1", Play("BATTLEFIELD:OTHER"));
        var original = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("REBUTTAL", RebuttalRulesTests.Card, [original]));
        await game.Top(); await DecideAsRequested(game, "PAY:ANY");
        await DecideAsRequested(game, RebuttalRulesTests.Option(game.State, "重选目标 1 为", "（ALLY）"));
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "CONFIRM");
        await DecideAsRequested(game, RebuttalRulesTests.Option(game.State, "目的地 BF"));
        await DecideAsRequested(game, "CONFIRM");
        var remade = Assert.Single(game.State.StackItems);
        Assert.Equal("P2", remade.ControllerId); Assert.Equal("BATTLEFIELD:BF", remade.Destination);
        Assert.Equal(new[] { "ALLY" }, remade.TargetObjectIds);
        await game.Top(); Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.Replay();
    }

    private static Task DecideAsRequested(Driver game, string choice)
        => game.Act(game.State.PendingRuleChoice!.Request.PlayerId,
            new PayCostCommand(game.State.PendingRuleChoice.Request.Id, "RULE_REPLACEMENT", [choice]));

    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", Card, "P1", "HAND");
        return RadGearTestPosition.Put(state, "ANCHOR", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
    }
    private static MatchState Tax(MatchState state)
    {
        // Public staged source: Gravity Field is not yet admitted.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TAX"] = new("TAX", cardNo: "RAD·062/167", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy]) } };
        return RadUnitEffectTestFixture.Place(state, "TAX", "P2", "BATTLEFIELD", "OTHER");
    }
    private static PlayCardCommand Play(string destination) => new("SPELL", Card, ["ENEMY"], Destination: destination);
    private static void Fee(Driver game)
    {
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", request.Kind);
        Assert.Equal("P1", request.PlayerId); Assert.Equal(1, request.ManaCost); Assert.Equal(1, request.PowerCost);
    }
    private static Task Decide(Driver game, string choice)
        => game.Act("P1", new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [choice]));
}
