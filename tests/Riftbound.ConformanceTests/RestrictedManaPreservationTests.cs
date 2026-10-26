using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.RestrictedManaPaymentTests;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedManaPreservationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task AnIndependentLegendActivationUsesOnlyOrdinaryManaAndRetainsTheRestrictedPool(int mana, bool accepted)
    {
        var state = RestrictedManaContinuationTests.AddSource(RestrictedManaLifecycleTests.Position(), "TEEMO", "OGN·121/298", "P1");
        state = Pool(state, new(mana, 10));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "OGN·263/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        var acquired = await Act(state, new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        var command = new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TeemoLegendAbilityId, ["TEEMO"], ["SPEND_MANA:1"]);
        if (!accepted) { await Rejected(acquired.State, command, ErrorCodes.InsufficientCost); return; }
        var result = await Act(acquired.State, command);
        Assert.Equal(0, result.State.RunePools["P1"].Mana); Assert.Equal(2, result.State.RunePools["P1"].SpellOnlyMana);
        Assert.True(result.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task PaidSimultaneousMovementRetainsRestrictedManaInItsPowerPaymentPath()
    {
        var state = RestrictedManaContinuationTests.AddSource(RestrictedManaLifecycleTests.Position(), "A", "SFD·125/221", "P1");
        state = RestrictedManaContinuationTests.AddSource(state, "B", "SFD·125/221", "P1");
        state = RestrictedManaContinuationTests.AddSource(state, "TAX", "UNL-163/219", "P2");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = [], Battlefields = state.PlayerZones["P2"].Battlefields.Append("TAX").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["TAX"] = new("P2", "BATTLEFIELD", "FIELD") } };
        var driver = new StandbyDefendObserverTests.Driver(state);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await driver.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD", SourceObjectIds: ["A", "B"]));
        Assert.Equal(9, driver.State.RunePools["P1"].Power); Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        Assert.Contains(driver.Events(), e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("powerCost"), 1));
        await RideTheWindCurrentMovementTests.Replay(state, driver);
    }

    [Fact]
    public async Task OrdinaryRuneTapAndRecyclePreserveTheSeparateRestrictedBalance()
    {
        var initial = VoluntaryPaymentResourceChoicesTests.WithRune(RestrictedManaLifecycleTests.Position(), RuneTrait.Red);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["EXTRA-RUNE"] = initial.CardObjects["EXTRA-RUNE"] with { IsExhausted = false } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await driver.Act("P1", new TapRuneCommand("EXTRA-RUNE"));
        Assert.Equal(1, driver.State.RunePools["P1"].Mana); Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        await driver.Act("P1", new RecycleRuneCommand("EXTRA-RUNE"));
        Assert.Equal(1, driver.State.RunePools["P1"].Mana); Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(1, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("UNL-093/219", 1, 10)]
    [InlineData("OGN·098/298", 1, 10)]
    public async Task OrdinaryResourceAbilitiesRetainAlreadyAcquiredRestrictedMana(string card, int mana, int power)
    {
        var initial = RestrictedManaContinuationTests.AddSource(RestrictedManaLifecycleTests.Position(), "OTHER", card, "P1");
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == card);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await driver.Act("P1", new ActivateAbilityCommand("OTHER", ability.AbilityId, []));
        Assert.Equal(mana, driver.State.RunePools["P1"].Mana); Assert.Equal(power, driver.State.RunePools["P1"].Power);
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task AnOrdinaryManaGainCannotOverflowTheCombinedPoolAndCannotExhaustItsSource()
    {
        var state = RestrictedManaContinuationTests.AddSource(RestrictedManaLifecycleTests.Position(), "OTHER", "UNL-093/219", "P1");
        state = Pool(state, new(0, 0, spellOnlyMana: int.MaxValue));
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == "UNL-093/219");
        await Rejected(state, new ActivateAbilityCommand("OTHER", ability.AbilityId, []), ErrorCodes.UnsupportedCardBehavior);
        Assert.False(state.CardObjects["OTHER"].IsExhausted);
    }
}
