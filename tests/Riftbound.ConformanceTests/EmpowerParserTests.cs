using System.Text.Json;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EmpowerParserTests
{
    [Fact]
    public void CurrentSourceQueueClassifiesCostsWithoutCertifyingUnpromotedConsumers()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Riftbound.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "docs/legacy/evidence/empower-2026-10-13/source-readback.json")));
        var records = json.RootElement.GetProperty("empowerRecords").EnumerateArray().ToArray();
        Assert.Equal(84, records.Length);
        var keywordRecords = records.Where(r => Regex.IsMatch(r.GetProperty("record").GetProperty("cardEffect").GetString()!, @"\{\{强化[^}]*\}\}")).ToArray();
        Assert.Equal(58, keywordRecords.Length);
        var classified = keywordRecords.Select(r => {
            var card = r.GetProperty("record"); var specs = EmpowerParser.Parse(card.GetProperty("cardEffect").GetString()!);
            if (card.GetProperty("cardNo").GetString() != "VEN·163") Assert.NotEmpty(specs);
            return new { cardNo = card.GetProperty("cardNo").GetString(), specs };
        }).ToArray();
        Assert.Empty(Assert.Single(classified, r => r.cardNo == "VEN·163").specs);
        Assert.Equal(57, classified.Sum(r => r.specs.Count));
        foreach (var cardNo in new[] { "VEN·007", "VEN·110", "VEN·110a", "VEN·124", "VEN·188" })
            Assert.All(Assert.Single(classified, r => r.cardNo == cardNo).specs.SelectMany(s => s.Alternatives), c => Assert.False(c.SupportedResources));
        var output = Environment.GetEnvironmentVariable("RIFTBOUND_EMPOWER_PARSER_OUTPUT");
        if (output is not null) File.WriteAllText(output, JsonSerializer.Serialize(classified, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    [Theory]
    [InlineData("{{强化3}}", 3, 0, "", 0)]
    [InlineData("{{强化2红色}}", 2, 0, "red", 1)]
    [InlineData("{{强化6紫色紫色}}", 6, 0, "purple", 2)]
    [InlineData("{{强化1AA}}", 1, 2, "", 0)]
    [InlineData("{{强化橙色橙色}}", 0, 0, "orange", 2)]
    public void PrintedResourceCostsKeepManaSeparateFromTypedAndGenericPower(string text, int mana, int power, string trait, int typed)
    {
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(text)).Alternatives);
        Assert.True(cost.SupportedResources); Assert.Equal(mana, cost.Mana); Assert.Equal(power, cost.Power);
        Assert.Equal(typed, trait.Length == 0 ? cost.TypedPower.Values.Sum() : cost.TypedPower[trait]);
    }

    [Fact]
    public void AlternativeAndExhaustCostsRemainExplicitAndNonresourceCostsDoNotPretendToBeFree()
    {
        var choices = Assert.Single(EmpowerParser.Parse("{{强化}} — 支付{{1}}或{{橙色}}（支付其中一种费用：强化我。）")).Alternatives;
        Assert.Equal(2, choices.Count); Assert.Equal(1, choices[0].Mana); Assert.Equal(1, choices[1].TypedPower["orange"]);
        Assert.All(choices, c => Assert.True(c.SupportedResources));
        var exhausted = Assert.Single(Assert.Single(EmpowerParser.Parse("{{强化}} — 支付{{1}}，{{横置}}（支付此费用：强化此牌。）")).Alternatives);
        Assert.Equal(1, exhausted.Mana); Assert.True(exhausted.Exhaust); Assert.True(exhausted.SupportedResources);
        foreach (var text in new[] { "{{强化}} — 弃置一张手牌", "{{强化}} — 摧毁一名友方单位" })
            Assert.False(Assert.Single(Assert.Single(EmpowerParser.Parse(text)).Alternatives).SupportedResources);
        Assert.False(Assert.Single(Assert.Single(EmpowerParser.Parse("{{强化}}")).Alternatives).SupportedResources);
    }

    [Fact]
    public void RuneReducersAndEmpoweredSelfTextAreConditional()
    {
        var reduced = Assert.Single(EmpowerParser.Parse("{{强化5}}。如果你控制的符文数量不超过四枚，则此技能的费用减少{{3}}。"));
        Assert.Equal(4, reduced.RuneLimit); Assert.Equal(3, reduced.ManaReductionAtMostRunes);
        Assert.True(Assert.Single(EmpowerParser.Parse("{{强化12}}。你每控制一枚符文，此技能的费用便减少{{1}}。")).ReduceManaPerRune);
        var self = EmpowerParser.Self("{{已强化>}} 我获得{{S}}+1和{{游走}}。\n{{已强化>}} 我获得{{法盾}}和{{坚守3}}。");
        Assert.Equal(1, self.Might); Assert.Equal(new[] { "游走", "法盾", "坚守3" }, self.Keywords);
    }
}
