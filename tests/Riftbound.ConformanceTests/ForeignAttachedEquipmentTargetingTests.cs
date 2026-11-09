using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ForeignAttachedEquipmentTargetingTests
{
    private static MatchState Position(string zone = "BASE")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "BREAK", "RAD·101/167", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", zone, zone == "BASE" ? null : "BF");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", "P2", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P1", zone, zone == "BASE" ? null : "BF");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { ControllerId = "P2", AttachedToObjectId = "ALLY" } } };
    }

    [Theory, InlineData("BASE"), InlineData("BATTLEFIELD")]
    public async Task ARealEquipmentSpellCanTargetAnOpponentsArmamentAttachedToOurUnit(string zone)
    {
        var game = new Driver(Position(zone));
        await game.Act("P1", new PlayCardCommand("BREAK", "RAD·101/167", ["GEAR"])); await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Graveyard);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task EquipmentAttachedInAnotherControllersBaseCountsForOurPlayCostsAndStaticPower()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "ALLY", "P2", "BASE");
        state = RadUnitEffectTestFixture.Place(state, "GEAR", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { ControllerId = "P2" },
            ["GEAR"] = state.CardObjects["GEAR"] with { ControllerId = "P1" } } };
        state = RadGearTestPosition.Put(state, "ORNN", "SFD·085/221", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GUARD", "VEN·064", "P1", "HAND");
        var command = new PlayCardCommand("GUARD", "VEN·064", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(9, quote.Cost!.Mana);

        var game = new Driver(state);
        var mana = state.RunePools["P1"].Mana;
        await game.Act("P1", command);
        Assert.Equal(mana - 9, game.State.RunePools["P1"].Mana);
        Assert.Equal(5, game.State.CardObjects["ORNN"].Power);
        Assert.Equal("P1", game.State.CardObjects["GEAR"].ControllerId);
        Assert.Equal("P2", game.State.CardObjects["GEAR"].OwnerId);
        Assert.Equal("ALLY", game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base);
        var aura = Assert.Single(game.State.ContinuousEffects, effect =>
            effect.SourceObjectId == "ORNN" && effect.EffectKind == "FRIENDLY_FIELD_EQUIPMENT_COUNT_TO_SOURCE_UNIT_POWER");
        Assert.Equal(new[] { "GEAR" }, aura.ParticipantObjectIds);
        await game.ColdCheckpoint();
    }

    [Theory]
    [InlineData("loose")] [InlineData("missing-host")] [InlineData("different-zone")]
    [InlineData("different-battlefield")] [InlineData("hidden-host")] [InlineData("non-unit-host")]
    [InlineData("unit-gear")] [InlineData("unknown-controller")]
    public async Task AForeignLocationNeedsARealLegalAttachmentRatherThanOnlyAnAttachmentId(string mutation)
    {
        var state = Position(mutation == "different-battlefield" ? "BATTLEFIELD" : "BASE");
        var gear = state.CardObjects["GEAR"]; var host = state.CardObjects["ALLY"];
        switch (mutation)
        {
            case "loose": gear = gear with { AttachedToObjectId = null }; break;
            case "missing-host": gear = gear with { AttachedToObjectId = "MISSING" }; break;
            case "different-zone": state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF"); break;
            case "different-battlefield": state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["ALLY"] = new("P1", "BATTLEFIELD", "OTHER") } }; break;
            case "hidden-host": host = host with { IsFaceDown = true }; break;
            case "non-unit-host": host = host with { Tags = [CardObjectTags.EquipmentCard] }; break;
            case "unit-gear": gear = gear with { Tags = [..gear.Tags, CardObjectTags.UnitCard] }; break;
            case "unknown-controller": gear = gear with { ControllerId = "MISSING" }; break;
        }
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["GEAR"] = gear, ["ALLY"] = host } };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("BREAK", "RAD·101/167", ["GEAR"]));
    }
}
