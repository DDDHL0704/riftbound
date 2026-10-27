using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheTwoActuallyPaidEchoModesKeepOppositeInversesInThePlayersChosenResolutionOrder(bool empowerLast)
    {
        var initial = Add(Add(Position("P1"), "GATE", "SFD·078/221", zone: "BASE"), "ORDER-TARGET", UnitNo, zone: "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ORDER-TARGET"] = initial.CardObjects["ORDER-TARGET"] with { IsEmpowered = true } } };
        var game = new Driver(initial);
        try
        {
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new ActivateAbilityCommand("GATE", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, [])); await game.Top();
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-2")); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-3"));
            var mana = game.State.RunePools["P1"].Mana;
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["ORDER-TARGET"], Mode: Remove,
                OptionalCosts: ["ECHO"], RepeatChoices: [new(Empower, ["ORDER-TARGET"])]));
            Assert.Equal(mana - 6, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Top(); Assert.True(game.State.CardObjects["ORDER-TARGET"].IsEmpowered);
            Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "DELAYED_EMPOWER_SCHEDULED"));
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(2, game.State.TriggerQueue.Count);
            var empower = Assert.Single(game.State.TriggerQueue, t => t.DelayedEmpower?.CreatingEffectKind == "TEMPORARY_REMOVE_EMPOWER_UNIT");
            var remove = Assert.Single(game.State.TriggerQueue, t => t.DelayedEmpower?.CreatingEffectKind == "TEMPORARY_EMPOWER_UNIT");
            Assert.Equal(0, empower.DelayedEmpower!.ExecutionIndex); Assert.Equal(1, remove.DelayedEmpower!.ExecutionIndex);
            var order = empowerLast ? new[] { remove.TriggerId, empower.TriggerId } : new[] { empower.TriggerId, remove.TriggerId };
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
            Assert.Equal("ordered-" + order[0], game.State.StackItems.Last().StackItemId);
            await game.Top(); Assert.Equal(!empowerLast, game.State.CardObjects["ORDER-TARGET"].IsEmpowered);
            await game.Top(); Assert.Equal(empowerLast, game.State.CardObjects["ORDER-TARGET"].IsEmpowered);
            Assert.Equal("P2", game.State.TurnPlayerId); await game.Replay(); Restore(game.State);
        }
        finally { await Save("paid-echo-opposite-inverse-order-empower-last-" + empowerLast, initial, game); }
    }

    [Fact]
    public async Task AnActuallyDrawnPrivateCreatingSpellStaysHiddenWhileItsInverseWaitsInTheRealOrderingQueue()
    {
        var initial = Add(Add(Add(Position("P1"), "QUEUE-TARGET", UnitNo, zone: "BASE"), "SECOND-SPELL", No), "DRAW", "OGN·048/298");
        var moved = initial.PlayerZones["P1"].MainDeck.Concat(initial.PlayerZones["P1"].Graveyard).ToArray();
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                MainDeck = [], Graveyard = [], Hand = [..initial.PlayerZones["P1"].Hand, ..moved] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => moved.Contains(p.Key) ? new ObjectLocationState("P1", "HAND") : p.Value) };
        var game = new Driver(initial);
        try
        {
            await TapRunes(game); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, ["QUEUE-TARGET"], Mode: Empower)); await game.Top();
            await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", [])); await game.Top();
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand); Assert.Equal(1, game.State.PlayerScores["P2"]);
            await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-2"));
            await game.Act("P1", new PlayCardCommand("SECOND-SPELL", No, ["QUEUE-TARGET"], Mode: Empower)); await game.Top();
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(2, game.State.TriggerQueue.Count);
            var hidden = Assert.Single(game.State.TriggerQueue, t => t.SourceObjectId == "SPELL");
            Assert.True(game.State.CardObjects["SPELL"].ObjectGeneration > hidden.DelayedEmpower!.Source.Generation);
            var sequence = game.Events().Last().Sequence; var state = game.State;
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
            var queue = JsonSerializer.Deserialize<Dictionary<string, object?>[]>(JsonSerializer.Serialize(spectator.SpectatorSnapshot.Timing["triggerQueue"], Json), Json)!;
            var row = Assert.Single(queue, q => Assert.IsType<JsonElement>(q["triggerId"]).GetString() == hidden.TriggerId);
            Assert.Equal("HIDDEN", Assert.IsType<JsonElement>(row["sourceObjectId"]).GetString()); Assert.False(row.ContainsKey("delayedEmpower"));
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
            row["effectKind"] = "FORGED_PUBLIC_EFFECT";
            var wrongTiming = new Dictionary<string, object?>(spectator.SpectatorSnapshot.Timing) { ["triggerQueue"] = queue };
            var wrongEffect = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Timing = wrongTiming } };
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, wrongEffect), e => e.Contains("hidden effect kind", StringComparison.Ordinal));
            row["effectKind"] = hidden.EffectKind;
            row["sourceObjectId"] = "SPELL"; row["sourceVisibility"] = "VISIBLE";
            var timing = new Dictionary<string, object?>(spectator.SpectatorSnapshot.Timing) { ["triggerQueue"] = queue };
            var leaky = spectator with { SpectatorSnapshot = spectator.SpectatorSnapshot with { Timing = timing } };
            Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator));
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, leaky), e => e.Contains("source", StringComparison.Ordinal));
            await FinishEnd(game, "P1"); Assert.False(game.State.CardObjects["QUEUE-TARGET"].IsEmpowered);
            Assert.Contains("SPELL", game.State.PlayerZones["P1"].Hand); await game.Replay(); Restore(game.State);
        }
        finally { await Save("delayed-real-private-source-ordering-queue", initial, game); }
    }
}
