using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class NamiHeldNextUnitTests
{
    [Fact]
    public async Task OriginalAcceptedHistoryRejectsDeletingOrCoherentlyReplacingAnUnusedGrant()
    {
        var game = new Driver(Position()); await EarnHold(game); await game.Top();
        var grant = Assert.Single(CoreRuleEngine.UnitEventTurnPlayGrants(game.State));
        Assert.Empty(Errors(game, game.State));
        var journal = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        var deleted = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Where(e => e != grant.Marker).ToArray() };
        Assert.True(CoreRuleEngine.ValidCurrentTurnUnitPlayGrants(deleted));
        Assert.Contains("unit event turn play grants differ from original accepted history", Errors(game, deleted));
        foreach (var replacement in new[] {
            RebindGrant(grant with { StackItemId = grant.StackItemId + "-FORGED" }),
            RebindGrant(grant with { ControllerId = "P2" })
        })
        {
            var forged = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects
                .Select(e => e == grant.Marker ? replacement.Marker : e).ToArray() };
            Assert.True(CoreRuleEngine.ValidTurnUnitPlayGrant(forged, replacement));
            Assert.Contains("unit event turn play grants differ from original accepted history", Errors(game, forged));
        }
        Assert.Equal(journal, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task ConsumedUsesAndEarnedSkillsCannotBeSilentlyDeletedRefundedOrResurrected()
    {
        var game = new Driver(Position()); await EarnHold(game); await game.Top(); await Fund(game);
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        var skill = Assert.Single(game.State.StackItems);
        var journal = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.Contains("unit event turn play skills differ from original accepted history", Errors(game,
            game.State with { StackItems = [] }));
        var refunded = game.State with { StackItems = [], UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects
            .Where(e => !e.StartsWith(CoreRuleEngine.TurnUnitPlayGrantUsePrefix, StringComparison.Ordinal)).ToArray() };
        Assert.True(CoreRuleEngine.ValidCurrentTurnUnitPlayGrants(refunded));
        Assert.Contains("unit event turn play uses differ from original accepted history", Errors(game, refunded));
        Assert.Equal(journal, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.Top();
        Assert.True(CoreRuleEngine.ValidTurnUnitPlayBoon(game.State, skill.TurnUnitPlayBoon,
            skill.EffectKind, skill.SourceObjectId, skill.ControllerId, skill.CardNo));
        Assert.Contains("unit event turn play skills differ from original accepted history",
            Errors(game, game.State with { StackItems = [skill] }));
        await game.ColdCheckpoint();
    }

    private static TurnUnitPlayGrant RebindGrant(TurnUnitPlayGrant grant)
    {
        string Enc(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        var marker = string.Join('|', "TURN_UNIT_PLAY_GRANT", grant.TurnNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), Enc(grant.ControllerId),
            Enc(grant.Source.ObjectId), grant.Source.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture), Enc(grant.CardNo), Enc(grant.StackItemId),
            grant.ExecutionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), grant.Kind, Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(grant.UnitEventOrigin)));
        return grant with { Marker = marker };
    }

    private static IReadOnlyList<string> Errors(Driver game, MatchState state)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, options), options)!;
        var commands = JsonSerializer.Deserialize<RecoveredCommand[]>(JsonSerializer.Serialize(game.Commands(), options), options)!;
        var events = JsonSerializer.Deserialize<RecoveredEvent[]>(JsonSerializer.Serialize(game.Events(), options), options)!;
        var sequence = events.Length == 0 ? 0 : events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
    }
}
