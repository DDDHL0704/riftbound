using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenGlowingStoneTests
{
    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task StonePaysTwoGenericPowerToEmpowerThenPaysRemovalAndExhaustionBeforeGivingControl(string chosen)
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·133", []));
        Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await Reject(game.State, new ActivateAbilityCommand("UNIT", "GLOWING_STONE_TRANSFER", [chosen]));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        foreach (var invalid in new[] { "ALLY", "GEAR", "P3" })
            await Reject(game.State, new ActivateAbilityCommand("UNIT", "GLOWING_STONE_TRANSFER", [invalid]));
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "GLOWING_STONE_TRANSFER", [chosen]));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
        Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
        await game.Top(); Assert.Equal(chosen, game.State.CardObjects["UNIT"].ControllerId);
        Assert.Equal("P1", game.State.CardObjects["UNIT"].OwnerId); Assert.Contains("UNIT", game.State.PlayerZones[chosen].Base);
        Assert.Equal("BASE", game.State.ObjectLocations["UNIT"].Zone); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        if (chosen == "P2") {
            await game.Act("P1", new EndTurnCommand()); Assert.Contains("UNIT", game.State.PlayerZones["P2"].Base);
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
            await game.Act("P2", new EndTurnCommand()); Assert.Equal(MatchPhases.TurnEnd, game.State.Phase);
            await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
            Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        }
        await game.Replay();
    }

    [Fact]
    public async Task UnempoweredStoneStillDestroysItselfAtItsControllersEndAndDealsFiveToEveryFriendlyUnit()
    {
        var initial = HandDiscardObserverTests.Put(Position(), "SURVIVOR", "VEN·118", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·133", []));
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(MatchPhases.TurnEnd, game.State.Phase); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard); Assert.Contains("SURVIVOR", game.State.PlayerZones["P1"].Base);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT")).ToArray();
        Assert.Equal(new[] { "ALLY", "SURVIVOR" }, damage.Select(e => (string)e.Event.Payload["targetObjectId"]!).Order().ToArray());
        Assert.All(damage, e => Assert.Equal(5, Convert.ToInt32(e.Event.Payload["damage"]))); await game.Replay();
    }

    [Fact]
    public async Task PreventingSkillDamageInResponseStillDestroysTheStoneButSavesTheUnits()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "PREVENT", "OGN·145/298", "P1");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·133", []));
        await game.Act("P1", new EndTurnCommand());
        await game.Act("P1", new PlayCardCommand("PREVENT", "OGN·145/298", [])); await game.Top(); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Base);
        await game.Replay();
    }

    private static MatchState Position() => VenUnitEntryBasicTests.Position("VEN·133");
    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-stone", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
