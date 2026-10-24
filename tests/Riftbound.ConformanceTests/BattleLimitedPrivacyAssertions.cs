using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Bounded proof for the public source of an already-earned self skill. Only
// this one old-generation reference is normalized on the guard's local clone.
internal static class BattleLimitedPrivacyAssertions
{
    internal static void Check(JsonNode snapshot, MatchState state, string viewerId)
    {
        if (snapshot["stack"] is not JsonArray rows) return;
        foreach (var row in rows.OfType<JsonObject>()) {
            var stackId = row["stackItemId"]?.GetValue<string>();
            var item = state.StackItems.SingleOrDefault(i => i.StackItemId == stackId);
            if (item?.FieldContext is not { Kind: "BATTLE_DOUBLE_SELF_ATTACK" or "BATTLE_DOUBLE_SELF_DEFEND" } context
                || !state.CardObjects.TryGetValue(item.SourceObjectId, out var current)
                || current.ObjectGeneration <= context.SourceGeneration) continue;
            var privateOwners = state.PlayerZones.Where(p => p.Value.Hand.Concat(p.Value.MainDeck).Concat(p.Value.RuneDeck)
                .Contains(item.SourceObjectId)).Select(p => p.Key).ToArray();
            if (privateOwners.Length == 0 || privateOwners.Contains(viewerId)) continue;
            var privateOwner = Assert.Single(privateOwners);
            Assert.Contains(context.CardNo, new[] { "SFD·110/221", "SFD·110a/221" });
            Assert.Equal("FIELD_TARGETED_TRIGGER", item.EffectKind); Assert.Equal(context.CardNo, item.CardNo);
            Assert.True(context.SourceGeneration >= 0 && context.SourceGeneration < current.ObjectGeneration);
            Assert.Null(context.SourceTokenFactoryCardNo); Assert.False(context.SourceCeasedToExist);
            Assert.Null(context.RecycledBoon); Assert.Null(context.AffectedUnit); Assert.False(context.AffectedUnitCeasedToExist);
            Assert.Contains(context.ReturnFocusPlayerId, state.Seats.Keys); Assert.Empty(item.TargetObjectIds);
            Assert.Null(item.TargetGenerations); Assert.Empty(item.OptionalCosts); Assert.Equal(0, item.DamageAmount);
            Assert.Equal(1, item.EffectRepeatCount); Assert.Equal("", item.Destination);
            Assert.Contains(CardObjectTags.UnitCard, current.Tags);
            var roleName = context.Kind == "BATTLE_DOUBLE_SELF_ATTACK" ? "ATTACKER" : "DEFENDER";
            var first = Assert.Single(state.UntilEndOfTurnEffects.Where(t => t.StartsWith("BATTLE_ROLE_FIRST:", StringComparison.Ordinal))
                .Select(t => JsonNode.Parse(t["BATTLE_ROLE_FIRST:".Length..])!.AsObject()),
                r => r["sourceObjectId"]?.GetValue<string>() == item.SourceObjectId
                    && r["sourceGeneration"]?.GetValue<long>() == context.SourceGeneration && r["role"]?.GetValue<string>() == roleName);
            Keys(first, "scope", "sourceObjectId", "sourceGeneration", "controllerId", "role", "sourceFace", "faceRevision",
                "acquiredAtTick", "roleEventOrder", "oneOnOne", "tokenFactoryCardNo");
            Assert.Equal(item.ControllerId, first["controllerId"]!.GetValue<string>());
            Assert.Equal(context.CardNo, first["sourceFace"]!.GetValue<string>()); Assert.True(first["oneOnOne"]!.GetValue<bool>());
            Assert.Null(first["tokenFactoryCardNo"]); Assert.True(first["faceRevision"]!.GetValue<long>() >= 0);
            var scope = first["scope"]!.AsObject(); Keys(scope, "battlefieldId", "startedAtTick", "roleEventOrder", "attackingPlayerId", "defendingPlayerId");
            Assert.Equal(context.BattlefieldId, scope["battlefieldId"]!.GetValue<string>());
            Assert.Equal(item.ControllerId, scope[roleName == "ATTACKER" ? "attackingPlayerId" : "defendingPlayerId"]!.GetValue<string>());
            Assert.InRange(scope["startedAtTick"]!.GetValue<long>(), 0, state.Tick);
            Assert.InRange(first["acquiredAtTick"]!.GetValue<long>(), scope["startedAtTick"]!.GetValue<long>(), state.Tick);
            Assert.True(scope["roleEventOrder"]!.GetValue<int>() >= 0 && first["roleEventOrder"]!.GetValue<int>() >= 0);
            Assert.Single(state.UntilEndOfTurnEffects.Where(t => t.StartsWith("BATTLE_LIMITED_SCOPE:", StringComparison.Ordinal))
                .Select(t => JsonNode.Parse(t["BATTLE_LIMITED_SCOPE:".Length..])), s => JsonNode.DeepEquals(scope, s));
            Assert.Equal("BATTLEFIELD", context.SourceLocation!.Zone); Assert.Equal(context.BattlefieldId, context.SourceLocation.BattlefieldObjectId);
            Assert.Equal(item.ControllerId, context.SourceLocation.PlayerId);
            if (context.Origin is { } origin) {
                Assert.Equal(context.CardNo, origin.CardNo); Assert.Equal(item.SourceObjectId, origin.Unit.ObjectId);
                Assert.Equal(context.SourceGeneration, origin.Unit.Generation); Assert.True(origin.Revision > 0);
                Assert.InRange(origin.CapturedTick!.Value, 0, first["acquiredAtTick"]!.GetValue<long>());
                Assert.InRange(origin.FaceRevision!.Value, 0, first["faceRevision"]!.GetValue<long>());
                Assert.True(state.CardObjects.TryGetValue(origin.Equipment.ObjectId, out var gear));
                Assert.Contains(gear.CardNo, new[] { "SFD·059/221", "VEN·137" });
                Assert.InRange(origin.Equipment.Generation, 0, gear.ObjectGeneration);
                if (origin.Equipment.Generation == gear.ObjectGeneration) Assert.True(origin.Revision <= gear.AttachmentRevision);
            }
            else Assert.Contains(current.CardNo, new[] { "SFD·110/221", "SFD·110a/221" });
            // Exact existing public row. No current private object, source stat,
            // extra context or unknown field inherits the historical allowance.
            Keys(row, "stackItemId", "controllerId", "sourceObjectId", "sourceVisibility", "effectKind", "cardNo", "targetObjectIds", "damageAmount");
            Assert.Equal(item.StackItemId, row["stackItemId"]!.GetValue<string>()); Assert.Equal(item.ControllerId, row["controllerId"]!.GetValue<string>());
            Assert.Equal(item.SourceObjectId, row["sourceObjectId"]!.GetValue<string>()); Assert.Equal("VISIBLE", row["sourceVisibility"]!.GetValue<string>());
            Assert.Equal(item.EffectKind, row["effectKind"]!.GetValue<string>()); Assert.Equal(item.CardNo, row["cardNo"]!.GetValue<string>());
            Assert.Empty(row["targetObjectIds"]!.AsArray()); Assert.Equal(0, row["damageAmount"]!.GetValue<int>());
            Assert.False(snapshot["players"]![privateOwner]!["objects"]!.AsObject().ContainsKey(item.SourceObjectId));
            row["sourceObjectId"] = "CHECKED_CAPTURED_ABILITY_SOURCE";
        }
    }
    private static void Keys(JsonObject value, params string[] expected)
        => Assert.True(value.Select(p => p.Key).ToHashSet(StringComparer.Ordinal).SetEquals(expected), "Unexpected or missing historical C11 source fields.");
}
