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
    int DrawCount = 0);

public static class CardDamageConditionKinds
{
    public const string None = "NONE";
    public const string ControllerHasFaceDownCard = "CONTROLLER_HAS_FACE_DOWN_CARD";
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
            1),
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
            "STUNNED"),
        new(
            "OGN·009/298",
            "海克斯射线",
            1,
            "HEXTECH_RAY_DAMAGE_3",
            3,
            1),
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
            1)
    ];

    public static bool TryGetByCardNo(string cardNo, out CardBehaviorDefinition definition)
    {
        definition = Definitions.FirstOrDefault(candidate => string.Equals(
            candidate.CardNo,
            cardNo,
            StringComparison.Ordinal))!;
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
}
