using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class UnitManaReceiptCapacityTests
{
    [Fact]
    public async Task UnitPlayReceiptDistinguishesRestrictedManaFromOrdinaryMana()
    {
        var initial = RestrictedPowerPlayTests.Position("SFD·143/221", false);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 1, unitPlayOrUnitAbilityOnlyMana: 4) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("CARD", "SFD·143/221", [], Destination: "BASE"));
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event.Payload;
        Assert.Equal(4, Convert.ToInt32(paid["mana"]));
        Assert.Equal(0, Convert.ToInt32(paid["ordinaryManaSpent"]));
        Assert.Equal(4, Convert.ToInt32(paid["unitPlayOrUnitAbilityOnlyManaSpent"]));
        Assert.Equal(0, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        await game.Replay();
    }

    [Fact]
    public async Task FullRestrictedManaPoolDoesNotOfferAnUnrepresentableOrdinaryGain()
    {
        var initial = RestrictedManaContinuationTests.AddSource(
            RestrictedManaLifecycleTests.Position(), "OTHER", "UNL-093/219", "P1");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 0, unitPlayOrUnitAbilityOnlyMana: int.MaxValue) } };
        var ability = Assert.Single(P4ActivatedAbilityCatalog.GetAll(), a => a.SourceCardNo == "UNL-093/219");
        var command = new ActivateAbilityCommand("OTHER", ability.AbilityId, []);
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [],
            candidate => candidate.Action == CommandTypes.ActivateAbility && (candidate.Sources ?? []).Any(source => source.Id == "OTHER"));
        var result = await new CoreRuleEngine().ResolveAsync(initial,
            new("unit-mana-capacity", "P1", CommandTypes.ActivateAbility), command, default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["OTHER"].IsExhausted);
    }
}
