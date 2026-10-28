using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RenektonInlinePaymentTests
{
    private const string Ability = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private const string Resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:2";
    private const string Mana = "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:2";

    [Fact]
    public async Task LegendResourceCanBeUsedDuringActualUnitPaymentAndReplayed()
    {
        var initial = Position(new RunePool(2, 3));
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [Resource, Mana], Destination: "BASE");
        var before = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        Assert.Single(game.Events(), e => e.Event.Kind == "MANA_GAINED");
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "COST_PAID"));
        await game.Replay();
    }

    [Fact]
    public async Task TheLegendCostCannotBorrowThePowerNeededForTheUnit()
    {
        var initial = Position(new RunePool(2, 2));
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [Resource, Mana], Destination: "BASE");
        var before = MatchStateHasher.Hash(initial);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command)).CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("inline-insufficient-power", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task InlineLegendAndUnitCanChooseDifferentPowerSources()
    {
        var initial = Position(new RunePool(2, 2, new Dictionary<string, int> { ["red"] = 1, ["purple"] = 1 }));
        var resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:1,red:1";
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts:
            [resource, Mana, "POWER_SPEND:purple:1"], Destination: "BASE");
        var game = new Driver(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        await game.Act("P1", command);
        var pool = game.State.RunePools["P1"];
        Assert.Equal(1, pool.Power); Assert.Equal(0, pool.PowerByTrait.GetValueOrDefault("red"));
        Assert.Equal(0, pool.PowerByTrait.GetValueOrDefault("purple"));
        Assert.Equal(0, pool.TotalStoredMana);
        await game.Replay();
    }

    private static MatchState Position(RunePool pool)
    {
        var state = OfficialHoldSequenceTests.AddLegend(RestrictedPowerPlayTests.Position("SFD·143/221", false), "VEN·141");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }
}
