using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSpinningAxeStackTests
{
    private const string Axe = "RAD·148/167";

    private static MatchState Position(string ally)
    {
        var state = RadSpellDuelReactionTests.Position();
        var unit = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", ally, "P1");
        if (CardBehaviorRegistry.TryGetByCardNo(ally, out var behavior))
            unit = unit with { Tags = unit.Tags.Concat(behavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.Ordinal).ToArray() };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = unit,
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2") } };
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "AXE1", Axe, "P1");
        return AttackDamageDistributionTests.Spell(state, "AXE2", Axe, "P1");
    }

    private static async Task<Driver> StackBoth(MatchState initial)
    {
        var game = new Driver(initial);
        await RadSpellDuelReactionTests.OpenDuel(game);
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].TotalPower;
        foreach (var source in new[] { "AXE1", "AXE2" })
            await game.Act("P1", new PlayCardCommand(source, Axe, ["ALLY", "ENEMY"]));
        Assert.Equal(mana - 4, game.State.RunePools["P1"].Mana);
        Assert.Equal(power - 2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(new[] { "AXE1", "AXE2" }, game.State.StackItems.Select(item => item.SourceObjectId));
        Assert.Empty(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        return game;
    }

    private static (string Source, int Damage)[] Damage(Driver game)
        => game.Events().Select(entry => entry.Event).Where(ev => ev.Kind == "DAMAGE_APPLIED"
                && ev.Payload.GetValueOrDefault("sourceObjectId") is string source && source.StartsWith("AXE", StringComparison.Ordinal))
            .Select(ev => ((string)ev.Payload["sourceObjectId"]!, (int)ev.Payload["damage"]!)).ToArray();

    private static int Grants(Driver game)
        => game.State.CardObjects["ALLY"].UntilEndOfTurnEffects.Count(effect =>
            effect.StartsWith("COMBAT_KEYWORD:坚守2:", StringComparison.Ordinal));

    [Theory]
    [InlineData("OGN·219/298", 2, 4)]
    [InlineData("RAD·031/167", 5, 7)]
    public async Task EachAxeAddsItsOwnGrantAndReadsTheTotalWhenItResolves(string ally, int topDamage, int bottomDamage)
    {
        var game = await StackBoth(Position(ally));
        var pools = MatchStateHasher.HashValue(game.State.RunePools);
        await game.Top();
        Assert.Equal(new[] { ("AXE2", topDamage) }, Damage(game));
        Assert.Equal(1, Grants(game));
        await game.Top();
        Assert.Equal(new[] { ("AXE2", topDamage), ("AXE1", bottomDamage) }, Damage(game));
        Assert.Equal(2, Grants(game));
        Assert.Equal(pools, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Empty(game.State.StackItems);
        Assert.Contains("AXE1", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("AXE2", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task CounteringTheOlderAxeAfterTheNewerOneResolvesKeepsOnlyTheResolvedGrantAndDamage()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("OGN·219/298"), "COUNTER", "OGN·045/298", "P2");
        var game = await StackBoth(initial);
        var mana = game.State.RunePools["P1"].Mana;
        var power = game.State.RunePools["P1"].TotalPower;
        await game.Top();
        var bottom = Assert.Single(game.State.StackItems);
        Assert.Equal("AXE1", bottom.SourceObjectId);
        if (game.State.PriorityPlayerId != "P2")
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [bottom.StackItemId]));
        await game.Top();
        Assert.Equal(new[] { ("AXE2", 2) }, Damage(game));
        Assert.Equal(1, Grants(game));
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(power, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems);
        Assert.Contains("AXE1", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("ALLY", "P1")]
    [InlineData("ENEMY", "P2")]
    public async Task ReturningATargetBetweenAxesUsesItsOriginalGenerationAndKeepsOnlyApplicableInstructions(string target, string player)
    {
        var initial = AttackDamageDistributionTests.Spell(Position("OGN·219/298"), "RETURN", "OGN·104/298", player);
        var generation = initial.CardObjects[target].ObjectGeneration;
        var game = await StackBoth(initial);
        await game.Top();
        Assert.Equal(1, Grants(game));
        if (game.State.PriorityPlayerId != player)
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", [target]));
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones[player].Hand);
        Assert.True(game.State.CardObjects[target].ObjectGeneration > generation);
        Assert.Single(game.State.StackItems, item => item.SourceObjectId == "AXE1");
        await game.Top();
        Assert.Equal(new[] { ("AXE2", 2) }, Damage(game));
        Assert.Equal(target == "ALLY" ? 0 : 2, Grants(game));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
