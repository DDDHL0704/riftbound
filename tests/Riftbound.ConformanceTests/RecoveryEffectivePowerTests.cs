using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RecoveryEffectivePowerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpectatorRecoveryUsesProjectedAuraPowerAndRejectsTampering(bool tamper)
    {
        var session = new MatchSession("aura-recovery", new CoreRuleEngine());
        session.EnsurePlayer("P1"); session.EnsurePlayer("P2");
        var seeded = await session.SeedScenarioAsync("P1", "seed", "native-play-confirmation", null, default);
        var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(seeded.State))!;
        var frame = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        var player = Assert.IsType<Dictionary<string, object?>>(frame.SpectatorSnapshot.Players["P1"]);
        var objects = Assert.IsType<Dictionary<string, object?>>(player["objects"]);
        var ally = Assert.IsType<Dictionary<string, object?>>(objects["QA-ALLY"]);
        Assert.Equal(3, ally["power"]);
        Assert.Equal(4, ally["effectivePower"]);
        if (tamper) ally["effectivePower"] = 3;
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [],
            new Dictionary<string, RecoveredPlayerView>(), state, currentTick: state.Tick, spectatorReplayFrame: frame);
        if (tamper) Assert.Contains(errors, e => e.Contains("effective power does not match", StringComparison.Ordinal));
        else Assert.Empty(errors);
    }
}
