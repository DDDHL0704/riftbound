using System.Text.RegularExpressions;
using Riftbound.Contracts;

namespace Riftbound.CardCatalog;

public static class EquipmentKeywordParser
{
    public static IReadOnlyList<StaticAuraSpec> ParseOwnedKeywordGrants(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var match = Regex.Match(text,
            @"(?:^|\n)\s*你拥有\{\{部署\}\}的装备具有\{\{迅捷\}\}。",
            RegexOptions.CultureInvariant);
        return match.Success ? [new(StaticAuraKinds.OwnedFilteredEquipmentKeyword,
            "RULE_TEXT", "WHILE_SOURCE_ON_PUBLIC_FIELD",
            StaticAuraTargetScopes.OwnedFilteredEquipment, StaticAuraParticipantScopes.SourceObject,
            0, match.Value.Trim(), BehaviorImplementationStatuses.Unimplemented,
            "A public source grants Swift to its controller's owned equipment with Deploy in every zone.",
            TargetFilter: StaticAuraTargetFilters.TagPrefix + "部署", GrantedKeyword: "迅捷")] : [];
    }

    public static IReadOnlyList<TriggerSpec> ParseConquest(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var match = Regex.Match(text,
            @"(?:^|\n)\s*当我征服一处战场时，如果此处有拥有\{\{部署\}\}的友方装备，则召出一枚休眠的符文。",
            RegexOptions.CultureInvariant);
        return match.Success ? [new(TriggerKinds.UnitConquestCallDormantRune, TriggerTimings.UnitConquest,
            match.Value.Trim(), TargetScope: TriggerTargetScopes.SourceUnit, RuneCallCount: 1,
            RequiresFriendlyEquipmentKeywordHere: "部署")] : [];
    }
}
