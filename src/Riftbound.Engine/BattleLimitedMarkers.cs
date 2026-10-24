using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Riftbound.Engine;

// A command-local role-event coordinate identifies a battle.
// These records live inside private marker strings, not new public DTO fields.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BattleLimitedScope(
    [property: JsonRequired] string BattlefieldId,
    [property: JsonRequired] long StartedAtTick,
    [property: JsonRequired] int RoleEventOrder,
    [property: JsonRequired] string AttackingPlayerId,
    [property: JsonRequired] string DefendingPlayerId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BattleFirstRole(
    [property: JsonRequired] BattleLimitedScope Scope,
    [property: JsonRequired] string SourceObjectId,
    [property: JsonRequired] long SourceGeneration,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] string Role,
    [property: JsonRequired] string SourceFace,
    [property: JsonRequired] long FaceRevision,
    [property: JsonRequired] long AcquiredAtTick,
    [property: JsonRequired] int RoleEventOrder,
    [property: JsonRequired] bool OneOnOne,
    [property: JsonRequired] string? TokenFactoryCardNo);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BattleLimitedGrant(
    [property: JsonRequired] BattleLimitedScope Scope,
    [property: JsonRequired] string SourceObjectId,
    [property: JsonRequired] long SourceGeneration,
    [property: JsonRequired] string ControllerId,
    [property: JsonRequired] FieldTriggerContext Context,
    [property: JsonRequired] string StackItemId,
    [property: JsonRequired] string TargetObjectId,
    [property: JsonRequired] long TargetGeneration,
    [property: JsonRequired] string? KeywordEffectId,
    [property: JsonRequired] string? PowerEffectId,
    [property: JsonRequired] int? SnapshotMight);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BattlePowerIdentity(
    [property: JsonRequired] BattleLimitedScope Scope,
    [property: JsonRequired] string StackItemId,
    [property: JsonRequired] string TargetObjectId,
    [property: JsonRequired] long TargetGeneration,
    [property: JsonRequired] int AppliedOrder);

internal static class BattleLimitedMarkers
{
    internal const string ScopePrefix = "BATTLE_LIMITED_SCOPE:";
    internal const string RolePrefix = "BATTLE_ROLE_FIRST:";
    internal const string GrantPrefix = "BATTLE_LIMITED_GRANT:";
    internal const string PowerPrefix = "BATTLE_LIMITED_POWER:";
    internal const string Duration = "UNTIL_END_OF_BATTLE";
    internal const string DoubleAttack = "BATTLE_DOUBLE_SELF_ATTACK";
    internal const string DoubleDefend = "BATTLE_DOUBLE_SELF_DEFEND";
    internal const string SteadfastDefend = "BATTLE_STEADFAST_DEFEND";
    internal const string SourcePath = "CoreRuleEngine.ResolveBattleLimitedRoleSkill";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static bool Kind(string value) => value is DoubleAttack or DoubleDefend or SteadfastDefend;
    internal static string Role(string kind) => kind == DoubleAttack ? "ATTACKER" : "DEFENDER";
    internal static bool Definition(string cardNo, string kind) => kind switch {
        DoubleAttack or DoubleDefend => cardNo is "SFD·110/221" or "SFD·110a/221",
        SteadfastDefend => BattlefieldTriggerSpecRules.TryGetTrigger(cardNo,
            BattlefieldTriggerSpecRules.IsBattlefieldDefendGrantSteadfastTrigger, out _), _ => false };
    internal static string ScopeId(BattleLimitedScope scope) => ScopePrefix + JsonSerializer.Serialize(scope, Json);
    internal static string RoleId(BattleFirstRole role) => RolePrefix + JsonSerializer.Serialize(role, Json);
    internal static string GrantId(BattleLimitedGrant grant) => GrantPrefix + JsonSerializer.Serialize(grant, Json);
    internal static string PowerId(BattlePowerIdentity power) => PowerPrefix
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(power, Json)))).ToLowerInvariant();

    internal static bool TryScope(string value, out BattleLimitedScope? scope) => Read(value, ScopePrefix, out scope);
    internal static bool TryRole(string value, out BattleFirstRole? role) => Read(value, RolePrefix, out role);
    internal static bool PrivateMarker(string value) => value.StartsWith(ScopePrefix, StringComparison.Ordinal)
        || value.StartsWith(RolePrefix, StringComparison.Ordinal) || value.StartsWith(GrantPrefix, StringComparison.Ordinal);
    internal static bool TryGrant(string value, out BattleLimitedGrant? grant)
    {
        if (!Read(value, GrantPrefix, out grant)) return false;
        // Existing FieldTriggerContext and RuleTextInstanceBinding do not mark
        // every constructor field required, so private markers enforce presence.
        using var doc = JsonDocument.Parse(value[GrantPrefix.Length..]);
        var context = doc.RootElement.GetProperty("context");
        if (!Has(context, "cardNo", "kind", "sourceGeneration", "battlefieldId", "returnFocusPlayerId")) return false;
        if (context.TryGetProperty("origin", out var origin) && origin.ValueKind != JsonValueKind.Null) {
            if (!Has(origin, "cardNo", "unit", "equipment", "revision", "capturedTick", "faceRevision")) return false;
            foreach (var key in new[] { "unit", "equipment" })
                if (!Has(origin.GetProperty(key), "objectId", "generation")) return false;
        }
        return true;
    }

    private static bool Has(JsonElement value, params string[] fields)
        => value.ValueKind == JsonValueKind.Object && fields.All(field => value.TryGetProperty(field, out _));
    private static bool UniqueKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in value.EnumerateObject())
                if (!keys.Add(field.Name) || !UniqueKeys(field.Value)) return false;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) if (!UniqueKeys(item)) return false;
        return true;
    }
    private static bool Read<T>(string? value, string prefix, out T? result) where T : class
    {
        result = null;
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        try {
            using var document = JsonDocument.Parse(value[prefix.Length..]);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !UniqueKeys(document.RootElement)) return false;
            result = document.RootElement.Deserialize<T>(Json);
            return result is not null;
        }
        catch (Exception error) when (error is JsonException or NotSupportedException or ArgumentException) { return false; }
    }

    internal static BattleLimitedScope? ScopeFor(MatchState state, string battlefield)
    {
        BattleLimitedScope? result = null;
        foreach (var text in state.UntilEndOfTurnEffects.Where(v => v.StartsWith(ScopePrefix, StringComparison.Ordinal))) {
            if (!TryScope(text, out var scope)) return null;
            if (scope!.BattlefieldId != battlefield) continue;
            if (result is not null) return null;
            result = scope;
        }
        return result;
    }
    internal static BattleFirstRole? First(MatchState state, BattleLimitedScope scope, string source, long generation, string role)
    {
        var entries = state.UntilEndOfTurnEffects.Where(v => v.StartsWith(RolePrefix, StringComparison.Ordinal))
            .Select(v => TryRole(v, out var entry) ? entry : null)
            .Where(entry => entry is not null && entry.Scope == scope && entry.SourceObjectId == source
                && entry.SourceGeneration == generation && entry.Role == role).ToArray();
        return entries.Length == 1 ? entries[0] : null;
    }
}
