namespace Riftbound.Engine;

public static class LegendActionAbilityCatalog
{
    public const string AzirLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT";
    public const string LilliaLegendAbilityId = "LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> SourceCardNosByAbilityId =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [AzirLegendAbilityId] = ["SFD·197/221", "SFD·247/221"],
            [LilliaLegendAbilityId] = ["UNL-189/219", "UNL-230/219", "UNL-230*/219"]
        };

    public static IReadOnlyList<string> SourceCardNosForAbility(string? abilityId)
    {
        return !string.IsNullOrWhiteSpace(abilityId)
            && SourceCardNosByAbilityId.TryGetValue(abilityId.Trim(), out var sourceCardNos)
                ? sourceCardNos
                : [];
    }

    public static bool IsSourceCardNoForAbility(
        string? abilityId,
        string? cardNo)
    {
        return !string.IsNullOrWhiteSpace(cardNo)
            && SourceCardNosForAbility(abilityId).Contains(cardNo.Trim(), StringComparer.Ordinal);
    }
}
