namespace Riftbound.Engine;

public sealed record P6TokenFactoryDefinition(
    string CardNo,
    string CardName,
    string TokenFamilyName,
    string CategoryName,
    int DefaultPower,
    IReadOnlyList<string> Tags,
    bool RequiresCopySource,
    string Reason)
{
    public CardObjectState CreateObject(
        string objectId,
        string ownerId,
        string controllerId,
        bool isExhausted = false)
    {
        return new CardObjectState(
            objectId,
            power: DefaultPower,
            isExhausted: isExhausted,
            tags: Tags,
            cardNo: CardNo,
            ownerId: ownerId,
            controllerId: controllerId);
    }
}

public sealed record P6DeferredTokenRuleSurface(
    string SurfaceId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchor,
    string SurfaceKind,
    bool IsActivatedCommandSurface,
    int TargetCount,
    string Reason);

public sealed record P6ImplementedTokenRuleSurface(
    string SurfaceId,
    string SourceCardNo,
    string DisplayName,
    string OfficialTextAnchor,
    string SurfaceKind,
    bool IsActivatedCommandSurface,
    int TargetCount,
    string Reason);

public static class P6TokenFactoryCatalog
{
    public const string BaronNestTokenCardNo = "UNL·T01";
    public const string BaronNestMoveStaticSurfaceId = "TOKEN_DEFERRED_BARON_NEST_MOVE_STATIC";
    public const string ImageTokenCardNo = "UNL·T06";
    public const string ImageCopySurfaceId = "TOKEN_DEFERRED_IMAGE_COPY_SOURCE_REQUIRED";
    public const string BattlefieldCardTag = "CARD_TYPE:BATTLEFIELD";
    public const string CopySourceRequiredTag = "COPY_SOURCE_REQUIRED";
    public const string ActivatedResourceSurfaceKind = "activated-resource";
    public const string CopyTokenSurfaceKind = "copy-token";
    public const string BattlefieldReplacementSurfaceKind = "battlefield-replacement";
    public const string BattlefieldStaticSurfaceKind = "battlefield-static";

    private static readonly P6TokenFactoryDefinition[] Definitions =
    [
        Battlefield(BaronNestTokenCardNo, "男爵巢穴", "男爵巢穴"),
        Unit("UNL·T02", "战鹰", "战鹰", 1, CardObjectTags.Spellshield, "鸟类"),
        Battlefield("UNL·T03", "草丛", "草丛"),
        Equipment("UNL·T05", "金币", "金币", "反应"),
        Unit(ImageTokenCardNo, "映像", "映像", 0, true, CopySourceRequiredTag),
        Unit("UNL·T07", "精灵", "精灵", 3, CardObjectTags.Ephemeral, "仙灵"),
        Unit("SFD·T01", "机器人", "机器人", 3, "机械"),
        Unit("SFD·T02", "黄沙士兵", "黄沙士兵", 2, CardObjectTags.SandSoldier),
        Equipment("SFD·T03", "金币", "金币", "反应"),
        Unit("OGN·271/298", "随从（德玛西亚）", "随从", 1, CardObjectTags.MinionTokenFamily),
        Unit("OGN·272/298", "随从（诺克萨斯）", "随从", 1, CardObjectTags.MinionTokenFamily),
        Unit("OGN·273/298", "随从（祖安）", "随从", 1, CardObjectTags.MinionTokenFamily),
        Unit("OGN·274/298", "精灵", "精灵", 3, CardObjectTags.Ephemeral)
    ];

    private static readonly P6DeferredTokenRuleSurface[] DeferredRuleSurfaces =
    [
        new(
            "TOKEN_DEFERRED_BRUSH_BATTLEFIELD_REPLACEMENT",
            "UNL·T03",
            "Brush battlefield replacement token",
            "当你在此处得分时，你可以选择使用被此牌替代的战场来替代此牌",
            BattlefieldReplacementSurfaceKind,
            IsActivatedCommandSurface: false,
            TargetCount: 0,
            "P6.11 keeps token battlefield replacement deferred until battlefield replacement ordering and original-location memory are modeled."),
    ];

    private static readonly P6ImplementedTokenRuleSurface[] ImplementedRuleSurfaces =
    [
        new(
            ImageCopySurfaceId,
            ImageTokenCardNo,
            "Image copy-token entry replacement",
            "当我被打出时，变为某张卡牌的复制体",
            CopyTokenSurfaceKind,
            IsActivatedCommandSurface: false,
            TargetCount: 1,
            "P6.11 retired this deferred representative after the token factory domain implemented Image copy-token CardNo, power, tags, and copy audit metadata."),
        new(
            BaronNestMoveStaticSurfaceId,
            BaronNestTokenCardNo,
            "Baron Nest movement static token",
            "单位可从任意位置移动到此处",
            BattlefieldStaticSurfaceKind,
            IsActivatedCommandSurface: false,
            TargetCount: 0,
            "P6.11 retired this deferred representative after the battlefield movement domain implemented Baron Nest destination-specific movement.")
    ];

    public static IReadOnlyList<P6TokenFactoryDefinition> GetAll()
    {
        return Definitions;
    }

    public static IReadOnlyList<P6DeferredTokenRuleSurface> GetDeferredRuleSurfaces()
    {
        return DeferredRuleSurfaces;
    }

    public static IReadOnlyList<P6ImplementedTokenRuleSurface> GetImplementedRuleSurfaces()
    {
        return ImplementedRuleSurfaces;
    }

    public static bool TryGetByCardNo(
        string cardNo,
        out P6TokenFactoryDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
            candidate.CardNo,
            cardNo,
            StringComparison.Ordinal))!;
        return definition is not null;
    }

    private static P6TokenFactoryDefinition Unit(
        string cardNo,
        string cardName,
        string tokenFamilyName,
        int power,
        params string[] tags)
    {
        return Unit(cardNo, cardName, tokenFamilyName, power, requiresCopySource: false, tags);
    }

    private static P6TokenFactoryDefinition Unit(
        string cardNo,
        string cardName,
        string tokenFamilyName,
        int power,
        bool requiresCopySource,
        params string[] tags)
    {
        return new P6TokenFactoryDefinition(
            cardNo,
            cardName,
            tokenFamilyName,
            "指示物单位",
            power,
            DistinctTags([CardObjectTags.UnitCard, .. tags]),
            requiresCopySource,
            "P6.11 binds this official unit token identity to the object factory domain; effect-specific creation timing remains owned by the source card behavior.");
    }

    private static P6TokenFactoryDefinition Equipment(
        string cardNo,
        string cardName,
        string tokenFamilyName,
        params string[] tags)
    {
        return new P6TokenFactoryDefinition(
            cardNo,
            cardName,
            tokenFamilyName,
            "指示物装备",
            0,
            DistinctTags([CardObjectTags.EquipmentCard, .. tags]),
            RequiresCopySource: false,
            "P6.11 binds this official equipment token identity to the object factory domain; activated resource timing remains separately deferred unless a source behavior implements it.");
    }

    private static P6TokenFactoryDefinition Battlefield(
        string cardNo,
        string cardName,
        string tokenFamilyName)
    {
        return new P6TokenFactoryDefinition(
            cardNo,
            cardName,
            tokenFamilyName,
            "指示物战场",
            0,
            [BattlefieldCardTag],
            RequiresCopySource: false,
            "P6.11 binds this official battlefield token identity to the object factory domain; battlefield replacement and location effects remain in the P6.10 domain.");
    }

    private static IReadOnlyList<string> DistinctTags(IReadOnlyList<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
