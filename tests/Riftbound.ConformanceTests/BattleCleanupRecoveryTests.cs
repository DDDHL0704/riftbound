using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BattleCleanupRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("winner")]
    [InlineData("stage")]
    [InlineData("tick")]
    [InlineData("recall")]
    [InlineData("bindings")]
    public async Task ReprojectedCleanupMustRetainItsActualAcceptedBirth(string attack)
    {
        var initial = BattleCleanupDeathObserverTimingTests.Opening(true);
        var run = await BattleCleanupDeathObserverTimingTests.Fight(initial);
        var valid = Frame(initial, run);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(valid, new CoreRuleEngine(), default));
        var pending = run.State.PendingBattleResult!;
        Assert.Equal("CLEANUP_TRIGGERS", pending.Stage);
        pending = attack switch
        {
            "winner" => pending with { WinnerPlayerId = "P2" },
            "stage" => pending with { Stage = "RESULT_TRIGGERS" },
            "tick" => pending with { ResultTick = pending.ResultTick - 1 },
            "recall" => pending with { AttackerRecalledByBattleCleanup = true },
            _ => pending with { PublicObjectBindings = [new("FORGED-OBJECT", 1)] }
        };
        var originalCommands = JsonSerializer.Serialize(run.Commands(), Json);
        var originalEvents = JsonSerializer.Serialize(run.Events(), Json);
        var forged = run.State with { PendingBattleResult = pending };
        var frame = Frame(initial, run, forged);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        Assert.Equal(originalCommands, JsonSerializer.Serialize(frame.Commands, Json));
        Assert.Equal(originalEvents, JsonSerializer.Serialize(frame.Events, Json));
        await run.Replay();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    public async Task TheCleanupRecallFactIsAnExplicitRequiredJsonMember(string attack)
    {
        var initial = BattleCleanupDeathObserverTimingTests.Opening(true);
        var run = await BattleCleanupDeathObserverTimingTests.Fight(initial);
        var frame = Frame(initial, run);
        var pending = run.State.PendingBattleResult!;
        var explicitFalse = JsonSerializer.SerializeToElement(pending,
            new JsonSerializerOptions(Json) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        Assert.False(explicitFalse.GetProperty("attackerRecalledByBattleCleanup").GetBoolean());
        var node = JsonSerializer.SerializeToNode(frame, Json)!.AsObject();
        var state = node["authoritativeState"]!.AsObject();
        var context = state["pendingBattleResult"]!.AsObject();
        if (attack == "missing") Assert.True(context.Remove("attackerRecalledByBattleCleanup"));
        else context["attackerRecalledByBattleCleanup"] = null;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MatchRecoveryFrame>(node.ToJsonString(), Json));
        await run.Replay();
    }

    private static MatchRecoveryFrame Frame(MatchState initial, Driver run, MatchState? authority = null)
    {
        var state = authority ?? run.State;
        var sequence = run.Commands()[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(player,
            state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, run.Commands(), run.Events(), views,
            [], state, initial, spectator) { OpeningSeed = initial.Seed };
        return JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
    }
}
