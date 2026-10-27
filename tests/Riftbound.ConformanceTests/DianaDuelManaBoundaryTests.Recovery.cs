using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class DianaDuelManaBoundaryTests
{
    [Theory]
    [InlineData("P1", "missing")] [InlineData("P2", "missing")] [InlineData("spectator", "missing")]
    [InlineData("P1", "2")] [InlineData("P2", "-1")] [InlineData("spectator", "2")]
    [InlineData("P1", "\"1\"")] [InlineData("P2", "1.5")] [InlineData("spectator", "null")]
    public async Task PeerAndSpectatorViewsCannotLoseForgeOrCoerceTheActualDuelBalance(string viewer, string value)
    {
        var initial = Position("UNL-197/219"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new LegendActCommand("LEGEND", Ability, [], []));
            var sequence = game.Events().LongLength;
            var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
            var views = game.State.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, game.State.Tick, sequence,
                snapshots[id], game.State.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
            Assert.Empty(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, game.Commands(), game.Events(), views, game.State, game.State.Tick, spectator));
            SnapshotDto Mutate(SnapshotDto snapshot)
            {
                var node = JsonSerializer.SerializeToNode(snapshot, Json)!;
                var pool = node["players"]!["P1"]!["runePool"]!.AsObject();
                if (value == "missing") pool.Remove("duelOnlyMana"); else pool["duelOnlyMana"] = JsonNode.Parse(value);
                return node.Deserialize<SnapshotDto>(Json)!;
            }
            if (viewer == "spectator") spectator = spectator with { SpectatorSnapshot = Mutate(spectator.SpectatorSnapshot) };
            else views[viewer] = views[viewer] with { Snapshot = Mutate(views[viewer].Snapshot) };
            Assert.NotEmpty(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, game.Commands(), game.Events(), views, game.State, game.State.Tick, spectator));
            Assert.Equal(RunePool.Empty with { DuelOnlyMana = 1 }, game.State.RunePools["P1"]);
            await game.Replay();
        }
        finally { await Save("projection-" + viewer + "-" + (value == "missing" ? "missing" : Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(value))), "UNL-197/219", initial, game, null); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task DuelBalanceSharesPhysicalManaCapacityAndOnlyExactFittingProductionRemainsLegal(int free)
    {
        // Representation-boundary Core checks, not actual billion-command histories.
        var initial = Position("UNL-197/219");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(int.MaxValue - 1 - free, 0, duelOnlyMana: 1) } };
        var initialHash = MatchStateHasher.Hash(initial);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        Assert.Equal(free == 1, (prompt.Candidates ?? []).Any(c => c.Action == CommandTypes.LegendAct && c.Enabled
            && (c.Sources ?? []).Any(source => source.Id == "LEGEND")));
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("diana-capacity-" + free, "P1", CommandTypes.LegendAct),
            new LegendActCommand("LEGEND", Ability, [], []), default);
        Assert.Equal(free == 1, result.Accepted);
        if (free == 0)
        {
            Assert.Empty(result.Events); Assert.Equal(initialHash, MatchStateHasher.Hash(result.State));
            Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
        }
        else
        {
            Assert.True(result.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Equal(int.MaxValue - 2, result.State.RunePools["P1"].Mana);
            Assert.Equal(2, result.State.RunePools["P1"].DuelOnlyMana);
            Assert.Equal(int.MaxValue, result.State.RunePools["P1"].TotalStoredMana);
        }
    }

    [Theory]
    [InlineData("\"1\"")] [InlineData("1.5")] [InlineData("true")] [InlineData("2147483648")]
    public void DuelManaCannotBeCoercedThroughJsonEvenWithAReaderThatAllowsNumericStrings(string value)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RunePool>("{\"mana\":0,\"power\":0,\"duelOnlyMana\":" + value + "}", options));
        var negative = new RunePool(0, 0, duelOnlyMana: -1);
        Assert.Equal(-1, negative.DuelOnlyMana); Assert.False(negative.HasSupportedAmounts);
        var overflow = new RunePool(int.MaxValue, 0, duelOnlyMana: 1);
        Assert.Equal(1, overflow.DuelOnlyMana); Assert.False(overflow.HasSupportedAmounts);
        Assert.False(PaymentCostRules.AuthorizePayment(new("bad-duel-pool", "ACTIVATE_ABILITY", "P1"), overflow).Accepted);
    }
}
