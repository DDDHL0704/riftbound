using System.Text.Json;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SameBattlefieldKeywordAuraRecoveryTests
{
    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "BANNER", "RAD·002/167", "P1", "BATTLEFIELD", "BF");
        return RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
    }

    [Fact]
    public void GenuineKeywordAuraMetadataIsAcceptedForPlayersAndSpectators()
    {
        var state = Position();
        Assert.Contains(state.ContinuousEffects, e => e.SourceObjectId == "BANNER" && e.TargetObjectId == "ALLY"
            && e.Layer == "RULE_TEXT" && e.SourceCardNo == "RAD·002/167");
        Assert.Empty(Errors(state));
    }

    [Theory]
    [InlineData("sourceCardNo", "OGN·219/298")]
    [InlineData("sourceCardNo", "RAD·044/167")]
    [InlineData("effectKind", "ARBITRARY_RUNTIME_KIND")]
    [InlineData("sourcePath", "Arbitrary.Runtime.Path")]
    [InlineData("effectId", "RULE_TEXT:FORGED")]
    [InlineData("duration", "UNTIL_END_OF_TURN")]
    [InlineData("sourceObjectId", "ENEMY")]
    [InlineData("source-binding", "ENEMY")]
    [InlineData("sourceCardNo", null)]
    [InlineData("effect", null)]
    public void ForgedAuraMetadataIsRejectedForPlayersAndSpectators(string field, string? value)
    {
        var errors = Errors(Position(), field, value);
        Assert.Contains(errors, e => e.StartsWith("snapshot for P1 ", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("spectator replay frame ", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> Errors(MatchState state, string? field = null, string? value = null)
    {
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, 0, snapshots[p], state.Tick, 0, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        if (field is not null)
        {
            IReadOnlyDictionary<string, object?> Change(IReadOnlyDictionary<string, object?> timing)
            {
                var effects = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(JsonSerializer.Serialize(timing["continuousEffects"]))!;
                var aura = Assert.Single(effects, e => e.TryGetValue("sourceCardNo", out var no) && no?.ToString() == "RAD·002/167");
                if (field == "effect") effects.Remove(aura);
                else if (field == "source-binding")
                {
                    aura["sourceObjectId"] = value;
                    aura["effectId"] = aura["effectId"]!.ToString()!.Replace("BANNER", value, StringComparison.Ordinal);
                }
                else aura[field] = value;
                return new Dictionary<string, object?>(timing) { ["continuousEffects"] = effects };
            }
            views["P1"] = views["P1"] with { Snapshot = views["P1"].Snapshot with { Timing = Change(views["P1"].Snapshot.Timing) } };
            spectator = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Timing = Change(spectator.SpectatorSnapshot.Timing) } };
        }
        return MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator);
    }
}
