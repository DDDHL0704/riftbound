using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class BattlefieldStaticAbilitySpecRules
{
    public static bool TryGetAbility(string? cardNo, Func<StaticAbilitySpec, bool> predicate, out StaticAbilitySpec ability)
    {
        return CardStaticAbilitySpecRules.TryGetStaticAbility(cardNo, predicate, out ability);
    }

    public static bool IsBattlefieldWinningScoreIncreaseAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.BattlefieldWinningScoreIncrease,
                StringComparison.Ordinal)
            && ability.Amount > 0;
    }

    public static bool TryGetBattlefieldPreventMoveToBaseAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldPreventMoveToBase,
            out ability);
    }

    public static bool TryGetBattlefieldPreventUnitPlayAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldPreventUnitPlay,
            out ability);
    }

    public static bool TryGetBattlefieldEchoCostReductionAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldEchoCostReduction,
            out ability);
    }

    public static bool TryGetBattlefieldEquipmentCostReductionAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldEquipmentCostReduction,
            out ability);
    }

    public static bool TryGetBattlefieldGrantUnitExperienceAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility,
            out ability);
    }

    public static bool TryGetBattlefieldTargetSpellSkillDamageBonusAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus,
            out ability);
    }

    public static bool TryGetBattlefieldGrantLegendAttachArmamentAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldGrantLegendAttachArmament,
            out ability);
    }

    public static bool TryGetBattlefieldStaticAbility(string? cardNo, string kind, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(cardNo, kind, out ability);
    }

    public static bool TryGetBattlefieldScoreDelayUntilTurnAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldScoreDelayUntilTurn,
            out ability);
    }

    public static bool TryGetBattlefieldExtraStandbyDestinationAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldExtraStandbyDestination,
            out ability);
    }

    public static bool TryGetBattlefieldDestroyedInBattlePayRecallReplacementAbility(string? cardNo, out StaticAbilitySpec ability)
    {
        return TryGetStaticAbility(
            cardNo,
            StaticAbilityKinds.BattlefieldDestroyedInBattlePayRecallReplacement,
            out ability);
    }

    private static bool TryGetStaticAbility(string? cardNo, string kind, out StaticAbilitySpec ability)
    {
        return CardStaticAbilitySpecRules.TryGetStaticAbility(cardNo, kind, out ability);
    }
}
