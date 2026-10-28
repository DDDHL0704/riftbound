using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalCostReductionNetworkChecksTests
{
    private const string Reducer = "reducer:7";
    private const long Generation = 3;

    [Theory]
    [InlineData("echo")]
    [InlineData("copied")]
    public void AcceptsTheExactDeclaredFeeAndEachIndependentSource(string kind)
    {
        var (requirement, costs) = Fixture(kind);
        var group = OptionalCostReductionNetworkChecks.SelectedGroup(ToElement(requirement), costs, kind, Reducer, Generation);

        Assert.Equal(kind == "copied" ? JsonValueKind.Object : JsonValueKind.Null,
            group.GetProperty("ruleTextOrigin").ValueKind);
    }

    [Theory]
    [InlineData("optional-cardinality")]
    [InlineData("wrong-fee-dependency")]
    [InlineData("wrong-source")]
    [InlineData("wrong-generation")]
    [InlineData("wrong-component")]
    [InlineData("stale-instance")]
    [InlineData("missing-allocation")]
    [InlineData("duplicate-allocation")]
    [InlineData("duplicate-group")]
    [InlineData("missing-fee")]
    public void RejectsMalformedCopiedAllocationMetadataOrCommand(string defect)
    {
        var (requirement, costs) = Fixture("copied");
        var groups = requirement["optionalCostReductionGroups"]!.AsArray();
        var copy = groups[1]!.AsObject();
        switch (defect)
        {
            case "optional-cardinality": copy["minSelections"] = 0; break;
            case "wrong-fee-dependency": copy["requiresOneOfOptionalCostSelections"] = JsonSerializer.SerializeToNode(new[] { new[] { "ECHO" } }); break;
            case "wrong-source": copy["sourceObjectId"] = "other"; break;
            case "wrong-generation": copy["sourceGeneration"] = Generation + 1; break;
            case "wrong-component": copy["componentId"] = "ECHO"; break;
            case "stale-instance": copy["sourceInstanceId"] = "ATTACHED:gear:0:old"; break;
            case "missing-allocation": costs.RemoveAt(costs.Count - 1); break;
            case "duplicate-allocation": costs.Add(costs[^1]); break;
            case "duplicate-group": groups.Add(copy.DeepClone()); break;
            case "missing-fee": costs.RemoveAt(0); break;
            default: throw new InvalidOperationException(defect);
        }

        Assert.Throws<InvalidOperationException>(() => OptionalCostReductionNetworkChecks.SelectedGroup(
            ToElement(requirement), costs, "copied", Reducer, Generation));
    }

    private static (JsonObject Requirement, List<string> Costs) Fixture(string kind)
    {
        var copied = kind == "copied";
        var component = copied ? "ADDITIONAL" : "ECHO";
        var fee = copied ? "SPEND_POWER:green:1" : "ECHO";
        var printedInstance = $"PRINTED:{Reducer}:{Generation}";
        const string copiedInstance = "ATTACHED:gear:0:2";
        var prefix = $"OPTIONAL_COST_REDUCTION:{Uri.EscapeDataString(Reducer)}:{Generation}:{component}:";
        var printedChoice = prefix + "MANA";
        var copiedChoice = prefix + "POWER-green:RULE_TEXT:" + Uri.EscapeDataString(copiedInstance);
        object Group(bool isCopy, string instance, string choice) => new {
            sourceObjectId = Reducer, sourceGeneration = Generation, sourceInstanceId = instance,
            componentId = component, minSelections = 1, maxSelections = 1,
            ruleTextOrigin = isCopy ? new { revision = 2, unit = new { objectId = Reducer }, equipment = new { objectId = "gear" } } : null,
            choiceIds = new[] { choice }, requiresOneOfOptionalCostSelections = new[] { new[] { fee } }
        };
        var groups = new List<object> { Group(false, printedInstance, printedChoice) };
        var costs = new List<string> { fee, printedChoice };
        if (copied) { groups.Add(Group(true, copiedInstance, copiedChoice)); costs.Add(copiedChoice); }
        return (JsonSerializer.SerializeToNode(new { optionalCostReductionGroups = groups })!.AsObject(), costs);
    }

    private static JsonElement ToElement(JsonNode node) => JsonSerializer.SerializeToElement(node);
}
