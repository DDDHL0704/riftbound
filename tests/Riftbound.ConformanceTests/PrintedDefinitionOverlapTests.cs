using System.Reflection;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PrintedDefinitionOverlapTests
{
    private static bool Same(OfficialCard left, OfficialCard right)
        => (bool)typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!
            .GetMethod("SamePrintedDefinition", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [left, right])!;

    [Fact]
    public async Task ReviewedPhysicalAndAdmittedGalleryPrintsCanShareTheirPrintedDefinition()
    {
        var current = Assert.Single(ScopedOfficialCardSources.GetPhysicalPrints(), card => card.CardNo == "RAD·063/167");
        var admitted = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, card => card.CardNo == current.CardNo);
        Assert.NotEqual(current.Id, admitted.Id);
        Assert.True(Same(current, admitted));
        Assert.True(Same(admitted, current));
        var clone = admitted with { CardColorList = admitted.CardColorList.ToArray(), CardCategoryList = admitted.CardCategoryList.ToArray(),
            CardCategoryNameList = admitted.CardCategoryNameList.ToArray() };
        Assert.True(Same(admitted, clone));
    }

    [Theory]
    [InlineData("might")] [InlineData("mana")] [InlineData("power")] [InlineData("color")]
    [InlineData("type")] [InlineData("name")] [InlineData("subtitle")] [InlineData("effect")]
    [InlineData("trait")] [InlineData("number")] [InlineData("hero")]
    public void GenuinePrintedConflictsRemainRejected(string field)
    {
        var original = Assert.Single(ScopedOfficialCardSources.GetPhysicalPrints(), card => card.CardNo == "RAD·063/167");
        var changed = field switch {
            "might" => original with { Power = original.Power + 1 }, "mana" => original with { Energy = original.Energy + 1 },
            "power" => original with { ReturnEnergy = original.ReturnEnergy + 1 }, "color" => original with { CardColorList = ["red"] },
            "type" => original with { CardCategoryNameList = ["法术"] }, "name" => original with { CardName = "FORGED" },
            "subtitle" => original with { SubTitle = "FORGED" }, "effect" => original with { CardEffect = "FORGED" },
            "trait" => original with { Tag = "FORGED" }, "number" => original with { CardNo = "RAD·SP4/005" },
            _ => original with { Hero = "FORGED" } };
        Assert.False(Same(original, changed));
    }
}
