using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public sealed record StandbyPlayedSelfEffect(int Might, bool ReadiesSelf = false);
public sealed record StandbyPlayedDrawEffect(int DrawCount, int OutsideTurnDiscardCount, bool OncePerTurn);

public static class StandbyParser
{
    // Keyword instances stand alone at the start of an instruction line.
    // References to standby in a trigger or a permission do not grant the keyword.
    public static bool HasPrintedKeyword(string? text)
        => text is not null && Regex.IsMatch(text, @"(?:^|\r?\n)\s*\{\{待命\}\}");

    public static StandbyPlayedSelfEffect? SelfEffectWhenControllerPlaysFromFaceDown(string? text)
    {
        var ready = Regex.Match(text ?? "", @"(?:^|\r?\n)当你将一张卡牌从正面朝下的状态打出时，让我变为活跃状态，且给予我在本回合内\{\{S\}\}\+(?<n>[0-9]+)[。.]?(?:$|\r?\n)", RegexOptions.CultureInvariant);
        if (ready.Success) return new(int.Parse(ready.Groups["n"].Value), ReadiesSelf: true);
        var match=Regex.Match(text??"",@"每当你将一张牌从正面朝下的\{\{待命\}\}状态中打出时，让我本回合内\{\{S\}\}\+(?<n>[0-9]+)");
        return match.Success ? new(int.Parse(match.Groups["n"].Value)) : null;
    }

    public static int SelfMightWhenControllerPlaysFromFaceDown(string? text)
        => SelfEffectWhenControllerPlaysFromFaceDown(text)?.Might ?? 0;

    public static StandbyPlayedDrawEffect? DrawWhenControllerPlaysFromFaceDown(string? text)
        => Regex.IsMatch(text ?? "", @"(?:^|\r?\n)每回合限一次，当你将一张牌从正面朝下的状态打出时，抽一张牌。然后，如果不是你的回合，则弃置一张手牌。[\s]*(?:$|\r?\n)", RegexOptions.CultureInvariant)
            ? new(1, 1, OncePerTurn: true) : null;

    public static bool PreventsEnemyFaceDownPlayHere(string? text)
        => text is not null && text.Contains("对手的{{待命}}卡牌无法在此处被翻开打出。",StringComparison.Ordinal);

    public static bool AttachesSelfToFriendlyUnitHereWhenPlayedFromFaceDown(string? text)
        => text is not null && text.Contains("当你从正面朝下状态中打出此牌时，将此牌贴附到此处你控制的一名单位上。", StringComparison.Ordinal);
}
