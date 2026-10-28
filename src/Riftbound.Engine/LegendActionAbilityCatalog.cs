using System.Collections.Immutable;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

public static class LegendActionAbilityCatalog
{
    public const string JayceLegendReadyOneAbilityId = "LEGEND_PAY_1_EXHAUST_READY_ONE_EQUIPMENT";
    public const string JayceLegendReadyTwoAbilityId = "LEGEND_EMPOWERED_PAY_1_EXHAUST_READY_TWO_EQUIPMENT";
    public const string MelLegendAbilityId = "LEGEND_REMOVE_EMPOWER_EXHAUST_BATTLEFIELD_UNIT_MINUS_TWO";
    public const string AmbessaLegendAbilityId = "LEGEND_REMOVE_EMPOWER_PAY_ONE_POWER_EXHAUST_READY_UNIT";
    public const string AkaliLegendAbilityId = "LEGEND_QUICK_EXHAUST_MOVE_DUEL_UNIT";
    public const string ShenLegendAbilityId = "LEGEND_QUICK_EXHAUST_GRANT_BULWARK";
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
    public const string RenektonLegendAbilityId = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
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
    private static readonly Lazy<ImmutableArray<string>> ShenSourceCardNos = new(() =>
        VendettaCardSources.GetAdmittedCards()
            .Where(card => card.CardName == "暮光之眼" && card.Hero == "慎"
                && card.CardEffect.StartsWith("{{迅捷>}}{{横置}}：给予一名友方单位在本回合内{{壁垒}}。", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<ImmutableArray<string>> AkaliSourceCardNos = new(() =>
        VendettaCardSources.GetAdmittedCards().Where(card => card.CardName == "离群之刺" && card.Hero == "阿卡丽"
            && card.CardEffect.Contains("{{迅捷>}}{{横置}}：如果这是你的回合", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<ImmutableArray<string>> MelSourceCardNos = new(() =>
        VendettaCardSources.GetAdmittedCards().Where(card => card.CardName == "流光镜影" && card.Hero == "梅尔"
            && card.CardEffect.Contains("解除我的强化，{{横置}}：给予战场上的一名单位在本回合内{{S}}-2。", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<ImmutableArray<string>> JayceSourceCardNos = new(() =>
        VendettaCardSources.GetAdmittedCards().Where(card => card.CardName == "未来守护者" && card.Hero == "杰斯"
            && card.CardEffect.Contains("支付{{1}}，{{横置}}：让一件装备变为活跃状态。", StringComparison.Ordinal)
            && card.CardEffect.Contains("{{已强化>}} 支付{{1}}，{{横置}}：让两件装备变为活跃状态。", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<ImmutableArray<string>> AmbessaSourceCardNos = new(() =>
        VendettaCardSources.GetAdmittedCards().Where(card => card.CardName == "铁血狼母" && card.Hero == "安蓓萨"
            && card.CardEffect.Contains("解除我的强化，支付{{A}}，{{横置}}：让一名单位变为活跃状态。", StringComparison.Ordinal))
            .Select(card => card.CardNo).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly Lazy<ImmutableArray<string>> RenektonSourceCardNos = new(() =>
        ReviewedResourceLegendSources.GetPhysicalPrints()
            .Where(card => card.CardName == "荒漠屠夫" && card.Hero == "雷克顿"
                && card.CardEffect == "{{反应>}} 支付{{A}}{{A}}，{{横置}}：{{获得}}{{2}}。此法力仅能用于打出单位或单位的主动技能。")
            .Select(card => card.CardNo).ToImmutableArray(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<string> SourceCardNosForAbility(string? abilityId)
    {
        if (abilityId is JayceLegendReadyOneAbilityId or JayceLegendReadyTwoAbilityId) return JayceSourceCardNos.Value;
        if (string.Equals(abilityId, MelLegendAbilityId, StringComparison.Ordinal)) return MelSourceCardNos.Value;
        if (string.Equals(abilityId, AmbessaLegendAbilityId, StringComparison.Ordinal)) return AmbessaSourceCardNos.Value;
        if (string.Equals(abilityId, AkaliLegendAbilityId, StringComparison.Ordinal))
            return AkaliSourceCardNos.Value;
        if (string.Equals(abilityId, ShenLegendAbilityId, StringComparison.Ordinal))
            return ShenSourceCardNos.Value;
        if (string.Equals(abilityId, RenektonLegendAbilityId, StringComparison.Ordinal))
            return RenektonSourceCardNos.Value;
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
        if (string.Equals(abilityId, RenektonLegendAbilityId, StringComparison.Ordinal))
            return cardNo is not null && SourceCardNosForAbility(abilityId).Contains(cardNo, StringComparer.Ordinal);
        return !string.IsNullOrWhiteSpace(cardNo)
            && SourceCardNosForAbility(abilityId).Contains(cardNo.Trim(), StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSourceCardNosByRepresentativeCardNo()
    {
        return OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(
            RepresentativeSourceCardNoByAbilityId.Values);
    }
}
