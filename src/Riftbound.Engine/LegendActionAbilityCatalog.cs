namespace Riftbound.Engine;

public static class LegendActionAbilityCatalog
{
    public const string YasuoLegendAbilityId = "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT";
    public const string LeeSinLegendAbilityId = "LEGEND_PAY_1_EXHAUST_GRANT_BOON";
    public const string PoppyLegendAbilityId = "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW";
    public const string ViktorLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_MINION";
    public const string MissFortuneLegendAbilityId = "LEGEND_EXHAUST_GRANT_ROAM";
    public const string KhazixLegendBoonAbilityId = "LEGEND_SPEND_1_EXPERIENCE_EXHAUST_GRANT_BOON";
    public const string KhazixLegendMoveAbilityId = "LEGEND_SPEND_2_EXPERIENCE_EXHAUST_MOVE_DORMANT_UNIT_TO_BASE";
    public const string PykeLegendAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_BATTLEFIELD_UNIT_CREATE_COIN";
    public const string JaxLegendAttachAbilityId = "LEGEND_PAY_1_EXHAUST_ATTACH_UNATTACHED_ARMAMENT";
    public const string JaxLegendReattachAbilityId = "LEGEND_EXHAUST_REATTACH_ATTACHED_ARMAMENT";
    public const string DariusLegendAbilityId = "LEGEND_ENCOURAGE_EXHAUST_GAIN_1_MANA";
    public const string DianaLegendAbilityId = "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA";
    public const string KaisaLegendAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_SPELL";
    public const string OrnnLegendAbilityId = "LEGEND_REACTION_EXHAUST_GAIN_1_POWER_FOR_EQUIPMENT";
    public const string EzrealLegendAbilityId = "LEGEND_REACTION_EXHAUST_DRAW_AFTER_TWO_ENEMY_TARGETS";
    public const string IreliaLegendAbilityId = "LEGEND_REACTION_PAY_1_EXHAUST_READY_TARGETED_FRIENDLY_UNIT";
    public const string TeemoLegendAbilityId = "LEGEND_PAY_1_EXHAUST_RECALL_OWNED_TEEMO_UNIT";
    public const string AzirLegendAbilityId = "LEGEND_PAY_1_EXHAUST_CREATE_SAND_SOLDIER_AFTER_ARMAMENT";
    public const string LilliaLegendAbilityId = "LEGEND_DYNAMIC_PAY_EXHAUST_CREATE_FAERIE";

    private static readonly IReadOnlyDictionary<string, string> RepresentativeSourceCardNoByAbilityId =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [YasuoLegendAbilityId] = "FND-259/298",
            [LeeSinLegendAbilityId] = "OGN·257/298",
            [PoppyLegendAbilityId] = "UNL-203/219",
            [ViktorLegendAbilityId] = "FND-265/298",
            [MissFortuneLegendAbilityId] = "OGN·267/298",
            [KhazixLegendBoonAbilityId] = "UNL-201/219",
            [KhazixLegendMoveAbilityId] = "UNL-201/219",
            [PykeLegendAbilityId] = "UNL-185/219",
            [JaxLegendAttachAbilityId] = "SFD·193/221",
            [JaxLegendReattachAbilityId] = "SFD·193/221",
            [DariusLegendAbilityId] = "OGN·253/298",
            [DianaLegendAbilityId] = "UNL-197/219",
            [KaisaLegendAbilityId] = "OGN·247/298",
            [OrnnLegendAbilityId] = "SFD·189/221",
            [EzrealLegendAbilityId] = "SFD·199/221",
            [IreliaLegendAbilityId] = "SFD·195/221",
            [TeemoLegendAbilityId] = "OGN·263/298",
            [AzirLegendAbilityId] = "SFD·197/221",
            [LilliaLegendAbilityId] = "UNL-189/219"
        };

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> SourceCardNosByRepresentativeCardNo =
        new(BuildSourceCardNosByRepresentativeCardNo, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<string> SourceCardNosForAbility(string? abilityId)
    {
        return !string.IsNullOrWhiteSpace(abilityId)
            && RepresentativeSourceCardNoByAbilityId.TryGetValue(abilityId.Trim(), out var representativeCardNo)
            && SourceCardNosByRepresentativeCardNo.Value.TryGetValue(
                OfficialCardSourceIdentityGroups.NormalizeCardNo(representativeCardNo),
                out var sourceCardNos)
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

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSourceCardNosByRepresentativeCardNo()
    {
        return OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(
            RepresentativeSourceCardNoByAbilityId.Values);
    }
}
