using System.Globalization;
using System.Text.Json;

internal static class OptionalCostReductionNetworkChecks
{
    internal static JsonElement SelectedGroup(JsonElement requirement, IReadOnlyList<string> optionalCosts,
        string kind, string reducer, long generation)
    {
        var (component, fee, expectedCount) = kind switch {
            "echo" => ("ECHO", "ECHO", 1),
            "copied" => ("ADDITIONAL", "SPEND_POWER:green:1", 2),
            _ => throw new InvalidOperationException("Unknown optional-reduction network fixture.")
        };
        var selected = optionalCosts.Where(id => id.StartsWith("OPTIONAL_COST_REDUCTION:", StringComparison.Ordinal)).ToArray();
        Check(optionalCosts.Count(id => id == fee) == 1 && selected.Length == expectedCount
            && selected.Distinct(StringComparer.Ordinal).Count() == expectedCount,
            "The declared fee must have the expected distinct reduction allocations.");
        var groups = requirement.GetProperty("optionalCostReductionGroups").EnumerateArray().ToArray();
        var matched = new List<(JsonElement Group, bool Copied, string Instance)>();
        foreach (var choice in selected)
        {
            var group = groups.Single(item => item.GetProperty("choiceIds").EnumerateArray()
                .Any(id => id.GetString() == choice));
            var instance = group.GetProperty("sourceInstanceId").GetString();
            var origin = group.GetProperty("ruleTextOrigin");
            Check(group.GetProperty("sourceObjectId").GetString() == reducer
                && group.GetProperty("sourceGeneration").GetInt64() == generation
                && group.GetProperty("componentId").GetString() == component
                && group.GetProperty("minSelections").GetInt32() == 1 && group.GetProperty("maxSelections").GetInt32() == 1
                && instance is { Length: > 0 } && origin.ValueKind is JsonValueKind.Null or JsonValueKind.Object,
                "Live metadata must bind the source, component, instance and mandatory allocation.");
            var requirements = group.GetProperty("requiresOneOfOptionalCostSelections").EnumerateArray().ToArray();
            Check(requirements.Length == 1 && requirements[0].EnumerateArray().Select(id => id.GetString()).SequenceEqual([fee]),
                "The allocation must be required only for the declared optional fee.");
            var copied = origin.ValueKind == JsonValueKind.Object;
            var expected = "OPTIONAL_COST_REDUCTION:" + Uri.EscapeDataString(reducer) + ":"
                + generation.ToString(CultureInfo.InvariantCulture) + ":" + component + ":"
                + (copied ? "POWER-green:RULE_TEXT:" + Uri.EscapeDataString(instance!) : "MANA");
            Check(choice == expected, "The allocation must match this source instance and resource dimension.");
            matched.Add((group, copied, instance!));
        }
        Check(matched.Count(item => !item.Copied) == 1 && matched.Count(item => item.Copied) == expectedCount - 1
            && matched.Select(item => item.Instance).Distinct(StringComparer.Ordinal).Count() == expectedCount,
            "Printed and copied allocations must be distinct, independently bound instances.");
        return matched.Single(item => item.Copied == (kind == "copied")).Group;
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
