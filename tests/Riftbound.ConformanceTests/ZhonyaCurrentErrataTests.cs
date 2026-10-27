using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZhonyaCurrentErrataTests
{
    private const string No = "OGN·077/298";
    private const string Standby = "{{待命}}（支付{{A}}正面朝下放置此牌，之后可支付{{0}}将其当作反应牌打出。）";
    private const string Reminder = "（把该单位送回基地，此行动不算作移动。）";
    private const string Printed = Standby + "\n下一次当友方单位被摧毁时，改为将此牌摧毁，然后以休眠状态将该单位召回。" + Reminder;
    private const string Head = "如果一名友方单位被摧毁，则改为将此牌摧毁";
    private const string Continuation = "移除该单位所受伤害，让其进入休眠状态，并将其召回";
    private const string Current = Standby + "\n" + Head + "。" + Continuation + "。" + Reminder;

    [Fact]
    public async Task ExactReviewedPrintingUsesTheCompleteCorrectionAndRetainsTheAcquiredPrintedSnapshot()
    {
        var raw = await OfficialCardCatalogLoader.LoadFromFileAsync(OfficialCardCatalog.DefaultCatalogPath(), default);
        var printed = Assert.Single(raw.Cards, c => c.Id == 31291);
        var effective = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.Id == 31291);
        await Save("exact-source", new { printed, effective, expectedCurrent = Current });
        Assert.Equal(No, printed.CardNo); Assert.Equal(Printed, printed.CardEffect);
        Assert.Equal(No, effective.CardNo); Assert.Equal("中娅沙漏", effective.CardName);
        Assert.Equal([RuneTrait.Green], effective.CardColorList); Assert.Equal(["equipment"], effective.CardCategoryList);
        Assert.Equal(2, effective.Energy); Assert.Null(effective.ReturnEnergy);
        Assert.Equal(Current, effective.CardEffect);
    }

    [Theory]
    [InlineData("printed")] [InlineData("current")] [InlineData("current-crlf")]
    public async Task LegacyAndCompleteCorrectedSpansRemainOneTypedReplacement(string version)
    {
        var text = version == "printed" ? Printed : version == "current" ? Current : Current.Replace("\n", "\r\n", StringComparison.Ordinal);
        var parsed = ReplacementParser.Parse(text); await Save("typed-" + version, new { text, parsed });
        var replacement = Assert.Single(parsed);
        Assert.Equal(ReplacementKinds.FriendlyUnitDestroyedDestroySourceRecallExhausted, replacement.Kind);
        Assert.Equal("friendly-unit-destroyed", replacement.AppliesTo);
        if (version == "printed") Assert.Contains("下一次当友方单位被摧毁时", replacement.Text, StringComparison.Ordinal);
        else
        {
            Assert.Contains(Head, replacement.Text, StringComparison.Ordinal);
            Assert.Contains(Continuation, replacement.Text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("missing-tail")] [InlineData("unrelated-recall")]
    [InlineData("wrong-order")] [InlineData("other-unit")]
    [InlineData("missing-damage-removal")] [InlineData("missing-exhaustion")]
    [InlineData("missing-recall")] [InlineData("opponent-trigger")]
    [InlineData("other-sacrifice")]
    public async Task AReplacementCannotBorrowAnotherClausesRecallOrOmitPartOfTheCorrectedAction(string branch)
    {
        var text = branch switch {
            "missing-tail" => Head + "。",
            "unrelated-recall" => Head + "。当你打出我时，将一名友方单位召回。",
            "wrong-order" => Continuation + "。" + Head + "。",
            "other-unit" => Head + "。移除另一名单位所受伤害，让其进入休眠状态，并将其召回。",
            "missing-damage-removal" => Head + "。让该单位进入休眠状态，并将其召回。",
            "missing-exhaustion" => Head + "。移除该单位所受伤害，并将其召回。",
            "missing-recall" => Head + "。移除该单位所受伤害，让其进入休眠状态。",
            "opponent-trigger" => Head.Replace("友方", "敌方", StringComparison.Ordinal) + "。" + Continuation + "。",
            _ => Head.Replace("此牌", "另一件装备", StringComparison.Ordinal) + "。" + Continuation + "。"
        };
        var parsed = ReplacementParser.Parse(text); await Save("negative-" + branch, new { text, parsed });
        Assert.DoesNotContain(parsed, r => r.Kind == ReplacementKinds.FriendlyUnitDestroyedDestroySourceRecallExhausted);
    }

    [Fact]
    public async Task TheBoundContinuationDoesNotConsumeTheNextIndependentReplacement()
    {
        const string unrelated = "如果一名敌方单位被摧毁，则改为将其放逐";
        var text = Head + "。" + Continuation + "。" + unrelated + "。";
        var parsed = ReplacementParser.Parse(text); await Save("independent-neighbor", new { text, parsed });
        Assert.Equal(2, parsed.Count);
        Assert.Equal(ReplacementKinds.FriendlyUnitDestroyedDestroySourceRecallExhausted, parsed[0].Kind);
        Assert.Contains(Continuation, parsed[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(unrelated, parsed[0].Text, StringComparison.Ordinal);
        Assert.Equal("replacement", parsed[1].Kind); Assert.Equal(unrelated, parsed[1].Text);
    }

    private static async Task Save(string label, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_OGN077_ERRATA_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN31291 whole printed record and complete current official API errata; legacy grammar and adjacent independent-clause boundaries. Publication/effective dates and complete source applicability UNKNOWN. Parser/catalog L1 only; no live game, resource reachability, whole-card or L2/L3 certification.",
            value }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
