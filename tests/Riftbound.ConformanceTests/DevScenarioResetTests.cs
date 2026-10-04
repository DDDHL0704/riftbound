using System.Text.Json;
using Xunit;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.ConformanceTests;

public sealed class DevScenarioResetTests
{
    [Theory]
    [InlineData("basic-play")]
    [InlineData("standard-group-movement")]
    [InlineData("spell-duel")]
    [InlineData("equipment")]
    public async Task ReseedingAfterAContestMatchesAFreshScenario(string scenario)
    {
        static MatchSession Session()
        {
            var session = new MatchSession("reset-proof", new CoreRuleEngine());
            session.EnsurePlayer("P1"); session.EnsurePlayer("P2"); return session;
        }
        static ValueTask<ResolutionResult> Seed(MatchSession session, string id, string scenario) => session.SeedScenarioAsync(
            "P1", id, scenario, JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), CancellationToken.None);
        var used = Session();
        await Seed(used, "first", "standard-group-movement");
        var command = new MoveUnitCommand("P1-BASE-GUARD-001", "BASE", "BATTLEFIELD:P2-SHOWCASE-ARENA",
            SourceObjectIds: ["P1-BASE-GUARD-001", "P1-BATTLEFIELD-UNIT-001"]);
        var moved = await used.SubmitAsync("P1", "move", command, JsonSerializer.SerializeToElement(command), CancellationToken.None);
        Assert.True(moved.Accepted, moved.ErrorMessage);
        Assert.Contains(moved.Events, item => item.Kind == "BATTLEFIELD_CONTESTED");
        var fresh = await Seed(Session(), "fresh", scenario);
        var reset = await Seed(used, "reset", scenario);
        Assert.Equal(MatchStateHasher.Hash(fresh.State), MatchStateHasher.Hash(reset.State with { Tick = fresh.State.Tick }));
        Assert.Equal(fresh.Prompts["P1"].Actions, reset.Prompts["P1"].Actions);
    }
    [Fact]
    public async Task ShowcaseRunePaysPrintedPowerAfterTwoRealTurnTransitions()
    {
        var session = new MatchSession("native-payment", new CoreRuleEngine());
        session.EnsurePlayer("P1"); session.EnsurePlayer("P2");
        var seed = await session.SeedScenarioAsync("P1", "seed", "standard-group-movement", null, CancellationToken.None);
        async ValueTask<ResolutionResult> Submit(string player, string id, GameCommand command)
            => await session.SubmitAsync(player, id, command, null, CancellationToken.None);
        var move = await Submit("P1", "move", new MoveUnitCommand("P1-BASE-GUARD-001", "BASE", "BATTLEFIELD:P1-SHOWCASE-ARENA"));
        Assert.True(move.Accepted, move.ErrorMessage);
        Assert.True((await Submit("P1", "end1", new EndTurnCommand())).Accepted);
        Assert.True((await Submit("P2", "end2", new EndTurnCommand())).Accepted);
        var tap = await Submit("P1", "tap", new TapRuneCommand("P1-SHOWCASE-RUNE-READY"));
        Assert.True(tap.Accepted, tap.ErrorMessage);
        var play = await Submit("P1", "play", new PlayCardCommand("P1-SPELL-HEXTECH-RAY", "OGN·009/298",
            ["P1-BASE-GUARD-001"], OptionalCosts: ["RECYCLE_RUNE:P1-SHOWCASE-RUNE-READY"]));
        Assert.True(play.Accepted, play.ErrorMessage);
        Assert.Equal(0, play.State.RunePools["P1"].Mana);
        Assert.Equal(0, play.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("P1-SHOWCASE-RUNE-READY", play.State.PlayerZones["P1"].Base);
        Assert.Contains(play.Events, item => item.Kind == "COST_PAID");
    }

}
