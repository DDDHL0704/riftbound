using System.Text.Json;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class LastBreathBattlefieldDamageBoundaryTests
{
    private static void AssertAreaBirthBindings(Driver game)
    {
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(game.State));
        var copied = game.State.TriggerQueue.First(t => t.LastBreathBattlefieldDamage?.Origin is not null);
        var c = copied.LastBreathBattlefieldDamage!;
        foreach (var invalid in new[] {
            c with { Source = null! }, c with { Source = c.Source with { ObjectId = null! } },
            c with { Source = c.Source with { Generation = game.State.CardObjects["UNIT"].ObjectGeneration } },
            c with { SourceCardNo = "UNKNOWN" }, c with { SourceTokenFactoryCardNo = "UNKNOWN" }, c with { CardNo = null! },
            c with { ControllerId = "UNSEATED" }, c with { InstanceId = null! }, c with { TriggerIndex = -1 },
            c with { Kind = "UNRELATED_EFFECT" }, c with { CreatedAtTick = game.State.Tick + 1 },
            c with { TurnNumber = game.State.TurnNumber + 1 }, c with { SourceZone = "BASE", BattlefieldObjectId = null },
            c with { BattlefieldObjectId = "OTHER" }, c with { Origin = null },
            c with { Origin = c.Origin! with { Equipment = null! } },
            c with { Origin = c.Origin! with { Unit = c.Origin.Unit with { ObjectId = null! } } }
        })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = game.State.TriggerQueue
                .Select(t => t == copied ? t with { LastBreathBattlefieldDamage = invalid } : t).ToArray() }),
                e => e.Contains("captured last-breath battlefield damage", StringComparison.Ordinal));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, copied] }),
            e => e.Contains("duplicate captured last-breath battlefield damage", StringComparison.Ordinal));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = game.State.TriggerQueue
            .Select(t => t == copied ? t with { LastBreathBattlefieldDamage = null } : t).ToArray() }),
                e => e.Contains("captured last-breath battlefield damage", StringComparison.Ordinal));

        var commands = game.Commands(); var events = game.Events(); var sequence = commands[^1].CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
        var views = game.State.Seats.Keys.ToDictionary(p => p,
            p => new RecoveredPlayerView(p, game.State.Tick, sequence, snapshots[p], game.State.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, sequence, [], game.State);
        var queue = JsonSerializer.SerializeToElement(snapshots["P2"].Timing["triggerQueue"], Json).EnumerateArray()
            .Select(item => JsonSerializer.Deserialize<Dictionary<string, object?>>(item.GetRawText(), Json)!).ToArray();
        queue[0]["lastBreathBattlefieldDamage"] = c;
        var view = views["P2"]; views["P2"] = view with { Snapshot = view.Snapshot with {
            Timing = new Dictionary<string, object?>(view.Snapshot.Timing) { ["triggerQueue"] = queue } } };
        Assert.Contains(MatchRecoveryValidator.Validate(game.State.RoomId, sequence, commands, events, views, game.State, game.State.Tick, spectator),
            e => e.Contains("private birth receipt", StringComparison.Ordinal));
    }
}
