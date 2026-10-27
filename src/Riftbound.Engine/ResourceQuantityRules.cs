namespace Riftbound.Engine;

internal static class ResourceQuantityRules
{
    internal const int DirectChoiceLimit = 64;
    internal const string ChooseAmountSuffix = "CHOOSE_AMOUNT";
    internal static string? ConversionPrefix(string ability) => ability switch {
        P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId => P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix,
        P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId => P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix,
        _ => null };

    internal static bool TryConversionMaximum(RunePool pool, string ability, out int maximum)
    {
        maximum = 0;
        if (!pool.HasSupportedAmounts) return false;
        long amount;
        if (ability == P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId)
            amount = Math.Min((long)pool.Mana, (long)int.MaxValue - pool.TotalStoredPower);
        else if (ability == P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId)
            amount = Math.Min((long)PaymentCostRules.PowerPoolForPurpose(pool, PowerPaymentPurpose.Equipment).TotalPower,
                (long)int.MaxValue - pool.Mana - pool.SpellOnlyMana);
        else return false;
        maximum = (int)amount;
        return true;
    }
}
