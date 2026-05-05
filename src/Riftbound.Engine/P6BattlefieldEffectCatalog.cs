namespace Riftbound.Engine;

public sealed record P6DeferredBattlefieldEffectSurface(
    string SurfaceId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchor,
    string SurfaceKind,
    bool IsActivatedCommandSurface,
    int TargetCount,
    string Reason);

public static class P6BattlefieldEffectCatalog
{
    public const string ActivatedGrantSurfaceKind = "activated-grant";
    public const string TriggerSurfaceKind = "trigger";
    public const string ReplacementSurfaceKind = "replacement";
    public const string StaticKeywordSurfaceKind = "static-keyword";

    private static readonly P6DeferredBattlefieldEffectSurface[] DeferredSurfaces =
    [
        new(
            "BATTLEFIELD_DEFERRED_GRANT_UNIT_EXHAUST_GAIN_EXPERIENCE",
            "UNL-213/219",
            "Mutation Garden unit experience skill grant",
            "此处的单位获得“{{横置}}：获得1经验。”",
            ActivatedGrantSurfaceKind,
            IsActivatedCommandSurface: true,
            TargetCount: 0,
            "P6.10 keeps battlefield-granted unit activated abilities deferred until battlefield locality, granted source validation, exhaustion, and experience gain can be validated outside PLAY_CARD."),
        new(
            "BATTLEFIELD_DEFERRED_GRANT_LEGEND_EXHAUST_ATTACH_WEAPON",
            "SFD·208/221",
            "Poro Forge legend weapon attach skill grant",
            "所有友方传奇获得“{{横置}}：将你控制的一件武装贴附到你控制的一名单位上。”",
            ActivatedGrantSurfaceKind,
            IsActivatedCommandSurface: true,
            TargetCount: 2,
            "P6.10 keeps battlefield-granted legend attachment abilities deferred until battlefield control, legend source exhaustion, attachment legality, and equipment target rules are generalized."),
        new(
            "BATTLEFIELD_DEFERRED_FIRST_FRIENDLY_SPELL_DRAW",
            "OGN·292/298",
            "Dream Tree first friendly-unit spell draw trigger",
            "每回合首次：当你对此处的友方单位使用法术时，抽一张牌。",
            TriggerSurfaceKind,
            IsActivatedCommandSurface: false,
            TargetCount: 0,
            "P6.10 keeps first-per-turn battlefield spell triggers deferred until battlefield locality and per-location trigger memory are modeled."),
        new(
            "BATTLEFIELD_DEFERRED_DESTROYED_IN_BATTLE_REPLACEMENT_RECALL",
            "UNL-206/219",
            "Blood Altar battle destruction replacement",
            "如果此处的一名单位在战斗中被摧毁，其控制者可以选择支付{{A}}{{A}}{{A}}",
            ReplacementSurfaceKind,
            IsActivatedCommandSurface: false,
            TargetCount: 0,
            "P6.10 keeps battlefield destruction replacement effects deferred until replacement ordering, optional colored payments, sleep state, damage removal, and recall timing are modeled."),
        new(
            "BATTLEFIELD_DEFERRED_STATIC_KEYWORD_GRANT_EPHEMERAL_DEFENDER_BONUS",
            "UNL-208/219",
            "Blackflame Altar ephemeral defender keyword grant",
            "此处拥有{{瞬息}}的单位获得{{坚守}}",
            StaticKeywordSurfaceKind,
            IsActivatedCommandSurface: false,
            TargetCount: 0,
            "P6.10 keeps battlefield static keyword grants deferred until location-scoped continuous effects and combat-time defender modifiers are modeled.")
    ];

    public static IReadOnlyList<P6DeferredBattlefieldEffectSurface> GetDeferredSurfaces()
    {
        return DeferredSurfaces;
    }
}
