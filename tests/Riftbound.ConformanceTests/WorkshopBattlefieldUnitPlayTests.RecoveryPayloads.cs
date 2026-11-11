using System.Text.Json;
using Riftbound.Contracts;
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

    private static IReadOnlyList<string> CorruptBattlefieldHistoryEffects(MatchState state, string malformed)
        => malformed switch {
            "list" => null!, "first" => [null!, ..state.UntilEndOfTurnEffects],
            _ => [..state.UntilEndOfTurnEffects, null!] };

    private static void RejectMalformedBattlefieldHistoryEffects(Driver game, MatchState forged)
    {
        var original = game.State;
        var stateHash = MatchStateHasher.Hash(original);
        var commands = JsonSerializer.Serialize(game.Commands()); var events = JsonSerializer.Serialize(game.Events());
        var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(original); var prompts = ResolutionResult.BuildPrompts(original);
        var views = original.Seats.Keys.ToDictionary(player => player, player => new RecoveredPlayerView(
            player, original.Tick, sequence, snapshots[player], original.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(original.RoomId, original.Tick, sequence, [], original);
        var errors = MatchRecoveryValidator.Validate(original.RoomId, sequence, game.Commands(), game.Events(), views,
            forged, original.Tick, spectator);
        Assert.Contains("battlefield unit-play history contains a null effect list or value", errors);
        var frame = new MatchRecoveryFrame(original.RoomId, original.Tick, sequence, game.Commands(), game.Events(), views,
            errors, AuthoritativeState: forged, SpectatorReplayFrame: spectator) { OpeningSeed = original.Seed };
        Assert.Throws<MatchSessionException>(() => MatchSession.Restore(frame, new CoreRuleEngine(), game));
        Assert.Equal(stateHash, MatchStateHasher.Hash(game.State));
        Assert.Equal(commands, JsonSerializer.Serialize(game.Commands())); Assert.Equal(events, JsonSerializer.Serialize(game.Events()));
    }

    [Theory]
    [InlineData(Valley, false, "list")] [InlineData(Valley, false, "first")] [InlineData(Valley, false, "last")]
    [InlineData(Spring, false, "list")] [InlineData(Spring, false, "first")] [InlineData(Spring, false, "last")]
    [InlineData(Spring, true, "list")] [InlineData(Spring, true, "first")] [InlineData(Spring, true, "last")]
    public async Task NullEffectListsOrValuesRejectPendingAndConsumedBattlefieldHistoryWithoutThrowing(
        string battlefield, bool declined, string malformed)
    {
        var game = new Driver(Position(battlefield)); await Enter(game);
        if (battlefield == Valley) await PayBoon(game);
        else await SelectReturn(game, declined ? [] : ["ALLY"]);
        RejectMalformedBattlefieldHistoryEffects(game, game.State with {
            UntilEndOfTurnEffects = CorruptBattlefieldHistoryEffects(game.State, malformed) });
        if (game.State.StackItems.Count > 0) await game.Top();
        await game.ColdCheckpoint();
    }

    [Theory, InlineData("list"), InlineData("first"), InlineData("last")]
    public async Task NullEffectsInsideAnActualTokenChoiceOriginCannotBypassBattlefieldHistoryOrThrow(string malformed)
    {
        var state = RadGearTestPosition.Put(Position(Spring), "OTHER-ALLY", Plain, "P1", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "BIRDS", "UNL-044/219", "P1", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(40, 10, new Dictionary<string, int> { ["green"] = 2 }) } };
        var game = new Driver(state); await Enter(game); await SelectReturn(game, ["ALLY"]);
        await game.Act("P1", new PlayCardCommand("BIRDS", "UNL-044/219", [], Mode: "CREATE_WARHAWKS"));
        await game.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        RejectMalformedBattlefieldHistoryEffects(game, game.State with { PendingRuleChoice = pending with {
            Origin = pending.Origin with { UntilEndOfTurnEffects = CorruptBattlefieldHistoryEffects(pending.Origin, malformed) } } });
        for (var guard = 0; game.State.PendingRuleChoice is { } choice; guard++)
        {
            Assert.True(guard < 4); Assert.Equal(CoreRuleEngine.UnitTokenDestinationChoiceKind, choice.Request.Kind);
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        }
        await game.Top(); await game.ColdCheckpoint();
    }
}
