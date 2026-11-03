using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenTargetedReplacementFaqTests
{
    // CN VEN official FAQ pp. 12-14 and Gangplank's printed replacement.
    [Theory]
    [InlineData("VEN·086", false)]
    [InlineData("VEN·086", true)]
    [InlineData("VEN·181", false)]
    [InlineData("VEN·181", true)]
    public async Task TargetedMightReductionBecomesThreeMightOnlyForAnEmpoweredSource(string printing, bool empower)
    {
        var initial = AttackDamageDistributionTests.Spell(EmpowerTests.Position(printing), "EXTORT", "OGN·095/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", printing, []));
        Assert.Equal(6, ArmamentMightTests.Might(game.State));
        if (empower) {
            await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
            await game.Top();
            Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
            Assert.Equal(6, ArmamentMightTests.Might(game.State));
        }
        var command = new PlayCardCommand("EXTORT", "OGN·095/298", ["HOST"]);
        var unpayable = game.State with { RunePools = new Dictionary<string, RunePool>(game.State.RunePools) {
            ["P1"] = RunePool.Empty } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpayable,
            new("unpayable-extort", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(unpayable), MatchStateHasher.Hash(rejected.State));

        await game.Act("P1", command);
        await game.Top();
        Assert.Equal(empower ? 9 : 5, ArmamentMightTests.Might(game.State));
        Assert.Equal(empower, game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }
}
