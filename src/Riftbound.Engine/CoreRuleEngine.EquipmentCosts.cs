namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record EquipmentCostAmounts(int Mana, int GenericPower,
        IReadOnlyDictionary<string, int> PowerByTrait, int Experience);

    // Equip and effects referencing Equip use the same adjusted amounts. The
    // Weaponmaster reduction applies only to generic Power (current core 821).
    private static EquipmentCostAmounts EquipmentCosts(MatchState state,
        AssembleEquipmentProfile profile, CardObjectState host, int genericPowerReduction = 0)
        => new(AssembleEquipmentManaCost(state, profile, host),
            Math.Max(0, AssembleEquipmentAnyPowerCost(profile) - Math.Max(0, genericPowerReduction)),
            AssembleEquipmentPowerCostByTrait(profile), profile.ExperienceCost);

    private static PaymentCostRules.PaymentPlan EquipmentPaymentPlan(EquipmentCostAmounts amounts,
        AssembleEquipmentProfile profile, string paymentId, string paymentWindow, string player,
        string sourceId, IReadOnlyList<string> costIds, IReadOnlyList<string> resourceActions)
        => new(paymentId, paymentWindow, player,
            baseManaCost: profile.ManaCost, totalManaCost: amounts.Mana,
            genericPowerCost: amounts.GenericPower,
            totalPowerCost: amounts.GenericPower + amounts.PowerByTrait.Values.Sum(),
            powerCostByTrait: amounts.PowerByTrait, experienceCost: amounts.Experience,
            optionalCostIds: costIds, paymentResourceActionIds: resourceActions,
            reason: paymentWindow, sourceObjectId: sourceId, powerPurpose: PowerPaymentPurpose.Equipment);
}
