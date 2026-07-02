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

    public static bool IsBattlefieldScoreDelayUntilTurnAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.BattlefieldScoreDelayUntilTurn,
                StringComparison.Ordinal)
            && ability.Amount > 0;
    }

    public static bool IsBattlefieldPreventMoveToBaseAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
            ability.Kind,
            StaticAbilityKinds.BattlefieldPreventMoveToBase,
            StringComparison.Ordinal);
    }

    public static bool IsBattlefieldPreventUnitPlayAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
            ability.Kind,
            StaticAbilityKinds.BattlefieldPreventUnitPlay,
            StringComparison.Ordinal);
    }

    public static bool IsBattlefieldEchoCostReductionAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.BattlefieldEchoCostReduction,
                StringComparison.Ordinal)
            && ability.Amount > 0;
    }

    public static bool IsBattlefieldEquipmentCostReductionAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.BattlefieldEquipmentCostReduction,
                StringComparison.Ordinal)
            && ability.Amount > 0;
    }

    public static bool IsBattlefieldGrantUnitExperienceAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility,
                StringComparison.Ordinal)
            && ability.Amount > 0;
    }

    public static bool IsBattlefieldTargetSpellSkillDamageBonusAbility(StaticAbilitySpec ability)
    {
        return string.Equals(
                ability.Kind,
                StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus,
                StringComparison.Ordinal)
            && ability.Amount > 0;
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
