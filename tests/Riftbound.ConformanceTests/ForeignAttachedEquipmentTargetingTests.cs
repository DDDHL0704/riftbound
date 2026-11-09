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
