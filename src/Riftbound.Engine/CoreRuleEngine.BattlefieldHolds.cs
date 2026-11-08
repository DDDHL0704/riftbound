using System.Text.Json.Serialization;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnBattlefieldHolds(int TurnNumber, IReadOnlyList<BattlefieldHoldRecord> Entries);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldHoldRecord(string PlayerId, ObjectBinding Battlefield);

public sealed partial record MatchState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TurnBattlefieldHolds? BattlefieldHolds { get; init; }
}

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult RecordBattlefieldScoringActions(ResolutionResult result)
        => RecordBattlefieldHolds(RecordBattlefieldConquests(result));

    private static ResolutionResult RecordBattlefieldHolds(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State;
        var entries = state.BattlefieldHolds?.TurnNumber == state.TurnNumber
            ? state.BattlefieldHolds.Entries.ToHashSet() : [];
        var events = result.Events.ToArray();
        for (var index = 0; index < events.Length; index++)
        {
            var e = events[index];
            if (e.Kind != "BATTLEFIELD_HELD") continue;
            if (TryGetPayloadInt(e, "holdTurnNumber", out var capturedTurn) && capturedTurn != state.TurnNumber) continue;
            var player = EventPayloadString(e, "playerId");
            var field = EventPayloadString(e, "battlefieldObjectId") ?? EventPayloadString(e, "battlefieldId");
            if (player is null || !state.Seats.ContainsKey(player)
                || BattlefieldLocalRules.Battlefield(state, field) is not { } card) continue;
            // SFD judge FAQ 2.2 distinguishes a prevented scoring action from
            // merely prevented points: Tianna still permits the hold and discount.
            if (TryBuildBattlefieldScorePreventedEvent(state, player, "BATTLEFIELD_HELD_SCORE", [card.ObjectId], out _)) continue;
            entries.Add(new(player, new(card.ObjectId, card.ObjectGeneration)));
            events[index] = e with { Payload = new Dictionary<string, object?>(e.Payload) {
                ["holdTurnNumber"] = state.TurnNumber, ["heldBattlefieldGeneration"] = card.ObjectGeneration } };
        }
        if (state.BattlefieldHolds is null && entries.Count == 0
            || state.BattlefieldHolds is { } previous && previous.TurnNumber == state.TurnNumber
                && entries.SetEquals(previous.Entries)) return result with { Events = events };
        var updated = state with { BattlefieldHolds = entries.Count == 0 ? null : new(state.TurnNumber,
            entries.OrderBy(e => e.PlayerId, StringComparer.Ordinal).ThenBy(e => e.Battlefield.ObjectId, StringComparer.Ordinal)
                .ThenBy(e => e.Battlefield.Generation).ToArray()) };
        return result with { State = updated, Events = events, Snapshots = ResolutionResult.BuildSnapshots(updated),
            Prompts = BuildCorePrompts(updated) };
    }
}
