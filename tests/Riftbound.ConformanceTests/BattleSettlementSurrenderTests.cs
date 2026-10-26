using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleSettlementSurrenderTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("CLEANUP_TRIGGERS", "P1")] [InlineData("CLEANUP_TRIGGERS", "P2")]
    [InlineData("RESULT_TRIGGERS", "P1")] [InlineData("RESULT_TRIGGERS", "P2")]
    [InlineData("CONTROL_TRIGGERS", "P1")] [InlineData("CONTROL_TRIGGERS", "P2")]
    public async Task EitherPlayerCanSurrenderBeforeTheNextEarnedBattleTask(string stage, string player)
    {
        // Explicit midgame HAND positions; the actual plays, prompts and journal prefix establish each window.
        MatchState initial; Driver run;
        if (stage == "CLEANUP_TRIGGERS")
        {
            initial = BattleCleanupDeathObserverTimingTests.Opening(true);
            run = await BattleCleanupDeathObserverTimingTests.Fight(initial);
            Assert.Contains(run.State.StackItems, s => s.SourceObjectId == "OBSERVER");
        }
        else
        {
            (initial, run) = await BattleWinnerPhaseBoundaryTests.Open("control-observer");
            await BattleWinnerPhaseBoundaryTests.PassFocus(run); await BattleWinnerPhaseBoundaryTests.Declare(run);
            await BattleWinnerPhaseBoundaryTests.ChooseGuardian(run);
            if (stage == "CONTROL_TRIGGERS") { await run.Top(); Assert.NotNull(run.State.PendingHandChoice); }
        }
        Assert.Equal(stage, run.State.PendingBattleResult!.Stage);
        Assert.Contains(CommandTypes.Surrender, ResolutionResult.BuildPrompts(run.State)[player].EnabledActions());
        var before = run.State; var previousEvents = run.Events().Length;
        var cards = JsonSerializer.Serialize(before.CardObjects, Json); var scores = JsonSerializer.Serialize(before.PlayerScores, Json);
        await run.Act(player, new SurrenderCommand());
        var final = run.State;
        Assert.Equal(MatchStatuses.Finished, final.Status); Assert.Equal(player == "P1" ? "P2" : "P1", final.WinnerPlayerId);
        var won = Assert.Single(run.Events().Skip(previousEvents)); Assert.Equal("MATCH_WON", won.Event.Kind);
        Assert.Equal("SURRENDER", won.Event.Payload["reason"]); Assert.Equal(player, won.Event.Payload["surrenderedPlayerId"]);
        Assert.Equal(cards, JsonSerializer.Serialize(final.CardObjects, Json)); Assert.Equal(scores, JsonSerializer.Serialize(final.PlayerScores, Json));
        Assert.All(ResolutionResult.BuildPrompts(final).Values, p => Assert.Equal(new[] { "WAIT" }, p.EnabledActions()));
        var endedPrompt = ResolutionResult.BuildPrompts(final)[player];
        var late = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.PassPriority,
            promptId = endedPrompt.PromptId, snapshotTick = final.Tick }, Json);
        var beforeCommands = JsonSerializer.Serialize(run.Commands(), Json); var beforeEvents = JsonSerializer.Serialize(run.Events(), Json);
        var beforeState = MatchStateHasher.Hash(final);
        var error = await Assert.ThrowsAsync<MatchSessionException>(() =>
            run.ActRetained(new(player, "late-priority-after-surrender", late), false));
        Assert.Equal(ErrorCodes.MatchFinished, error.Code);
        Assert.Equal(beforeState, MatchStateHasher.Hash(run.State));
        Assert.Equal(beforeCommands, JsonSerializer.Serialize(run.Commands(), Json)); Assert.Equal(beforeEvents, JsonSerializer.Serialize(run.Events(), Json));
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(final), Json), JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(run.State), Json));
        Assert.Equal(JsonSerializer.Serialize(ResolutionResult.BuildPrompts(final), Json), JsonSerializer.Serialize(ResolutionResult.BuildPrompts(run.State), Json));
        var commands = run.Commands(); var events = run.Events(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(final); var prompts = ResolutionResult.BuildPrompts(final);
        var views = final.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, final.Tick, sequence,
            snapshots[p], final.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(final.RoomId, final.Tick, sequence, [], final);
        var frame = new MatchRecoveryFrame(final.RoomId, final.Tick, sequence, commands, events, views, [], final, initial, spectator)
            { OpeningSeed = initial.Seed };
        var cold = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var restored = MatchSession.Restore(cold, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var actor in final.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[actor]), MatchStateHasher.HashValue(restored.SnapshotFor(actor)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[actor]), MatchStateHasher.HashValue(restored.PromptFor(actor)));
        }
        await run.Replay();
    }
}
