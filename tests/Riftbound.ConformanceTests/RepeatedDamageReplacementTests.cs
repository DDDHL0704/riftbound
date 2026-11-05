using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// UNL judge FAQ: separate Lotus Traps each double the same damage event.
// CN370.2/372: each replacement applies once, with prevention ordered normally.
public sealed class RepeatedDamageReplacementTests
{
    private const string Effect = "DAMAGE_RECEIVED_DOUBLED_THIS_TURN";

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 12)]
    [InlineData(3, 24)]
    public async Task SeparateLotusTrapsMultiplyEveryLaterRay(int applications, int expected)
    {
        var game = new Driver(Position());
        for (var i = 1; i <= applications; i++) await Cast(game, "LOTUS" + i, "UNL-013/219");
        Assert.Equal(applications, Effects(game.State).Length);
        await Cast(game, "RAY1", "OGN·009/298");
        Assert.Equal(expected, game.State.CardObjects["ENEMY"].Damage);
        await Cast(game, "RAY2", "OGN·009/298");
        Assert.Equal(expected * 2, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(applications, Effects(game.State).Length);
        await game.Replay();
    }

    [Fact]
    public async Task EchoKeepsTwoApplicationsFromOnePhysicalSpell()
    {
        var game = new Driver(Position() with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] });
        await game.Act("P1", new PlayCardCommand("LOTUS1", "UNL-013/219", ["ENEMY"],
            OptionalCosts: ["ECHO"], RepeatChoices: [new("", ["ENEMY"])]));
        await game.Top();
        Assert.Equal(2, Effects(game.State).Length);
        await Cast(game, "RAY1", "OGN·009/298");
        Assert.Equal(12, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Fact]
    public async Task TurnEndClearsEveryApplication()
    {
        var game = new Driver(Position());
        await Cast(game, "LOTUS1", "UNL-013/219");
        await Cast(game, "LOTUS2", "UNL-013/219");
        Assert.Equal(2, Effects(game.State).Length);
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(Effects(game.State));
        await game.Replay();
    }

    [Theory]
    [InlineData("DAMAGE_RECEIVED_DOUBLED_THIS_TURN:4")]
    [InlineData("DAMAGE_RECEIVED_DOUBLED_THIS_TURN:02")]
    [InlineData("DAMAGE_RECEIVED_DOUBLED_THIS_TURN:invalid")]
    public async Task RecoveryRejectsNoncanonicalApplicationReceipts(string forged)
    {
        var game = new Driver(Position());
        await Cast(game, "LOTUS1", "UNL-013/219");
        var enemy = game.State.CardObjects["ENEMY"];
        var changed = game.State with { CardObjects = new Dictionary<string, CardObjectState>(game.State.CardObjects) {
            ["ENEMY"] = enemy with { UntilEndOfTurnEffects = [Effect, forged] } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(changed),
            error => error.Contains("invalid damage doubling applications", StringComparison.Ordinal));
        await game.Replay();
    }

    private static MatchState Position()
    {
        // Keep the FAQ's printed three-damage Ray free of Void Gate's +1 modifier.
        var state = Put(Create("OGN·297/298"), "OTHER", "OGN·297/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = 80 } } };
        for (var i = 1; i <= 3; i++) state = Put(state, "LOTUS" + i, "UNL-013/219", "P1", "HAND");
        return Put(Put(state, "RAY1", "OGN·009/298", "P1", "HAND"), "RAY2", "OGN·009/298", "P1", "HAND");
    }

    private static string[] Effects(MatchState state)
        => state.CardObjects["ENEMY"].UntilEndOfTurnEffects.Where(e => e == Effect
            || e.StartsWith(Effect + ":", StringComparison.Ordinal)).ToArray();

    private static async Task Cast(Driver game, string id, string cardNo)
    {
        await game.Act("P1", new PlayCardCommand(id, cardNo, ["ENEMY"]));
        await game.Top();
    }
}
