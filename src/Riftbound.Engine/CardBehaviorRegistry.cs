namespace Riftbound.Engine;

public sealed record CardBehaviorDefinition(
    string CardNo,
    string DisplayName,
    int ManaCost,
    string EffectKind,
    int DamageAmount,
    int RequiredTargetCount);

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
