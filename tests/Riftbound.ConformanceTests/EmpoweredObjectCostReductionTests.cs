using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Shared cost contract for CN VEN·059 and core 442/827. The printing and
// real-card command consumers remain separate from these fixed cost positions.
public sealed class EmpoweredObjectCostReductionTests
{
    private static readonly CardBehaviorDefinition Behavior = new(
        "VEN·059", "电能震荡", 3, "DAMAGE", 4, 1,
        CostReductionConditionKind: CardCostReductionConditionKinds.ControllerControlsEmpoweredObject,
        CostReductionMana: 2);

    [Theory]
    [InlineData("BASE", CardObjectTags.UnitCard)]
    [InlineData("BATTLEFIELD", CardObjectTags.UnitCard)]
    [InlineData("BASE", CardObjectTags.EquipmentCard)]
    [InlineData("LEGEND", "CARD_TYPE:LEGEND")]
    [InlineData("BASE", CardObjectTags.RuneCard)]
    [InlineData("BASE", "TOKEN")]
    public void AnyControlledEmpoweredPublicObjectReducesTheManaCost(string zone, string tag)
        => AssertCost(Position(zone, tag), 1, 2);

    [Theory]
    [InlineData("P1", "P1", "P1", 1)]
    [InlineData("P2", "P1", "P2", 1)]
    [InlineData("P1", "P2", "P1", 3)]
    [InlineData("P2", "P2", "P2", 3)]
    public void CurrentControllerTakesPrecedenceOverOwnerAndZoneHolder(
        string owner, string controller, string zonePlayer, int mana)
        => AssertCost(Position("BASE", CardObjectTags.UnitCard, owner, controller, zonePlayer), mana, 3 - mana);

    [Theory]
    [InlineData("HAND")]
    [InlineData("MAIN_DECK")]
    [InlineData("RUNE_DECK")]
    [InlineData("GRAVEYARD")]
    [InlineData("BANISHED")]
    [InlineData("CHAMPION")]
    [InlineData("STACK")]
    [InlineData("NONE")]
    public void AnEmpoweredFlagOutsideTheFieldAndLegendZoneCannotReduceTheCost(string zone)
        => AssertCost(Position(zone, CardObjectTags.UnitCard), 3, 0);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AKeywordTagOrFaceDownObjectCannotStandInForTheVisibleEmpoweredState(bool empowered, bool faceDown)
    {
        var state = Position("BASE", CardObjectTags.UnitCard);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CONTRIBUTOR"] = state.CardObjects["CONTRIBUTOR"] with {
                IsEmpowered = empowered, IsFaceDown = faceDown,
                Tags = [CardObjectTags.UnitCard, "EMPOWERED", "已强化"] } } };
        AssertCost(state, 3, 0);
    }

    [Fact]
    public void AnOwnedButUncontrolledBattlefieldDoesNotGrantTheReduction()
        => AssertCost(Position("BATTLEFIELD", "CARD_TYPE:BATTLEFIELD", controller: ""), 3, 0);

    [Fact]
    public void MultipleEmpoweredObjectsApplyOnlyOneReductionToTheTotal()
    {
        var state = Position("BASE", CardObjectTags.EquipmentCard);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SECOND"] = new("SECOND", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) { IsEmpowered = true } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = ["CONTRIBUTOR", "SECOND"] } } };
        AssertCost(state, 1, 2);
        AssertCost(state, 5, 2, additionalMana: 4);
        AssertCost(state, 0, 1, behavior: Behavior with { ManaCost = 1 });
    }

    [Fact]
    public async Task RealEmpowerResolutionChangesTheSharedCostAndRealReturnRestoresIt()
    {
        var entered = await EmpowerTests.Enter("VEN·074");
        AssertCost(entered, 3, 0);
        var pending = await EmpowerTests.Activate(entered);
        Assert.True(pending.Accepted, pending.ErrorMessage);
        Assert.False(pending.State.CardObjects["HOST"].IsEmpowered);
        AssertCost(pending.State, 3, 0);
        var empowered = (await OfficialSpellCompletionTests.Top(pending.State)).State;
        Assert.True(empowered.CardObjects["HOST"].IsEmpowered);
        AssertCost(empowered, 1, 2);
        var ready = AttackDamageDistributionTests.Spell(empowered, "RETURN", "OGN·104/298", "P1");
        var cast = await OfficialSpellCompletionTests.Act(ready, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        Assert.True(cast.Accepted, cast.ErrorMessage);
        AssertCost(cast.State, 1, 2);
        var returned = (await OfficialSpellCompletionTests.Top(cast.State)).State;
        Assert.Contains("HOST", returned.PlayerZones["P1"].Hand);
        Assert.False(returned.CardObjects["HOST"].IsEmpowered);
        AssertCost(returned, 3, 0);
    }

    private static void AssertCost(MatchState state, int mana, int reduction, int additionalMana = 0,
        CardBehaviorDefinition? behavior = null)
    {
        var before = MatchStateHasher.Hash(state);
        var calculated = Method("CalculatePlayManaCost").Invoke(null,
            [state, "P1", behavior ?? Behavior, additionalMana, 0, null, null, null, null])!;
        var minimum = Method("MinimumPlayManaCost").Invoke(null,
            [state, "P1", behavior ?? Behavior, null, additionalMana])!;
        foreach (var result in new[] { calculated, minimum })
        {
            Assert.Equal(mana, Assert.IsType<int>(result.GetType().GetProperty("Total")!.GetValue(result)));
            Assert.Equal(reduction, Assert.IsType<int>(result.GetType().GetProperty("CardReduction")!.GetValue(result)));
        }
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    private static MethodInfo Method(string name)
        => typeof(CoreRuleEngine).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    private static MatchState Position(string zone, string tag, string owner = "P1", string controller = "P1", string zonePlayer = "P1")
    {
        var zones = zone switch {
            "BASE" => PlayerZones.Empty with { Base = ["CONTRIBUTOR"] },
            "BATTLEFIELD" => PlayerZones.Empty with { Battlefields = ["CONTRIBUTOR"] },
            "LEGEND" => PlayerZones.Empty with { LegendZone = ["CONTRIBUTOR"] },
            "HAND" => PlayerZones.Empty with { Hand = ["CONTRIBUTOR"] },
            "MAIN_DECK" => PlayerZones.Empty with { MainDeck = ["CONTRIBUTOR"] },
            "RUNE_DECK" => PlayerZones.Empty with { RuneDeck = ["CONTRIBUTOR"] },
            "GRAVEYARD" => PlayerZones.Empty with { Graveyard = ["CONTRIBUTOR"] },
            "BANISHED" => PlayerZones.Empty with { Banished = ["CONTRIBUTOR"] },
            "CHAMPION" => PlayerZones.Empty with { ChampionZone = ["CONTRIBUTOR"] },
            _ => PlayerZones.Empty };
        var state = PlayCostPreviewTests.Position(new(10, 0));
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [zonePlayer] = zones },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["CONTRIBUTOR"] = new("CONTRIBUTOR", ownerId: owner, controllerId: controller, tags: [tag]) { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["CONTRIBUTOR"] = new(zonePlayer, zone) } };
    }
}
