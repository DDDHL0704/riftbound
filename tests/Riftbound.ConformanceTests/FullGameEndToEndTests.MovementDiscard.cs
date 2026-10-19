using System.Text.Json.Nodes;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class FullGameEndToEndTests
{
    // Only the captured old field incarnation is normalized. Every current
    // private hand/deck identity remains subject to the existing assertion.
    private static void CheckHistoricalMovementDiscardSources(JsonNode snapshot, MatchState state)
    {
        foreach (var row in MovementDiscardJsonObjects(snapshot).ToArray())
        {
            var stackId = row["stackItemId"]?.GetValue<string>();
            var triggerId = row["triggerId"]?.GetValue<string>();
            var choiceId = row["choiceId"]?.GetValue<string>();
            var stack = state.StackItems.SingleOrDefault(s => s.StackItemId == stackId);
            var trigger = state.TriggerQueue.SingleOrDefault(t => t.TriggerId == triggerId);
            var hand = state.PendingHandChoice is { } pending && pending.ChoiceId == choiceId ? pending : null;
            var movement = stack?.MovementObserver ?? trigger?.MovementObserver ?? hand?.MovementObserver;
            if (movement?.Kind != "UNIT_MOVED_DRAW_DISCARD_TYPES") continue;
            var sourceId = stack?.SourceObjectId ?? trigger?.SourceObjectId ?? hand!.SourceObjectId;
            var controller = stack?.ControllerId ?? trigger?.ControllerId ?? hand!.PlayerId;
            if (!state.ObjectLocations.TryGetValue(sourceId, out var location)
                || location.Zone is not ("HAND" or "MAIN_DECK" or "RUNE_DECK")) continue;
            var source = state.CardObjects[sourceId];
            Assert.Equal("UNL-080/219", movement.CardNo); Assert.Equal(movement.CardNo, source.CardNo);
            Assert.Equal(sourceId, movement.Unit.ObjectId); Assert.Equal(controller, movement.Unit.ControllerId);
            Assert.Equal(movement.SourceGeneration, movement.Unit.Generation);
            Assert.InRange(movement.SourceGeneration, 0, source.ObjectGeneration - 1);
            Assert.Contains(movement.Unit.MovingPlayerId, state.Seats.Keys);
            Assert.Equal(controller, movement.Unit.Origin.PlayerId); Assert.Equal(controller, movement.Unit.Destination.PlayerId);
            Assert.Contains(movement.Unit.Origin.Zone, new[] { "BASE", "BATTLEFIELD" });
            Assert.Contains(movement.Unit.Destination.Zone, new[] { "BASE", "BATTLEFIELD" });
            Assert.NotEqual(movement.Unit.Origin, movement.Unit.Destination);
            Assert.Null(movement.MovementBatch); Assert.Null(movement.SourceLocation); Assert.Null(movement.MovementOrdinal);
            var effect = stack?.EffectKind ?? trigger?.EffectKind ?? "CAPTURED_UNIT_MOVEMENT_OBSERVER";
            if (hand is not null)
            {
                Assert.Equal(movement.Kind, hand.EffectKind); Assert.Equal(movement.Kind, hand.ChoiceWindow);
                Assert.Equal(movement.Kind, hand.Reason); Assert.Equal(0, hand.DrawCount);
                Assert.Equal(1, hand.RequiredCount); Assert.Equal(1, hand.MaxCount);
                Assert.Equal(state.PlayerZones[controller].Hand.Order(), hand.LegalObjectIds.Order());
                Assert.Null(movement.DiscardInline);
            }
            if (movement.DiscardInline is { } inline)
            {
                Assert.Equal("MOVEMENT_DISCARD_TYPE_INLINE", effect);
                var expectedTypes = inline.CardNo switch { "VEN·058" => 3, "OGN·083/298" => 4,
                    "OGN·072/298" => 2, "OGN·096/298" => 1, _ => throw new InvalidOperationException("Unreviewed discarded printing in historical Hwei disclosure.") };
                Assert.Equal(expectedTypes, inline.Types); Assert.Contains(inline.BranchType, new[] { 1, 2, 4 });
                Assert.NotEqual(0, expectedTypes & inline.BranchType); Assert.InRange(inline.CreatedAtTick, 1, state.Tick);
                var discarded = state.CardObjects[inline.DiscardedCard.ObjectId];
                Assert.Equal(inline.CardNo, discarded.CardNo);
                Assert.InRange(inline.DiscardedCard.Generation, 0, discarded.ObjectGeneration - 1);
            }
            else Assert.Equal("CAPTURED_UNIT_MOVEMENT_OBSERVER", effect);
            if (row["movementObserver"] is JsonObject projected)
            {
                Assert.Equal(movement.CardNo, projected["cardNo"]!.GetValue<string>());
                Assert.Equal(movement.SourceGeneration, projected["sourceGeneration"]!.GetValue<long>());
                Assert.Equal(sourceId, projected["unit"]!["objectId"]!.GetValue<string>());
                Assert.Equal(movement.Unit.Generation, projected["unit"]!["generation"]!.GetValue<long>());
                projected["unit"]!["objectId"] = "CHECKED_CAPTURED_ABILITY_SOURCE";
            }
            if (row["sourceObjectId"]?.GetValue<string>() == sourceId)
                row["sourceObjectId"] = "CHECKED_CAPTURED_ABILITY_SOURCE";
        }
    }

    private static IEnumerable<JsonObject> MovementDiscardJsonObjects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            yield return obj;
            foreach (var property in obj)
                foreach (var child in MovementDiscardJsonObjects(property.Value)) yield return child;
        }
        else if (node is JsonArray array)
            foreach (var value in array)
                foreach (var child in MovementDiscardJsonObjects(value)) yield return child;
    }
}
