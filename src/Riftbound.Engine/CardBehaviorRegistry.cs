namespace Riftbound.Engine;

public sealed record CardBehaviorDefinition(
    string CardNo,
    string DisplayName,
    int ManaCost,
    string EffectKind,
    int DamageAmount,
    int RequiredTargetCount,
    string DamageConditionKind = CardDamageConditionKinds.None,
    int ConditionalDamageAmount = 0,
    string StatusEffectId = "",
    int DrawCount = 0,
    string TargetScope = CardTargetScopes.BattlefieldUnit,
    int MinTargetCount = -1,
    string Mode = "",
    int EchoManaCost = 0);

public static class CardDamageConditionKinds
{
    public const string None = "NONE";
    public const string ControllerHasFaceDownCard = "CONTROLLER_HAS_FACE_DOWN_CARD";
}

public static class CardTargetScopes
{
    public const string BattlefieldUnit = "BATTLEFIELD_UNIT";
    public const string BaseUnit = "BASE_UNIT";
    public const string AnyUnit = "ANY_UNIT";
}

public static class CardBehaviorRegistry
{
    private static readonly CardBehaviorDefinition[] Definitions =
    [
        new(
            "UNL-007/219",
            "惩戒",
            2,
            "PUNISHMENT_DAMAGE_3",
            3,
            1,
            TargetScope: CardTargetScopes.AnyUnit),
        new(
            "UNL-014/219",
            "渊海狩咒",
            1,
            "ABYSSAL_HUNT_DAMAGE_2",
            2,
            1,
            CardDamageConditionKinds.ControllerHasFaceDownCard,
            4),
        new(
            "OGS·003/024",
            "焚烧",
            2,
            "INCINERATE_DAMAGE_2",
            2,
            1),
        new(
            "OGN·050/298",
            "符文禁锢",
            2,
            "RUNE_PRISON_STUN_UNIT",
            0,
            1,
            CardDamageConditionKinds.None,
            0,
            "STUNNED",
            TargetScope: CardTargetScopes.AnyUnit),
        new(
            "OGN·009/298",
            "海克斯射线",
            1,
            "HEXTECH_RAY_DAMAGE_3",
            3,
            1),
        new(
            "OGN·085/298",
            "彗星坠击",
            5,
            "COMET_STRIKE_DAMAGE_6",
            6,
            1),
        new(
            "OGS·022/024",
            "终极闪光",
            8,
            "FINAL_SPARK_DAMAGE_8",
            8,
            1,
            TargetScope: CardTargetScopes.AnyUnit),
        new(
            "UNL-061/219",
            "台前作秀",
            2,
            "CENTER_STAGE_DRAW_1",
            0,
            0,
            DrawCount: 1,
            EchoManaCost: 2),
        new(
            "OGN·105/298",
            "星芒凝汇",
            6,
            "STELLAR_CONVERGENCE_DAMAGE_6_UP_TO_2",
            6,
            2,
            TargetScope: CardTargetScopes.AnyUnit,
            MinTargetCount: 1),
        new(
            "SFD·077/221",
            "火箭轰击",
            4,
            "ROCKET_BARRAGE_BASE_UNIT_DAMAGE_4",
            4,
            1,
            TargetScope: CardTargetScopes.BaseUnit,
            Mode: "BASE_UNIT_DAMAGE_4"),
        new(
            "OGN·024/298",
            "虚空索敌",
            3,
            "VOID_SEEKER_DAMAGE_4_DRAW_1",
            4,
            1,
            CardDamageConditionKinds.None,
            0,
            "",
            1),
        new(
            "SFD·087/221",
            "先知之兆",
            2,
            "PROPHETS_OMEN_DRAW_3",
            0,
            0,
            DrawCount: 3),
        new(
            "OGN·114/298",
            "进化日",
            6,
            "EVOLUTION_DAY_DRAW_4",
            0,
            0,
            DrawCount: 4)
    ];

    public static bool TryGetByCardNo(string cardNo, out CardBehaviorDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
            candidate.CardNo,
            cardNo,
            StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool TryGetByCardNoAndMode(
        string cardNo,
        string mode,
        out CardBehaviorDefinition definition)
    {
        var normalizedMode = NormalizeMode(mode);
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.CardNo, cardNo, StringComparison.Ordinal)
            && string.Equals(NormalizeMode(candidate.Mode), normalizedMode, StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool TryGetByEffectKind(string effectKind, out CardBehaviorDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
            candidate.EffectKind,
            effectKind,
            StringComparison.Ordinal))!;
        return definition is not null;
    }

    private static string NormalizeMode(string? mode)
    {
        return string.IsNullOrWhiteSpace(mode) ? string.Empty : mode.Trim();
    }
}
