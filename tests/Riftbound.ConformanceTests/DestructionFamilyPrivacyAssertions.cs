using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class DestructionFamilyPrivacyAssertions
{
    // Only independently checked historical references are removed from the
    // string-value privacy scan. Current private card objects remain forbidden.
    internal static void Check(JsonNode snapshot, MatchState state)
    {
        if (snapshot["stack"] is JsonArray stack)
            foreach (var row in stack.OfType<JsonObject>())
            {
                var item = Assert.Single(state.StackItems, i => i.StackItemId == row["stackItemId"]!.GetValue<string>());
                CheckRow(row, item.DamageDestruction, item.GraveyardPlay, item.EffectKind, item.ControllerId, item.SourceObjectId, state);
            }
        if (snapshot["timing"]?["triggerQueue"] is JsonArray queue)
            foreach (var row in queue.OfType<JsonObject>())
            {
                var item = Assert.Single(state.TriggerQueue, i => i.TriggerId == row["triggerId"]!.GetValue<string>());
                CheckRow(row, item.DamageDestruction, item.GraveyardPlay, item.EffectKind, item.ControllerId, item.SourceObjectId, state);
            }
    }

    private static void CheckRow(JsonObject row, DamageDestructionContext? damage, GraveyardPlayContext? graveyard,
        string effect, string actor, string sourceId, MatchState state)
    {
        if (damage is not null)
        {
            Assert.Equal("CAPTURED_DAMAGE_DESTRUCTION", effect);
            Assert.Contains(damage.CardNo, new[] { "OGN·221/298", "OGN·254/298" });
            Assert.Equal(actor, damage.ControllerId); Assert.Equal(sourceId, damage.Source.ObjectId);
            Assert.Equal(state.TurnNumber, damage.TurnNumber); Assert.NotNull(damage.DamagedUnit);
            Assert.InRange(damage.CreatedAtTick, 1, state.Tick); Assert.InRange(damage.DamagedAtTick!.Value, damage.CreatedAtTick, state.Tick);
            Assert.Equal($"{damage.CreationStackId}:DAMAGE-DESTRUCTION:{damage.ExecutionIndex}", damage.InstanceId);
            var projected = row["damageDestruction"]!.AsObject();
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(damage, new JsonSerializerOptions(JsonSerializerDefaults.Web)), projected));
            Historical(projected["source"]!.AsObject(), damage.Source, state, row);
            Historical(projected["damagedUnit"]!.AsObject(), damage.DamagedUnit!, state);
            if (damage.Unit is { } target) { Assert.Equal(target, damage.DamagedUnit); Historical(projected["unit"]!.AsObject(), target, state); }
        }
        if (graveyard is not null)
        {
            Assert.Equal("CAPTURED_GRAVEYARD_UNIT_PLAY", effect); Assert.Equal("OGN·037/298", graveyard.CardNo);
            Assert.Equal(actor, graveyard.ControllerId); Assert.Equal(actor, graveyard.Attribution.ResponsiblePlayerId);
            Assert.True(graveyard.Attribution.FromSpell); Assert.Equal(sourceId, graveyard.Source.ObjectId);
            Assert.Equal(state.TurnNumber, graveyard.TurnNumber); Assert.InRange(graveyard.CreatedAtTick, 1, state.Tick);
            var projected = row["graveyardPlay"]!.AsObject();
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(graveyard, new JsonSerializerOptions(JsonSerializerDefaults.Web)), projected));
            Historical(projected["source"]!.AsObject(), graveyard.Source, state, row);
            Historical(projected["destroyed"]!.AsObject(), new(graveyard.Destroyed.ObjectId, graveyard.Destroyed.Generation), state);
            Historical(projected["attribution"]!["source"]!.AsObject(), graveyard.Attribution.Source, state);
        }
    }

    private static void Historical(JsonObject projected, ObjectBinding binding, MatchState state, JsonObject? sourceRow = null)
    {
        Assert.Equal(binding.ObjectId, projected["objectId"]!.GetValue<string>());
        Assert.Equal(binding.Generation, projected["generation"]!.GetValue<long>());
        if (!state.CardObjects.TryGetValue(binding.ObjectId, out var current)) return; // Full replay checks ceased token history.
        Assert.InRange(binding.Generation, 0, current.ObjectGeneration);
        if (binding.Generation == current.ObjectGeneration) return;
        projected["objectId"] = "CHECKED_CAPTURED_ABILITY_SOURCE";
        if (sourceRow is not null) sourceRow["sourceObjectId"] = "CHECKED_CAPTURED_ABILITY_SOURCE";
    }
}
