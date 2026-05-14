namespace Riftbound.Engine;

public sealed record P6DeferredLegendAbilitySurface(
    string AbilityId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchor,
    bool RequiresTarget,
    string Reason);

public sealed record P6ImplementedLegendActionSurface(
    string AbilityId,
    string RetiredDeferredSurfaceId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchor,
    bool RequiresTarget,
    string Reason);

public static class P6LegendAbilityCatalog
{
    private static readonly P6DeferredLegendAbilitySurface[] DeferredSurfaces = [];

    private static readonly P6ImplementedLegendActionSurface[] ImplementedLegendActionSurfaces =
    [
        new(
            "LEGEND_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
            "LEGEND_DEFERRED_PAY_2_EXHAUST_MOVE_FRIENDLY_UNIT",
            "FND-259/298",
            "Yasuo legend move skill",
            "支付{{2}}，{{横置}}：在战场和其所属基地之间移动一名友方单位",
            RequiresTarget: true,
            "P6.9 retired this deferred representative after LEGEND_ACTION_DOMAIN implemented source zone, exhaustion, target location, and movement timing."),
        new(
            "LEGEND_PAY_1_EXHAUST_GRANT_BOON",
            "LEGEND_DEFERRED_PAY_1_EXHAUST_GRANT_BOON",
            "OGN·257/298",
            "Lee Sin legend boon skill",
            "支付{{1}}，{{横置}}：给予一名友方单位增益",
            RequiresTarget: true,
            "P6.9 retired this deferred representative after LEGEND_ACTION_DOMAIN implemented legend source costs and boon target handling outside PLAY_CARD."),
        new(
            "LEGEND_SPELL_DUEL_EXHAUST_GAIN_1_MANA",
            "LEGEND_DEFERRED_REACTION_EXHAUST_GAIN_SPELL_DUEL_MANA",
            "UNL-234/219",
            "Diana legend spell-duel mana skill",
            "{{反应>}} {{横置}}：{{获得}}{{1}}",
            RequiresTarget: false,
            "P6.9 retired this deferred representative after LEGEND_ACTION_DOMAIN implemented spell-duel focus resource timing for legend sources."),
        new(
            "LEGEND_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
            "LEGEND_DEFERRED_SPEND_3_EXPERIENCE_EXHAUST_DRAW",
            "UNL-237/219",
            "Poppy legend experience draw skill",
            "消耗3经验，{{横置}}：抽一张牌",
            RequiresTarget: false,
            "P6.9 retired this deferred representative after LEGEND_ACTION_DOMAIN implemented legend source exhaustion and experience spending."),
        new(
            "LEGEND_PAY_1_EXHAUST_CREATE_MINION",
            "LEGEND_DEFERRED_PAY_1_EXHAUST_CREATE_MINION",
            "FND-265/298",
            "Viktor legend minion skill",
            "支付{{1}}，{{横置}}：打出一名1{{S}}的“随从”",
            RequiresTarget: false,
            "P6.9 retired this deferred representative after LEGEND_ACTION_DOMAIN connected token factories and legend source timing.")
    ];

    public static IReadOnlyList<P6DeferredLegendAbilitySurface> GetDeferredSurfaces()
    {
        return DeferredSurfaces;
    }

    public static IReadOnlyList<P6ImplementedLegendActionSurface> GetImplementedLegendActionSurfaces()
    {
        return ImplementedLegendActionSurfaces;
    }
}
