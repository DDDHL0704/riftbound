using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class LegendEmpowerActivationTests
{
    [Theory]
    [InlineData("VEN·139")]
    [InlineData("VEN·189")]
    [InlineData("VEN·189*")]
    public async Task PrintedLegendPaysThreeManaAndOnePowerThenEmpowersAfterResponses(string cardNo)
    {
        var initial = LegendAttachmentTests.PositionFor(cardNo);
        var before = initial.RunePools["P1"];
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        Assert.Contains(prompt.Candidates!, c => c.Action == CommandTypes.ActivateAbility && c.Sources!.Any(x => x.Id == "LEGEND"));
        var game = new VenBasicFlowSpellTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", []));
        Assert.Equal(before.Mana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.False(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.NotNull(Assert.Single(game.State.StackItems).EmpowerActivation);
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        Assert.False(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("power")]
    [InlineData("empowered")]
    [InlineData("target")]
    [InlineData("wrong-player")]
    public async Task InvalidLegendEmpowerDoesNotChangeState(string branch)
    {
        var state = LegendAttachmentTests.PositionFor("VEN·139");
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 20) } };
        if (branch == "power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 0) } };
        if (branch == "empowered") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = state.CardObjects["LEGEND"] with { IsEmpowered = true } } };
        var command = new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", branch == "target" ? ["HOST"] : []);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-legend-empower", branch == "wrong-player" ? "P2" : "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
