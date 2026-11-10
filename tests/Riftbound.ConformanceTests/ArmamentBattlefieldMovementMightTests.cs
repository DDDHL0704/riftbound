using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentBattlefieldMovementMightTests
{
    private const string Gear = "VEN·011", Wind = "OGN·173/298";
    private static MatchState Position(bool attached = true)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create("OGN·275/298"), "GEAR", Gear, "P1", "BASE");
        state = RadGearTestPosition.Put(state, "WIND", Wind, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "BLINK", "OGS·011/024", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        if (attached) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state);
    }
    private static async Task Move(Driver game)
    { await game.Act("P1", new PlayCardCommand("WIND", Wind, ["ALLY"], Destination: "BATTLEFIELD:BF")); await game.Top(); }
    private static int Might(Driver game) => CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("ALLY").GetProperty("effectivePower").GetInt32();
    [Fact]
    public async Task BattlefieldArrivalEarnsASeparateTwoMightSkillThatExpiresAtTurnEnd()
    {
        var game = new Driver(Position()); var before = Might(game); await Move(game);
        Assert.Equal(before, Might(game)); var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(Gear, skill.CardNo); Assert.True(skill.UnitEvent!.Origin!.ArmamentGranted);
        await game.Top(); Assert.Equal(before + 2, Might(game));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(before, Might(game)); await game.Replay();
    }
    [Fact]
    public async Task AReturnToBaseInResponseDoesNotInvalidateTheOriginalMoveOrItsSameGenerationBuff()
    {
        var game = new Driver(Position()); var before = Might(game); await Move(game);
        await game.Act("P1", new PlayCardCommand("BLINK", "OGS·011/024", ["ALLY"])); await game.Top();
        Assert.Equal("BASE", game.State.ObjectLocations["ALLY"].Zone); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Equal(before + 2, Might(game)); await game.Replay();
    }
    [Fact]
    public async Task ReturningTheHostToHandMakesTheEarnedBuffUnableToAffectItsPrivateNewGeneration()
    {
        var game = new Driver(Position()); await Move(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top(); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Replay();
    }
    [Fact]
    public async Task LooseEquipmentCannotGiveAMovingUnitItsLowerBoxSkill()
    {
        var game = new Driver(Position(attached: false)); var before = Might(game); await Move(game);
        Assert.DoesNotContain(game.State.StackItems, s => s.CardNo == Gear); Assert.Equal(before, Might(game)); await game.Replay();
    }
    [Theory]
    [InlineData("erase")] [InlineData("destination")] [InlineData("duplicate")]
    public async Task RecoveryPreservesTheOriginalGrantedArrivalInsteadOfCurrentLocationOrInventedCopies(string mutation)
    {
        var game = new Driver(Position()); await Move(game); var skill = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, game.State with { StackItems = mutation switch {
            "erase" => [], "duplicate" => [skill, skill],
            _ => [skill with { UnitEvent = skill.UnitEvent! with { Here = new("P1", "BASE") } }] } }));
        await game.Top(); await game.Replay();
    }
}
