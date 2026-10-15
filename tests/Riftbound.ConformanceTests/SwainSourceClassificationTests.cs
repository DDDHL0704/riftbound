using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SwainSourceClassificationTests
{
    [Theory]
    [InlineData(37937, "VEN·065", "VEN·065/166", "ven-065-166")]
    [InlineData(37876, "VEN·173", "VEN·173/166", "ven-173-166")]
    public void ExactCurrentSourcesKeepIdentityAndConjunctiveConquestCondition(
        long id, string apiNo, string printedNo, string englishId)
    {
        using var raw = Read("cards-api.json");
        var card = Assert.Single(raw.RootElement.GetProperty("result").GetProperty("list").EnumerateArray(),
            c => c.GetProperty("id").GetInt64() == id);
        Assert.Equal(apiNo, card.GetProperty("cardNo").GetString());
        Assert.Equal(1, card.GetProperty("status").GetInt32());
        Assert.False(card.GetProperty("isPreview").GetBoolean());
        Assert.Equal(6, card.GetProperty("energy").GetInt32());
        Assert.Equal(1, card.GetProperty("returnEnergy").GetInt32());
        Assert.Equal(6, card.GetProperty("power").GetInt32());
        var text = card.GetProperty("cardEffect").GetString()!;
        Assert.StartsWith("{{预知}}", text);
        var trigger = Assert.Single(TriggerParser.Parse(text), t => t.Timing == TriggerTimings.UnitConquest);
        Assert.Equal(TriggerKinds.UnitConquestPlayedUnitEquipmentSpellGainScore, trigger.Kind);
        Assert.Equal(TriggerTimings.UnitConquest, trigger.Timing);
        Assert.Equal(TriggerTargetScopes.SourceUnit, trigger.TargetScope);
        Assert.Equal(1, trigger.ScoreAmount);
        Assert.Contains("非指示物单位、一件非指示物装备和一个法术", trigger.Text);
        Assert.Null(trigger.RequiredOverkillDamage);
        Assert.Null(trigger.CreatedTokenCount);

        using var source = Read("source.json");
        var identity = Assert.Single(source.RootElement.GetProperty("printings").EnumerateArray(),
            c => c.GetProperty("apiId").GetInt64() == id);
        Assert.Equal(apiNo, identity.GetProperty("rawApiCardNo").GetString());
        Assert.Equal(printedNo, identity.GetProperty("printedImageCardNo").GetString());
        Assert.Equal(englishId, identity.GetProperty("englishGalleryIdentity").GetString());
        Assert.Equal("CN", source.RootElement.GetProperty("region").GetString());
        Assert.Equal("2026-10-14", source.RootElement.GetProperty("applicableAsOf").GetString());
        Assert.False(source.RootElement.GetProperty("semanticCoverageCertified").GetBoolean());
    }

    [Theory]
    [InlineData("当我征服一处战场时，如果你在本回合内打出了一名单位、一件非指示物装备和一个法术，则你获得1分。")]
    [InlineData("当我征服一处战场时，如果你在本回合内打出了一名非指示物单位、一件装备和一个法术，则你获得1分。")]
    [InlineData("当我征服一处战场时，如果你在本回合内打出了一名非指示物单位、一件非指示物装备或一个法术，则你获得1分。")]
    [InlineData("当我征服一处战场时，如果你在上回合内打出了一名非指示物单位、一件非指示物装备和一个法术，则你获得1分。")]
    [InlineData("当我据守一处战场时，如果你在本回合内打出了一名非指示物单位、一件非指示物装备和一个法术，则你获得1分。")]
    [InlineData("当我征服一处战场时，如果你在本回合内打出了一名非指示物单位、一件非指示物装备和一个法术，则对手获得1分。")]
    public void ChangedConditionsCannotSilentlyBecomeSwainsExactTrigger(string text)
        => Assert.DoesNotContain(TriggerParser.Parse(text),
            t => t.Kind == TriggerKinds.UnitConquestPlayedUnitEquipmentSpellGainScore);

    private static JsonDocument Read(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Riftbound.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName,
            "docs/evidence/swain-source-2026-10-15", name)));
    }
}
