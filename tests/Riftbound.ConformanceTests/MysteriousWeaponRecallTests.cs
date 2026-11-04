using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class MysteriousWeaponRecallTests
{
    private const string Weapon = "OGN·023/298";
    private const string EkkoNextUnit = "SFD·143/221";

    [Fact]
    public async Task APlayedWeaponCanDiscardAndExhaustToGrantItsFriendlyUnitReplacement()
    {
        var initial = HandDiscardObserverTests.Put(VenUnitEntryBasicTests.Position(Weapon),
            "COST", "UNL-009/219", "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Weapon, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        await game.Replay();

        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(),
            definition => P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(definition, Weapon));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", ability.AbilityId, ["ALLY"],
            OptionalCosts: ["DISCARD_HAND_CARD:COST"]));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("UNIT", skill.SourceObjectId);
        Assert.Equal(new[] { "ALLY" }, skill.TargetObjectIds);
        await game.Top();
        Assert.Contains(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.MysteriousWeaponRecallPrefix, StringComparison.Ordinal));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task EkkoEndTurnDestructionCanBeReplacedAndDoesNotRepeatOnLaterTurns()
    {
        var initial = VenUnitEntryBasicTests.Position(EkkoNextUnit);
        initial = WithTimeAssassinLegend(initial);
        initial = HandDiscardObserverTests.Put(initial, "WEAPON", Weapon, "P1", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "COST", "UNL-009/219", "P1", "HAND");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 20 }) } };
        var game = new Driver(initial);

        await game.Act("P1", new PlayCardCommand("WEAPON", Weapon, []));
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        await game.Act("P1", new PlayCardCommand("UNIT", EkkoNextUnit, []));
        Assert.Single(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        await game.Act("P1", new ActivateAbilityCommand("WEAPON", P4ActivatedAbilityCatalog.MysteriousWeaponRecallAbilityId,
            ["UNIT"], OptionalCosts: ["DISCARD_HAND_CARD:COST"]));
        await game.Top();
        Assert.Contains(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.MysteriousWeaponRecallPrefix, StringComparison.Ordinal));

        await game.Act("P1", new EndTurnCommand());
        await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Equal("P1", request.PlayerId);
        var option = Assert.Single(request.Options, choice => choice.Id.StartsWith("PAID_RECALL:UNIT:", StringComparison.Ordinal));
        var eventsBeforePayment = game.Events().Length;
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option.Id]));

        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(0, game.State.CardObjects["UNIT"].Damage);
        var payment = Assert.Single(game.Events().Skip(eventsBeforePayment), entry => entry.Event.Kind == "COST_PAID"
            && Equals(entry.Event.Payload.GetValueOrDefault("reason"), P4ActivatedAbilityCatalog.MysteriousWeaponRecallAbilityId));
        Assert.Equal(1, payment.Event.Payload["power"]);
        Assert.Equal(new Dictionary<string, int> { [RuneTrait.Red] = 1 },
            Assert.IsType<Dictionary<string, int>>(payment.Event.Payload["powerByTrait"]));
        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal));
        Assert.DoesNotContain(game.State.CardObjects["UNIT"].UntilEndOfTurnEffects,
            effect => effect.StartsWith(CoreRuleEngine.MysteriousWeaponRecallPrefix, StringComparison.Ordinal));

        var eventsBeforeLaterTurn = game.Events().Length;
        await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events().Skip(eventsBeforeLaterTurn), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT"));
        await game.Replay();
    }

    private static MatchState WithTimeAssassinLegend(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };
}
