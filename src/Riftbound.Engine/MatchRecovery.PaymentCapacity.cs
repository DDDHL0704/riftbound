using System.Text.Json;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static readonly JsonSerializerOptions PaymentCapacityJson = new(JsonSerializerDefaults.Web);
    // The capacity variant is derived from the actual pool and every legal
    // physical rune. Missing the old field is legal only for that exact shape.
    private static void ValidatePaymentResourceCapacityViews(RecoveredPlayerView view, MatchState state, List<string> errors)
    {
        if (view.Prompt is null) return;
        var expected = ResolutionResult.BuildPrompts(state).GetValueOrDefault(view.PlayerId);
        if (expected is null) return;
        foreach (var action in new[] { "PLAY_CARD", "ACTIVATE_ABILITY", "ASSEMBLE_EQUIPMENT", "PAY_COST", "DECLARE_BATTLE" })
        {
            var actualCandidates = view.Prompt.Candidates?.Where(c => c.Action == action).ToArray() ?? [];
            var expectedCandidates = expected.Candidates?.Where(c => c.Action == action).ToArray() ?? [];
            var wanted = Nodes(JsonSerializer.SerializeToElement(expectedCandidates.Select(c => c.Metadata), PaymentCapacityJson));
            var found = Nodes(JsonSerializer.SerializeToElement(actualCandidates.Select(c => c.Metadata), PaymentCapacityJson));
            var label = $"prompt for {view.PlayerId} {action} payment resource projection";
            if (!wanted.Keys.Order(StringComparer.Ordinal).SequenceEqual(found.Keys.Order(StringComparer.Ordinal)))
            { errors.Add(label + " has missing or extra resource requirements"); continue; }
            foreach (var (path, required) in wanted)
            foreach (var key in new[] { "availablePower", "availablePowerByTrait", "availableRainbowPower", "availablePowerWithPaymentResources",
                "availablePowerByTraitWithPaymentResources", "paymentResourcePowerCapacity", "paymentResourceChoices", "paymentResourcePowerByChoice",
                "paymentResourceActionIds", "optionalCostChoices", "resourceLedgerBeforePayment" })
            {
                var hasExpected = required.TryGetProperty(key, out var first);
                var hasActual = found[path].TryGetProperty(key, out var second);
                if (hasExpected != hasActual || hasExpected && MatchStateHasher.HashValue(first) != MatchStateHasher.HashValue(second))
                    errors.Add(label + " " + path + "." + key + " disagrees with the exact server-derived resource/capacity shape");
            }
        }
        static Dictionary<string, JsonElement> Nodes(JsonElement root)
        {
            var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal); Walk(root, "$"); return result;
            void Walk(JsonElement node, string path)
            {
                if (node.ValueKind == JsonValueKind.Object)
                {
                    if (node.TryGetProperty("availablePowerWithPaymentResources", out _)
                        || node.TryGetProperty("paymentResourcePowerCapacity", out _)
                        || node.TryGetProperty("availablePowerByTraitWithPaymentResources", out _)) result.Add(path, node);
                    foreach (var property in node.EnumerateObject()) Walk(property.Value, path + "." + property.Name);
                }
                else if (node.ValueKind == JsonValueKind.Array)
                { var index = 0; foreach (var item in node.EnumerateArray()) Walk(item, path + "[" + index++ + "]"); }
            }
        }
    }
}
