using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class EvelynnFaceDownPlayMovementTests
{
    [Fact]
    public async Task OriginalHistoryRejectsErasedOrCoherentlyForgedSourcePlayConditions()
    {
        var game = await Ready();
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var skill = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<UnitEventContext>(skill.UnitEvent);
        var origin = Assert.IsType<UnitEventSourcePlayCondition>(context.SourcePlayCondition);
        var standby = Assert.IsType<StandbyPlacement>(origin.FromStandby);
        var journal = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.Empty(Errors(game, game.State));
        foreach (var forged in new[] {
            context with { SourcePlayCondition = null },
            context with { SourcePlayCondition = origin with { FromStandby = null } },
            context with { SourcePlayCondition = origin with { TurnPlayerId = "P2" } },
            context with { SourcePlayCondition = origin with {
                FromStandby = standby with { SourceGeneration = standby.SourceGeneration + 1 } } }
        })
        {
            var invalid = game.State with { StackItems = [skill with { UnitEvent = forged }] };
            Assert.False(CoreRuleEngine.ValidUnitEvent(invalid, forged, skill.EffectKind,
                skill.SourceObjectId, skill.CardNo, skill.ControllerId));
            Assert.NotEmpty(Errors(game, invalid));
        }
        foreach (var condition in new[] {
            origin with { TurnNumber = origin.TurnNumber - 1 },
            origin with { FromStandby = standby with {
                Battlefield = standby.Battlefield with { Generation = standby.Battlefield.Generation + 1 } } },
            origin with { PlayingPlayerId = "P2", TurnPlayerId = "P2",
                FromStandby = standby with { ControllerId = "P2" } }
        })
        {
            var forged = context with { SourcePlayCondition = condition };
            var invalid = game.State with { StackItems = [skill with { UnitEvent = forged }] };
            Assert.True(CoreRuleEngine.ValidUnitEvent(invalid, forged, skill.EffectKind,
                skill.SourceObjectId, skill.CardNo, skill.ControllerId));
            Assert.Contains("observed unit event differs from original accepted capture", Errors(game, invalid));
        }
        Assert.Equal(journal, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnAcceptedMovementTriggerCannotDisappearFromItsOriginalHistory()
    {
        var game = await Ready();
        await game.Act("P1", new PlayCardCommand("SHADOW", Evelynn, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var journal = MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() });
        Assert.Contains("pending observed unit events differ from original accepted completion history",
            Errors(game, game.State with { StackItems = [] }));
        Assert.Equal(journal, MatchStateHasher.HashValue(new { commands = game.Commands(), events = game.Events() }));
        await game.ColdCheckpoint();
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
