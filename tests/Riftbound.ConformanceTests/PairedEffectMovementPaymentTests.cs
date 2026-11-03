using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Gravity Field is a staged public fee source, not an admitted printing.
public sealed class PairedEffectMovementPaymentTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task DifferentBattlefieldsAreLegalAndEachMovementPaysIndependently(bool first, bool second)
    {
        var initial = Tax(Tax(Position(), "TAX1", "BF"), "TAX2", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", Play()); await game.Top();
        Fee(game); await Decide(game, first ? "PAY" : "DECLINE");
        Fee(game); await Decide(game, second ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(first ? "OTHER" : "BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(second ? "BF" : "OTHER", game.State.ObjectLocations["SECOND"].BattlefieldObjectId);
        Assert.Equal(second ? "BF" : "OTHER", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        var paid = (first ? 1 : 0) + (second ? 1 : 0);
        Assert.Equal(8 - paid, game.State.RunePools["P1"].Mana);
        Assert.Equal(2 - paid, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(paid, game.Events().Count(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "IMPOSED_MOVEMENT_COST")));
        Assert.Equal(paid, game.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        Assert.Equal(first && second ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_LOCATIONS_SWAPPED"));
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task DecliningTheFirstMoveKeepsAcquiredResourcesAndDoesNotCancelTheReturn(bool pay, bool acquire)
    {
        var initial = RadUnitEffectTestFixture.Place(Tax(Position(), "TAX", "OTHER"), "ALLY", "P1", "BASE");
        initial = Pool(initial, new(acquire ? 2 : 3, acquire ? 0 : 1));
        if (acquire) initial = RadGearTestPosition.Put(initial, "RUNE", "OGN·007/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", Play()); await game.Top(); Fee(game);
        if (acquire) { await Decide(game, "TAP_RUNE:RUNE"); await Decide(game, "RECYCLE_RUNE:RUNE"); }
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal("BASE", game.State.ObjectLocations["SECOND"].Zone);
        Assert.Equal("BASE", game.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal("P2", game.State.ObjectLocations["GEAR"].PlayerId);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("P1-D0", game.State.PlayerZones["P1"].Hand);
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD")]
    public async Task TheSamePrecisePositionIsRejectedBeforePayment(string zone)
    {
        var initial = Position();
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", zone, zone == "BATTLEFIELD" ? "OTHER" : null);
        initial = RadUnitEffectTestFixture.Place(initial, "SECOND", "P1", zone, zone == "BATTLEFIELD" ? "OTHER" : null);
        await RadGearTestPosition.Reject(initial, "P1", Play());
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("SECOND")]
    public async Task AnActualReturnResponsePreventsThePairWithoutAFalseMovementFee(string returned)
    {
        var initial = RadGearTestPosition.Put(Tax(Tax(Position(), "TAX1", "BF"), "TAX2", "OTHER"),
            "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", Play());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top(); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations[returned].Zone);
        Assert.Null(game.State.PendingRuleChoice);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "UNIT_LOCATIONS_SWAPPED");
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "IMPOSED_MOVEMENT_COST"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TideCallerUsesTheSameMovementPaymentAfterItsActualPlay(bool pay)
    {
        var initial = RadGearTestPosition.Put(Tax(Position(), "TAX", "BF"), "TIDE", "OGN·199/298", "P1", "HAND");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("TIDE", "OGN·199/298", ["ALLY"], Destination: "BASE"));
        await game.Top(); Fee(game); await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["TIDE"].Zone);
        Assert.Equal(pay ? "BF" : null, game.State.ObjectLocations["TIDE"].BattlefieldObjectId);
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(pay ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_LOCATIONS_SWAPPED"));
        await game.Replay();
    }

    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", "UNL-083/219", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "SECOND", "OGN·219/298", "P1", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { Tags = state.CardObjects["ALLY"].Tags.Append(CardObjectTags.Ephemeral).ToArray() },
            ["GEAR"] = new("GEAR", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: "SECOND") } };
        return Pool(RadUnitEffectTestFixture.Place(state, "GEAR", "P2", "BATTLEFIELD", "OTHER"), new(10, 2));
    }

    private static MatchState Tax(MatchState state, string id, string field)
    {
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = new(id, cardNo: "RAD·062/167", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.EquipmentCard, "部署"]) } };
        return RadUnitEffectTestFixture.Place(state, id, "P2", "BATTLEFIELD", field);
    }
    private static MatchState Pool(MatchState s, RunePool p)
        => s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = p } };
    private static PlayCardCommand Play() => new("SPELL", "UNL-083/219", ["ALLY", "SECOND"]);
    private static void Fee(Driver game)
    {
        var r = game.State.PendingRuleChoice!.Request;
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", r.Kind); Assert.Equal("P1", r.PlayerId);
        Assert.Equal(1, r.ManaCost); Assert.Equal(1, r.PowerCost);
    }
    private static Task Decide(Driver game, string value)
        => game.Act("P1", new PayCostCommand(game.State.PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", [value]));
}
