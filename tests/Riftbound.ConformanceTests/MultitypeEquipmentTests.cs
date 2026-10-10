using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;
using static Riftbound.ConformanceTests.DestructionOrderTests;

namespace Riftbound.ConformanceTests;

public sealed class MultitypeEquipmentTests
{
    // CN 178 and 370.2 permit this state. The catalog contains no animation spell;
    // only the starting type-changing effect is seeded, all actions use real cards.
    internal static MatchState PositionFor(string spell, bool unit = true, bool battlefield = false, int power = 5, string controller = "P2", string? owner = null)
    {
        var s = Gear(NoReplacement());
        var zones = s.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Base = p.Value.Base.Where(id => id != "G1").ToArray(),
            Battlefields = p.Value.Battlefields.Where(id => id != "D").ToArray() });
        zones[controller] = battlefield ? zones[controller] with { Battlefields = [..zones[controller].Battlefields, "G1"] }
            : zones[controller] with { Base = [..zones[controller].Base, "G1"] };
        return s with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["AOE"] = s.CardObjects["AOE"] with { CardNo = spell },
                ["BF"] = s.CardObjects["BF"] with { ControllerId = battlefield ? controller : "P2" },
                ["G1"] = s.CardObjects["G1"] with { CardNo = "OGN·098/298", Power = power, OwnerId = owner ?? controller,
                    ControllerId = controller, Tags = unit ? [CardObjectTags.EquipmentCard, CardObjectTags.UnitCard] : [CardObjectTags.EquipmentCard] } },
            ObjectLocations = s.ObjectLocations.Where(p => p.Key != "D").ToDictionary(p => p.Key, p => p.Key == "G1"
                ? new ObjectLocationState(controller, battlefield ? "BATTLEFIELD" : "BASE", battlefield ? "BF" : null) : p.Value) };
    }

    [Theory]
    [InlineData("OGN·229/298")]
    [InlineData("UNL-180/219")]
    [InlineData("OGN·022/298")]
    public async Task BothTypesCanDestroyAndRecordExactlyOneUnitDeath(string spell)
    {
        var s = Source(PositionFor(spell), "UNL-129/219");
        var result = await Open(s, spell, spell == "OGN·229/298" ? ["G1"] : []);
        Assert.Contains("G1", result.State.PlayerZones["P2"].Graveyard);
        Assert.DoesNotContain(CardObjectTags.UnitCard, result.State.CardObjects["G1"].Tags);
        Assert.Equal(0, result.State.CardObjects["G1"].Damage);
        Assert.Equal(1, result.State.CardObjects["G1"].ObjectGeneration);
        Assert.Single(result.Events, e => e.Kind == "UNIT_DESTROYED" && e.Payload.GetValueOrDefault("targetObjectId") as string == "G1");
        Assert.Equal(spell == "UNL-180/219" ? 2 : 1, result.State.DeathLedger.Counts["P2"]);
        Assert.Equal(spell == "UNL-180/219" ? 0 : 1, result.Events.Count(e => e.Kind == "TRIGGER_QUEUED" && e.Payload.GetValueOrDefault("sourceObjectId") as string == "OBSERVER"));
        Assert.Contains("BF", result.State.PlayerZones["P2"].Battlefields);
        Restore(result.State);
    }

    [Theory]
    [InlineData("P1", 1)]
    [InlineData("P1", 5)]
    [InlineData("P2", 1)]
    [InlineData("P2", 5)]
    public async Task BattlefieldDamageHitsBothPlayersAndOnlyLethalDamageDestroys(string player, int power)
    {
        var result = await Open(PositionFor("OGN·133/298", battlefield: true, power: power, controller: player));
        if (power == 1) Assert.Contains("G1", result.State.PlayerZones[player].Graveyard);
        else {
            Assert.Contains("G1", result.State.PlayerZones[player].Battlefields);
            Assert.Equal(1, result.State.CardObjects["G1"].Damage);
            Assert.DoesNotContain(result.Events, e => e.Kind == "EQUIPMENT_RECALLED_TO_BASE");
        }
        Restore(result.State);
    }

    [Theory]
    [InlineData("OGN·229/298")]
    [InlineData("OGN·095/298")]
    public async Task OrdinaryEquipmentCannotBeChosenAsAUnit(string spell)
    {
        var s = PositionFor(spell, unit: false); var hash = MatchStateHasher.Hash(s);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("bad", "P1", CommandTypes.PlayCard), new PlayCardCommand("AOE", spell, ["G1"]), default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("UNL-180/219")]
    [InlineData("OGN·133/298")]
    public async Task UnitAreaEffectsLeaveOrdinaryEquipmentUntouched(string spell)
    {
        var result = await Open(PositionFor(spell, unit: false), spell);
        Assert.Contains("G1", result.State.PlayerZones["P2"].Base);
        Assert.Equal(0, result.State.CardObjects["G1"].Damage); Restore(result.State);
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task UnitSpellCanTargetEitherController(string player)
    {
        var result = await Open(PositionFor("OGN·095/298", controller: player), "OGN·095/298", "G1");
        Assert.Equal(4, result.State.CardObjects["G1"].Power); Restore(result.State);
    }

    [Fact]
    public async Task EnemyOnlySpellStillRejectsFriendlyEquipmentUnit()
    {
        var s = PositionFor("UNL-073/219", controller: "P1"); var hash = MatchStateHasher.Hash(s);
        var rejected = await new CoreRuleEngine().ResolveAsync(s, new("bad", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("AOE", "UNL-073/219", ["G1"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HiddenEquipmentUnitsRemainUnavailableAsUnitTargets(bool faceDown)
    {
        var s = PositionFor("OGN·229/298");
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["G1"] = s.CardObjects["G1"] with { IsFaceDown = faceDown,
                Tags = faceDown ? s.CardObjects["G1"].Tags : [..s.CardObjects["G1"].Tags, CardObjectTags.Standby] } } };
        var hash = MatchStateHasher.Hash(s);
        var result = await new CoreRuleEngine().ResolveAsync(s, new("hidden", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("AOE", "OGN·229/298", ["G1"]), default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task DeathUsesControllerButDestinationUsesOwner()
    {
        var result = await Open(PositionFor("OGN·229/298", owner: "P1"), "OGN·229/298", "G1");
        Assert.Contains("G1", result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(1, result.State.DeathLedger.Counts["P2"]); Restore(result.State);
    }

    [Fact]
    public async Task EquipmentUnitCanMoveAndRemainsOnBattlefield()
    {
        var s = PositionFor("OGN·095/298", controller: "P1");
        var moved = await Act(s, "P1", new MoveUnitCommand("G1", "BASE", "BATTLEFIELD:BF", []));
        Assert.Contains("G1", moved.State.PlayerZones["P1"].Battlefields);
        Assert.True(moved.State.CardObjects["G1"].IsExhausted); Restore(moved.State);
    }

    [Fact]
    public async Task RealUnitTargetStartsTheOfficialTwoHourglassReplacementChain()
    {
        var s = Gear(Gear(NoReplacement(), unit: true), "G2", unit: true);
        var opened = await Open(s, "OGN·229/298", "G1");
        Restore(opened.State);
        var done = await Select(opened.State, "GEAR:G1:G2");
        Assert.Contains("G1", done.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("G2", done.State.PlayerZones["P2"].Base);
        Assert.Equal(1, done.State.DeathLedger.Counts["P2"]); Restore(done.State);
    }
}
