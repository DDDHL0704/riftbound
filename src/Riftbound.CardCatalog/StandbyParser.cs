using System.Text.RegularExpressions;

namespace Riftbound.CardCatalog;

public static class StandbyParser
{
    // Keyword instances stand alone at the start of an instruction line.
    // References to standby in a trigger or a permission do not grant the keyword.
    public static bool HasPrintedKeyword(string? text)
        => text is not null && Regex.IsMatch(text, @"(?:^|\r?\n)\s*\{\{待命\}\}");

    public static int SelfMightWhenControllerPlaysFromFaceDown(string? text)
    {
        var match=Regex.Match(text??"",@"每当你将一张牌从正面朝下的\{\{待命\}\}状态中打出时，让我本回合内\{\{S\}\}\+(?<n>[0-9]+)");
        return match.Success ? int.Parse(match.Groups["n"].Value) : 0;
    }
    public static bool PreventsEnemyFaceDownPlayHere(string? text)
        => text is not null && text.Contains("对手的{{待命}}卡牌无法在此处被翻开打出。",StringComparison.Ordinal);

    public static bool AttachesSelfToFriendlyUnitHereWhenPlayedFromFaceDown(string? text)
        => text is not null && text.Contains("当你从正面朝下状态中打出此牌时，将此牌贴附到此处你控制的一名单位上。", StringComparison.Ordinal);
}
