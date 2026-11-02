using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static void ValidateUnitHeldStackViews(IReadOnlyList<object?> payloads, MatchState authority,
        string viewer, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            TryReadObjectString(payload, "stackItemId", out var id);
            var item = authority.StackItems.FirstOrDefault(value => value.StackItemId == id);
            if (item is null || !CoreRuleEngine.IsParsedUnitHeldContext(item.HeldContext)) continue;
            var json = JsonSerializer.SerializeToElement(payload);
            foreach (var key in new[] { "heldContext", "unitSource" })
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(key, out var value)
                    && value.ValueKind != JsonValueKind.Null)
                    errors.Add($"{viewer} public unit Hold skill must not contain private {key}");
            TryReadObjectString(payload, "sourceObjectId", out var source);
            if (source != ResolutionResult.VisibleObjectIdForViewer(authority, item.SourceObjectId, viewer))
                errors.Add($"{viewer} public unit Hold source differs from authoritative visibility");
        }
    }
}
