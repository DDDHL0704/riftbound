using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class NaturalBattleDeclarationOrderTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NaturalMoveDuelRetainsTheAcceptedCombatDeclarationOrder(bool reverseDeclaration)
    {
        var initial = ConquestLifecycleRegressionTests.State("OGN·278/298", 0);
        var cards = initial.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var zones = initial.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        var locations = initial.ObjectLocations.ToDictionary(e => e.Key, e => e.Value);
        cards["UNIT"] = cards["UNIT"] with { Power = 2 };
        cards["A-FIRST"] = cards["UNIT"] with { ObjectId = "A-FIRST" };
        cards["DEFENDER"] = new("DEFENDER", cardNo: "SFD·125/221", power: 1,
            tags: [CardObjectTags.UnitCard], ownerId: "P2", controllerId: "P2");
        cards["BF"] = cards["BF"] with { ControllerId = "P2" };
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Append("A-FIRST").ToArray() };
        zones["P2"] = zones["P2"] with { Battlefields = ["BF", "DEFENDER"] };
        locations["A-FIRST"] = new("P1", "BASE"); locations["DEFENDER"] = new("P2", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", [], ["UNIT", "A-FIRST"]));
        await driver.Act("P1", new PassFocusCommand());
        await driver.Act("P2", new PassFocusCommand());
        var attackers = reverseDeclaration ? new[] { "A-FIRST", "UNIT" } : new[] { "UNIT", "A-FIRST" };
        var declared = await driver.Act("P1", new DeclareBattleCommand("BF", attackers, ["DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(attackers, Assert.IsAssignableFrom<IEnumerable<string>>(
            Assert.Single(declared.Events, e => e.Kind == "BATTLE_DECLARED").Payload["attackerObjectIds"]));
        await driver.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF",
            [new("UNIT", "DEFENDER", 2), new("A-FIRST", "DEFENDER", 2)]));
        var settled = await driver.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("DEFENDER", "UNIT", 1)]));
        Assert.Equal(attackers[0], Assert.Single(settled.Events, e => e.Kind == "BATTLEFIELD_CONQUERED").Payload["sourceObjectId"]);
        Assert.All(attackers, id => Assert.Contains(id, driver.State.PlayerZones["P1"].Battlefields));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }
}
