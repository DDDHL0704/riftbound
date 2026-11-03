using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    [Fact]
    public async Task BorrowedPoroTreatsDestroyThisCardAsTheBorrowersCostAndThenDraws()
    {
        var game = await WithTemplate("SFD·046/221", "EQUIPMENT", pool:
            new(8, 4, new Dictionary<string, int> { ["green"] = 4 }));
        var id = TemplateId(game.State, "抽 1 张牌");
        var beforeHand = game.State.PlayerZones["P1"].Hand.Count;
        var beforeMana = game.State.RunePools["P1"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("GRAVEYARD", game.State.ObjectLocations["HOST"].Zone);
        Assert.Single(game.State.StackItems);
        Assert.Equal(beforeMana with { Mana = beforeMana.Mana - 1,
            PowerByTrait = new Dictionary<string, int> { ["green"] = 3 } }, game.State.RunePools["P1"]);
        await game.Top();
        Assert.Equal(beforeHand + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("wrong-target")]
    [InlineData("no-green")]
    [InlineData("no-mana")]
    public async Task BorrowedPoroRejectsIllegalDestroyAndDrawCostsAtomically(string boundary)
    {
        var game = await WithTemplate("SFD·046/221", "EQUIPMENT", pool:
            new(8, 4, new Dictionary<string, int> { ["green"] = 4 }));
        var id = TemplateId(game.State, "抽 1 张牌");
        var state = game.State;
        if (boundary == "no-green")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(8, 0, new Dictionary<string, int> { ["blue"] = 4 }) } };
        if (boundary == "no-mana")
            state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 4, new Dictionary<string, int> { ["green"] = 4 }) } };
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("borrowed-self-cost-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, boundary == "wrong-target" ? ["ALLY"] : []), default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain("HOST", result.State.PlayerZones["P1"].Graveyard);
        Assert.False(result.State.CardObjects["HOST"].IsExhausted);
    }
}
