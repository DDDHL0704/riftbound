using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RunePoolEventReplayTests
{
    [Theory]
    [InlineData("reordered", true)]
    [InlineData("replaced", false)]
    [InlineData("duplicate", false)]
    public async Task SimultaneousPoolClearSurvivesHistoricalSeatOrderButRejectsChangedParticipants(string change, bool matches)
    {
        var seedSession = new MatchSession("pool-replay", new CoreRuleEngine());
        seedSession.EnsurePlayer("完整对局甲"); seedSession.EnsurePlayer("完整对局乙");
        var seeded = await seedSession.SeedScenarioAsync("完整对局甲", "seed", "native-play-confirmation", null, default);
        var state = seeded.State;
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("end", state.TurnPlayerId, "END_TURN"), new EndTurnCommand(), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(result.Events, e => e.Kind == "RUNE_POOL_CLEARED");
        var events = result.Events.Select((e, i) =>
        {
            if (e.Kind == "RUNE_POOL_CLEARED")
            {
                Assert.Equal(new[] { "完整对局乙", "完整对局甲" }, Assert.IsType<string[]>(e.Payload["playerIds"]));
                var payload = e.Payload.ToDictionary(x => x.Key, x => x.Value);
                payload["playerIds"] = change switch
                {
                    "reordered" => new[] { "完整对局甲", "完整对局乙" },
                    "replaced" => new[] { "完整对局甲", "篡改" },
                    _ => new[] { "完整对局甲", "完整对局乙", "完整对局乙" }
                };
                e = e with { Payload = payload };
            }
            return new RecoveredEvent(i + 1, result.State.Tick, i, e);
        }).ToArray();
        // Simulate JSONB materialization, whose object enumeration order need not match live dictionaries.
        events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(events))!;
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "END_TURN" });
        var command = new RecoveredCommand(state.TurnPlayerId, "end", "END_TURN", raw, state.Tick, result.State.Tick, 0, events.Length, true, null);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [command], result.State, engine, default, events);
        Assert.Equal(matches, replay.IsMatch);
        if (matches) Assert.Empty(replay.Errors);
        else Assert.Contains(replay.Errors, error => error.Contains("payload hash", StringComparison.Ordinal));
    }
}
