using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SacrificeRecallReplacementParserTests
{
    private const string Kind = ReplacementKinds.SourceUnitDestroyedDestroyFriendlyRecallExhaustedOncePerTurn;
    private const string Text = "每回合限一次，如果我将被摧毁，则你可以选择改为摧毁一名友方单位，以此移除我所受伤害、让我变为休眠状态、并将我召回。";

    [Theory]
    [InlineData("RAD·020/167", 39510)] [InlineData("RAD·020a/167", 39511)]
    public void BothOfficialTextsKeepSelfDestructionFriendlySacrificeAndTheOncePerTurnLimit(string no, int id)
    {
        using var stream = typeof(OfficialCardCatalog).Assembly.GetManifestResourceStream("Riftbound.CardCatalog.VendettaSourceSnapshot");
        using var doc = JsonDocument.Parse(stream!);
        var card = Assert.Single(doc.RootElement.GetProperty("cards").EnumerateArray(),
            row => row.GetProperty("cardNo").GetString() == no && row.GetProperty("id").GetInt32() == id);
        var text = card.GetProperty("cardEffect").GetString()!;
        Assert.Contains(Text, text);
        var replacement = Assert.Single(ReplacementParser.Parse(text), r => r.Kind == Kind);
        Assert.Equal("source-unit-destroyed", replacement.AppliesTo);
        Assert.Equal(Text.TrimEnd('。'), replacement.Text);
    }

    [Theory]
    [InlineData("每回合限一次，如果我将被摧毁，则你可以选择改为摧毁一名友方单位，以此移除我所受伤害、让我变为休眠状态、并将我召回。")]
    [InlineData("每回合限一次, 如果我将被摧毁, 则你可以选择改为摧毁一名友方单位, 以此移除我所受伤害, 让我变为休眠状态, 并将我召回。")]
    public void TheReplacementDependsOnTheInstructionAndKeepsItsOwnSubject(string text)
        => Assert.Equal("source-unit-destroyed", Assert.Single(ReplacementParser.Parse(text), r => r.Kind == Kind).AppliesTo);

    [Theory]
    [InlineData("如果我将被摧毁，则你可以选择改为摧毁一名友方单位，以此移除我所受伤害、让我变为休眠状态、并将我召回。")]
    [InlineData("每回合限一次，如果我将被摧毁，则你可以选择改为摧毁一件友方装备，以此移除我所受伤害、让我变为休眠状态、并将我召回。")]
    [InlineData("每回合限一次，如果我将被摧毁，则你可以选择改为摧毁一名敌方单位，以此移除我所受伤害、让我变为休眠状态、并将我召回。")]
    [InlineData("每回合限一次，如果我将被摧毁，则你可以选择改为摧毁一名友方单位，以此移除我所受伤害、让我变为活跃状态、并将我召回。")]
    public void DifferentLimitsCostsOrRecallInstructionsCannotAcquireThisReplacement(string text)
        => Assert.DoesNotContain(ReplacementParser.Parse(text), r => r.Kind == Kind);
}
