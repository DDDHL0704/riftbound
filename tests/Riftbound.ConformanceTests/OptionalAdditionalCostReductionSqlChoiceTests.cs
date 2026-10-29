using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalAdditionalCostReductionSqlChoiceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SelectsCopiedEchoManaWhenTheGroupHasMultipleChoices(long revision)
    {
        var instance = Instance("GEAR", revision);
        var mana = Choice("EZREAL", "ECHO", "MANA", instance);
        var power = Choice("EZREAL", "ECHO", "POWER-any", instance);
        var prompt = Prompt(Group("EZREAL", "ECHO", "GEAR", revision, power, mana));

        Assert.Equal(mana, OptionalAdditionalCostReductionSqlAcceptance.CopyChoice(prompt));
    }

    [Fact]
    public void SelectsOnlyTheExpectedSourceComponentAndEquipment()
    {
        var instance = Instance("GEAR", 2);
        var mana = Choice("EZREAL", "ECHO", "MANA", instance);
        var prompt = Prompt(
            Group("EZREAL", "ADDITIONAL", "GEAR", 2, Choice("EZREAL", "ADDITIONAL", "MANA", instance)),
            Group("OTHER", "ECHO", "GEAR", 2, Choice("OTHER", "ECHO", "MANA", instance)),
            Group("EZREAL", "ECHO", "OTHER-GEAR", 2, Choice("EZREAL", "ECHO", "MANA", Instance("OTHER-GEAR", 2))),
            Group("EZREAL", "ECHO", "GEAR", 2, Choice("EZREAL", "ECHO", "POWER-any", instance), mana));

        Assert.Equal(mana, OptionalAdditionalCostReductionSqlAcceptance.CopyChoice(prompt));
    }

    [Fact]
    public void RejectsAnOldInstanceInsteadOfSubstitutingItForTheCurrentOne()
    {
        var oldChoice = Choice("EZREAL", "ECHO", "MANA", Instance("GEAR", 1));
        var prompt = Prompt(Group("EZREAL", "ECHO", "GEAR", 2, oldChoice));

        Assert.Throws<InvalidOperationException>(() => OptionalAdditionalCostReductionSqlAcceptance.CopyChoice(prompt));
    }

    [Fact]
    public void RejectsAChoiceFromAnotherSourceGeneration()
    {
        var wrongGeneration = Choice("EZREAL", "ECHO", "MANA", Instance("GEAR", 2))
            .Replace(":EZREAL:0:", ":EZREAL:1:", StringComparison.Ordinal);
        var prompt = Prompt(Group("EZREAL", "ECHO", "GEAR", 2, wrongGeneration));

        Assert.Throws<InvalidOperationException>(() => OptionalAdditionalCostReductionSqlAcceptance.CopyChoice(prompt));
    }

    [Fact]
    public void RejectsDuplicateExactChoices()
    {
        var mana = Choice("EZREAL", "ECHO", "MANA", Instance("GEAR", 2));
        var prompt = Prompt(Group("EZREAL", "ECHO", "GEAR", 2, mana, mana));

        Assert.Throws<InvalidOperationException>(() => OptionalAdditionalCostReductionSqlAcceptance.CopyChoice(prompt));
    }

    private static ActionPromptDto Prompt(params object[] groups)
        => new("P1", true, "", [], Candidates: [new(CommandTypes.PlayCard, "", true, "",
            Metadata: new Dictionary<string, object?> {
                ["sourceRequirements"] = new object[] { new { sourceObjectId = "CARD", optionalCostReductionGroups = groups } }
            })]);

    private static object Group(string source, string component, string equipment, long revision, params string[] choices)
        => new {
            sourceObjectId = source,
            sourceGeneration = 0L,
            sourceInstanceId = Instance(equipment, revision),
            componentId = component,
            ruleTextOrigin = new RuleTextInstanceBinding("SFD·149/221", new(source, 0), new(equipment, 0), revision, 0, 0),
            choiceIds = choices
        };

    private static string Instance(string equipment, long revision) => $"ATTACHED:{equipment}:0:{revision}";

    private static string Choice(string source, string component, string dimension, string instance)
        => $"OPTIONAL_COST_REDUCTION:{source}:0:{component}:{dimension}:RULE_TEXT:{Uri.EscapeDataString(instance)}";
}
