using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// The move consumers are admitted printings; Gravity Field is a staged public
// fee source until its remaining shared Last Breath dependency is complete.
public sealed class LegacyEffectMovementPaymentTests
{
    [Theory]
    [InlineData(true, false)] [InlineData(false, false)]
    [InlineData(true, true)] [InlineData(false, true)]
    public async Task StunningDisplayKeepsItsBoonWhenMovementIsDeclinedAndRetainsAcquiredResources(bool pay, bool acquire)
    {
        var initial = DisplayPosition(acquire);
        var game = new Driver(initial);
        await game.Act("P1", Display()); await game.Top();
        Fee(game);
        Assert.Contains(CardObjectTags.Boon, game.State.PendingRuleChoice!.Request.Preview!.CardObjects["ALLY"].Tags);
        if (acquire) await Gain(game);
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Contains(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal(initial.CardObjects["ALLY"].Power + 1, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(pay ? "BF" : null, game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(initial.CardObjects["ALLY"].ObjectGeneration, game.State.CardObjects["ALLY"].ObjectGeneration);
        await game.Replay();
    }

    [Theory]
    [InlineData("")] [InlineData("BASE:P1")] [InlineData("BASE:P2")] [InlineData("BATTLEFIELD:MISSING")]
    public async Task StunningDisplayRequiresAnActualBattlefieldBeforePayingOrGrantingBoon(string destination)
    {
        var initial = DisplayPosition(false);
        await RadGearTestPosition.Reject(initial, "P1", Display() with { Destination = destination });
    }

    [Fact]
    public async Task ARealReturnResponsePreventsBothTheBoonAndMovementWithoutChargingAMovementFee()
    {
        var initial = DisplayPosition(false);
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        initial = WithPool(initial, new(20, 20));
        var game = new Driver(initial);
        await game.Act("P1", Display());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"]));
        await game.Top(); await game.Top();
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal("HAND", game.State.ObjectLocations["ALLY"].Zone);
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "IMPOSED_MOVEMENT_COST"));
        await game.Replay();
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, false)]
    [InlineData(true, true)] [InlineData(false, true)]
    public async Task YasuoChoosesTheBattlefieldAndUsesTheSharedPaymentContinuation(bool pay, bool acquire)
    {
        var initial = OfficialHoldSequenceTests.AddLegend(Position(), "OGN·259/298");
        initial = WithPool(initial, new(acquire ? 2 : 3, acquire ? 0 : 1));
        if (acquire) initial = RadGearTestPosition.Put(initial, "RUNE", "OGN·007/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.YasuoLegendAbilityId, ["ALLY"], ["SPEND_MANA:2"]));
        Assert.Equal("MOVEMENT_DESTINATION", game.State.PendingRuleChoice!.Request.Kind);
        Assert.All(game.State.PendingRuleChoice.Request.Options, option => Assert.StartsWith("BATTLEFIELD:", option.Id));
        await Decide(game, "BATTLEFIELD:BF");
        Fee(game);
        if (acquire) await Gain(game);
        await Decide(game, pay ? "PAY" : "DECLINE");
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(pay ? "BATTLEFIELD" : "BASE", game.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal(pay ? "BF" : null, game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.Equal(pay ? 1 : 0, game.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BothLegendReturnInstructionsMoveAttachedEquipmentWithoutAnEntryFee(bool khazix)
    {
        var initial = OfficialHoldSequenceTests.AddLegend(Position(), khazix ? "UNL-201/219" : "OGN·259/298");
        initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = RadUnitEffectTestFixture.Place(initial, "GEAR", "P2", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { IsExhausted = true } },
            PlayerExperience = new Dictionary<string, int> { ["P1"] = 2, ["P2"] = 0 } };
        initial = WithPool(initial, new(khazix ? 0 : 2, 0));
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND",
            khazix ? LegendActionAbilityCatalog.KhazixLegendMoveAbilityId : LegendActionAbilityCatalog.YasuoLegendAbilityId,
            ["ALLY"], [khazix ? "SPEND_EXPERIENCE:2" : "SPEND_MANA:2"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal("BASE", game.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal("P2", game.State.ObjectLocations["GEAR"].PlayerId);
        Assert.Null(game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "MOVEMENT_COST_DECLINED");
        await game.Replay();
    }

    private static MatchState Position()
    {
        var state = RadGearTestPosition.Create();
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TAX"] = new("TAX", cardNo: "RAD·062/167", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy]),
            ["GEAR"] = new("GEAR", cardNo: "SFD·022/221", ownerId: "P2", controllerId: "P2",
                tags: [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Weapon], attachedToObjectId: "ALLY") } };
        state = RadUnitEffectTestFixture.Place(state, "TAX", "P2", "BATTLEFIELD", "BF");
        return RadUnitEffectTestFixture.Place(state, "GEAR", "P2", "BASE");
    }

    private static MatchState DisplayPosition(bool acquire)
    {
        var state = RadGearTestPosition.Put(Position(), "SPELL", "OGN·270/298", "P1", "HAND");
        state = WithPool(state, new(acquire ? 1 : 2, acquire ? 0 : 1,
            new Dictionary<string, int> { [RuneTrait.Orange] = 1 }));
        return acquire ? RadGearTestPosition.Put(state, "RUNE", "OGN·007/298", "P1", "BASE") : state;
    }

    private static MatchState WithPool(MatchState state, RunePool pool)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };

    private static PlayCardCommand Display() => new("SPELL", "OGN·270/298", ["ALLY"], Destination: "BATTLEFIELD:BF");

    private static void Fee(Driver game)
    {
        var request = game.State.PendingRuleChoice!.Request;
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", request.Kind);
        Assert.Equal(1, request.ManaCost); Assert.Equal(1, request.PowerCost);
    }

    private static async Task Gain(Driver game)
    {
        await Decide(game, "TAP_RUNE:RUNE"); await Decide(game, "RECYCLE_RUNE:RUNE");
    }

    private static Task Decide(Driver game, string option)
    {
        var request = game.State.PendingRuleChoice!.Request;
        return game.Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
    }
}
