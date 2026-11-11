using System.Text.Json;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class WorkshopBattlefieldUnitPlayTests
{
    [Theory]
    [InlineData(Valley, false)]
    [InlineData(Spring, false)]
    [InlineData(Spring, true)]
    public async Task NullOriginalPayloadsRejectRecoveryWithPendingSkillsOrConsumedFirstUses(string battlefield, bool declined)
    {
        var game = new Driver(Position(battlefield));
        await Enter(game);
        if (battlefield == Valley) await PayBoon(game);
        else await SelectReturn(game, declined ? [] : ["ALLY"]);

        var state = game.State;
        var commands = game.Commands(); var originalEvents = game.Events();
        var commandJson = JsonSerializer.Serialize(commands); var eventJson = JsonSerializer.Serialize(originalEvents);
        var stateHash = MatchStateHasher.Hash(state);
        var sequence = commands.Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(
            player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, originalEvents, views, state, state.Tick, spectator));

        // Keep the command journal and real game untouched. Corrupt one copied
        // original event of each kind, including this family's queue/target
        // receipts and unrelated payment/play records.
        foreach (var original in originalEvents.DistinctBy(entry => entry.Event.Kind))
        {
            var malformed = originalEvents.Select(entry => entry.Sequence == original.Sequence
                ? entry with { Event = entry.Event with { Payload = null! } } : entry).ToArray();
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, malformed, views, state, state.Tick, spectator);
            Assert.Contains(errors, error => error.Contains("payload is required", StringComparison.Ordinal));
            var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, malformed, views, errors,
                AuthoritativeState: state, SpectatorReplayFrame: spectator) { OpeningSeed = state.Seed };
            Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), game));
        }
        Assert.Equal(commandJson, JsonSerializer.Serialize(game.Commands()));
        Assert.Equal(eventJson, JsonSerializer.Serialize(game.Events()));
        Assert.Equal(stateHash, MatchStateHasher.Hash(game.State));
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.ColdCheckpoint();
    }
}
