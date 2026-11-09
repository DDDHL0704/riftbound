using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BromainGroupKeywordTests
{
    private const string Lord = "UNL-012/219";
    private static MatchState Position(bool field = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "LORD", Lord, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "HOST", "OGN·219/298", "P1", field ? "BATTLEFIELD" : "BASE", field ? "BF" : null);
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", field ? "BASE" : "BATTLEFIELD", field ? null : "BF");
        state = RadGearTestPosition.Put(state, "BLINK", "OGS·011/024", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> {
                [RuneTrait.Red] = 4, [RuneTrait.Yellow] = 4, [RuneTrait.Blue] = 4 }) } };
    }
    private static int Assault(Driver game, string id)
        => TemporaryCombatKeywordRules.Amount(game.State.CardObjects[id], CardCombatKeywordNames.Assault);
    private static Task Play(Driver game, bool field = false)
        => game.Act("P1", new PlayCardCommand("LORD", Lord, [], Destination: field ? "BATTLEFIELD:BF" : "BASE"));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualPlayGrantsAssaultOnlyToOtherCurrentFriendsHereAndItExpires(bool field)
    {
        var game = new Driver(Position(field)); var before = game.State.CardObjects["HOST"].Power;
        await Play(game, field); Assert.Equal(0, Assault(game, "HOST"));
        Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds); Assert.Null(game.State.PendingCardChoice);
        await game.Top(); Assert.Equal(1, Assault(game, "HOST"));
        Assert.Equal(before, game.State.CardObjects["HOST"].Power);
        Assert.Empty(game.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        foreach (var id in new[] { "LORD", "ALLY", "ENEMY" }) Assert.Equal(0, Assault(game, id));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(0, Assault(game, "HOST")); await game.Replay();
    }

    [Fact]
    public async Task TheGrantedAssaultActuallyChangesMightOnlyAfterItsRecipientAttacks()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "ENEMY", "P2", "BATTLEFIELD", "OTHER");
        var game = new Driver(state); var before = game.State.CardObjects["HOST"].Power;
        await Play(game); await game.Top(); Assert.Equal(before, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(before + 1, ArmamentMightTests.Might(game.State));
        Assert.Equal(before, game.State.CardObjects["HOST"].Power); await game.Replay();
    }

    [Fact]
    public async Task ARealBlinkResponseChangesTheLocationUsedByTheGroupInstruction()
    {
        var game = new Driver(Position(true)); await Play(game, true);
        await game.Act("P1", new PlayCardCommand("BLINK", "OGS·011/024", ["LORD"])); await game.Top();
        Assert.Contains("LORD", game.State.PlayerZones["P1"].Base); await game.Top();
        Assert.Equal(1, Assault(game, "ALLY")); Assert.Equal(0, Assault(game, "HOST"));
        Assert.Equal(0, Assault(game, "LORD")); await game.Replay();
    }

    [Fact]
    public async Task ARealSourceReturnMakesHereUnavailableWithoutUsingHistoricalRecipients()
    {
        var game = new Driver(Position()); await Play(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["LORD"])); await game.Top(); await game.Top();
        Assert.Contains("LORD", game.State.PlayerZones["P1"].Hand);
        foreach (var id in new[] { "HOST", "ALLY", "LORD" }) Assert.Equal(0, Assault(game, id)); await game.Replay();
    }

    [Fact]
    public async Task SeparateRealPlaysGrantIndependentStackingAssaultInstances()
    {
        var state = RadGearTestPosition.Put(Position(), "SECOND-LORD", Lord, "P1", "HAND");
        var game = new Driver(state); await Play(game); await game.Top();
        await game.Act("P1", new PlayCardCommand("SECOND-LORD", Lord, [])); await game.Top();
        Assert.Equal(2, Assault(game, "HOST")); Assert.Equal(1, Assault(game, "LORD"));
        Assert.Equal(0, Assault(game, "SECOND-LORD")); Assert.Equal(0, Assault(game, "ALLY"));
        Assert.Equal(2, game.State.CardObjects["HOST"].UntilEndOfTurnEffects.Count(e => e.StartsWith("COMBAT_KEYWORD:强攻:", StringComparison.Ordinal)));
        await game.Replay();
    }

    [Fact]
    public async Task AnEmptyInitialGroupStillProducesAnIndependentTargetlessSkill()
    {
        var state = RadUnitEffectTestFixture.Place(Position(), "HOST", "P1", "HAND");
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("LORD", Lord, ["ENEMY"]));
        var game = new Driver(state); await Play(game); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Equal(0, Assault(game, "LORD")); Assert.Equal(0, Assault(game, "ALLY")); await game.Replay();
    }
}
