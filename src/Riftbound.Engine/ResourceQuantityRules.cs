namespace Riftbound.Engine;

internal static class ResourceQuantityRules
{
    internal const int DirectChoiceLimit = 64;
    internal const string ChooseAmountSuffix = "CHOOSE_AMOUNT";
    internal static string? ConversionPrefix(string ability) => ability switch {
        P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId => P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix,
        P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId => P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix,
        _ => null };

    internal static bool TryConversionMaximum(RunePool pool, string ability, out int maximum, MatchState? state = null)
    {
        maximum = 0;
        return (ability is P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId or P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId)
            && TryConversionMaximum(pool, ability == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId,
                ManaPaymentPurpose.Other, PowerPaymentPurpose.Equipment, out maximum, state);
    }

    internal static bool TryConversionMaximum(RunePool pool, bool manaToPower, ManaPaymentPurpose manaPurpose,
        PowerPaymentPurpose powerPurpose, out int maximum, MatchState? state = null)
    {
        maximum = 0;
        if (!pool.HasSupportedAmounts) return false;
        long amount;
        if (manaToPower)
            amount = Math.Min((long)PaymentCostRules.AvailableMana(pool, manaPurpose, state),
                (long)int.MaxValue - pool.TotalStoredPower);
        else
            amount = Math.Min((long)PaymentCostRules.PowerPoolForPurpose(pool, powerPurpose).TotalPower,
                (long)int.MaxValue - pool.TotalStoredMana);
        maximum = (int)amount;
        return true;
    }
}
