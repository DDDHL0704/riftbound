using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PowerSpentTurnHistoryTests
{
    [Theory]
    [InlineData("SFD·143/221", false, 1)] [InlineData("SFD·143/221", true, 2)]
    [InlineData("SFD·143a/221", false, 1)] [InlineData("SFD·143a/221", true, 2)]
    public async Task RealPrintedAndOptionalFeesProduceOneAcceptedPowerReceipt(string no, bool haste, int paid)
    {
        var initial = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SIVIR", no, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SIVIR", no, [],
            OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : []));
        Assert.Equal(paid, game.State.RunePools["P1"].PaidPowerThisTurn);
        Assert.Equal(0, game.State.RunePools["P2"].PaidPowerThisTurn);
        var receipt = Assert.Single(game.Events(), row => row.Event.Kind == CoreRuleEngine.PowerSpentEvent);
        Assert.Equal("P1", receipt.Event.Payload["playerId"]);
        Assert.Equal(paid, receipt.Event.Payload["amount"]);
        Assert.Equal(paid, receipt.Event.Payload["paidPowerTotal"]);
        Assert.Equal(game.State.TurnNumber, receipt.Event.Payload["turnNumber"]);
        await game.Replay();
    }

    [Theory, InlineData(-1), InlineData(1)]
    public async Task RebuiltPublicViewsCannotAuthenticateAnAlteredPaidPowerCount(int change)
    {
        const string no = "SFD·143/221";
        var game = new Driver(RadGearTestPosition.Put(RadGearTestPosition.Create(), "SIVIR", no, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SIVIR", no, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        var pool = game.State.RunePools["P1"];
        var forged = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = pool with { PaidPowerThisTurn = pool.PaidPowerThisTurn + change } } };
        Assert.Contains(ArmamentGrantedCombatTriggerTests.Errors(game, forged), error =>
            error.Contains("power payment counter differs from original accepted debit history", StringComparison.Ordinal));
        await game.Replay();
    }
}
