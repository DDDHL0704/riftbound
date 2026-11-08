using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SameBattlefieldPowerSwapTests
{
    private const string Swap = "SFD·145/221";

    private static MatchState Position(bool sameField = true)
    {
        var state = RadUnitEffectTestFixture.Place(RadGearTestPosition.Create(), "ALLY", "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "SECOND", "OGN·082/298", "P1", "BATTLEFIELD", sameField ? "BF" : "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1" } } };
        return AttackDamageDistributionTests.Spell(state, "SWAP", Swap, "P1");
    }

    private static int Power(MatchState state, string id) => CurrentPowerTargetTests.Snapshot(state)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();

    [Fact]
    public async Task TwoUnitsAtTheSameActualBattlefieldExchangeCurrentPowerUntilEndOfTurn()
    {
        var game = new Driver(Position()); var first = Power(game.State, "ALLY"); var second = Power(game.State, "SECOND");
        Assert.NotEqual(first, second);
        await game.Act("P1", new PlayCardCommand("SWAP", Swap, ["ALLY", "SECOND"])); await game.Top();
        Assert.Equal(second, Power(game.State, "ALLY")); Assert.Equal(first, Power(game.State, "SECOND"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(first, Power(game.State, "ALLY")); Assert.Equal(second, Power(game.State, "SECOND")); await game.Replay();
    }

    [Fact]
    public async Task DifferentBattlefieldsAreRejectedAtPreviewAndSubmissionWithoutPaying()
    {
        var state = Position(false); var command = new PlayCardCommand("SWAP", Swap, ["ALLY", "SECOND"]);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).IsValid);
        await RadGearTestPosition.Reject(state, "P1", command);
    }

    [Fact]
    public async Task ReturningOneTargetInResponsePreventsAnExchangeWithTheCardInHand()
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1"));
        var first = Power(game.State, "ALLY");
        await game.Act("P1", new PlayCardCommand("SWAP", Swap, ["ALLY", "SECOND"]));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SECOND"]));
        await game.Top(); await game.Top();
        Assert.Contains("SECOND", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(first, Power(game.State, "ALLY")); Assert.Empty(game.State.StackItems); await game.Replay();
    }
}
