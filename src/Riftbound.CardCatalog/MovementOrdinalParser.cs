using System.Globalization;
using System.Text.RegularExpressions;
using Riftbound.Contracts;

namespace Riftbound.CardCatalog;

public static class MovementOrdinalParser
{
    public const string NextControllerTurnEnd = "UNTIL_END_OF_NEXT_CONTROLLER_TURN";

    public static IReadOnlyList<TriggerSpec> Parse(string? text)
    {
        var result = new List<TriggerSpec>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var resource = Regex.Match(text,
            @"(?:^|\n)\s*当我每回合首次移动时，\{\{获得\}\}\{\{(?<mana>[0-9]+)\}\}和\{\{A\}\}。",
            RegexOptions.CultureInvariant);
        if (resource.Success)
            result.Add(new(TriggerKinds.UnitOrdinalMovementGainResources, TriggerTimings.UnitMoved, resource.Value.Trim(),
                TargetScope: TriggerTargetScopes.SourceUnit,
                ManaDelta: int.Parse(resource.Groups["mana"].Value, CultureInfo.InvariantCulture),
                MovementOrdinal: 1, GeneratedPower: 1));
        var banish = Regex.Match(text,
            @"(?:^|\n)\s*当我每回合第二次移动时，放逐你主牌堆顶部的一张牌。直到你的下一个回合结束阶段，你可以选择打出该牌。(?:（仍须支付卡牌费用。）)?",
            RegexOptions.CultureInvariant);
        if (banish.Success)
            result.Add(new(TriggerKinds.UnitOrdinalMovementBanishTopPlay, TriggerTimings.UnitMoved, banish.Value.Trim(),
                TargetScope: TriggerTargetScopes.SourceUnit, Duration: NextControllerTurnEnd, BanishCount: 1,
                PlayCount: 1, PlayOriginZone: "BANISHED", Optional: true, MovementOrdinal: 2));
        return result;
    }
}
