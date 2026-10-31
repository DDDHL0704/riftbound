using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static ManaPaymentPurpose ManaPurposeForActivatedAbility(MatchState state, string sourceId)
        => state.CardObjects.TryGetValue(sourceId, out var source) && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, sourceId) ? ManaPaymentPurpose.UnitAbility : ManaPaymentPurpose.Other;

    internal static PowerPaymentPurpose PowerPurposeForActivatedAbility(P4ActivatedAbilityDefinition ability)
        => ability.RequiresBaseEquipmentSource || ability.SourceRequiredTag == CardObjectTags.EquipmentCard
            ? PowerPaymentPurpose.Equipment : PowerPaymentPurpose.Other;

    private sealed record EquipmentCostAmounts(int Mana, int GenericPower,
        IReadOnlyDictionary<string, int> PowerByTrait, int Experience);

    // Equip and effects referencing Equip use the same adjusted amounts. The
    // Weaponmaster reduction applies only to generic Power (current core 821).
    private static EquipmentCostAmounts EquipmentCosts(MatchState state,
        AssembleEquipmentProfile profile, CardObjectState host, int genericPowerReduction = 0, bool exhaustUnitInstead = false)
        => exhaustUnitInstead ? new(0, 0, new Dictionary<string, int>(), 0)
            : new(AssembleEquipmentManaCost(state, profile, host),
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

    internal static bool CanExhaustUnitForEquipmentCost(MatchState state, string player, string id)
        => IsPlayerControlledFieldUnitObject(state, player, id)
            && state.CardObjects.TryGetValue(id, out var unit) && !unit.IsFaceDown && !unit.IsExhausted;

    internal static string[] EquipmentExhaustCostChoices(MatchState state, string player)
        => state.CardObjects.Keys.Where(id => CanExhaustUnitForEquipmentCost(state, player, id))
            .Order(StringComparer.Ordinal).Select(id => ExhaustFriendlyUnitOptionalCostPrefix + id).ToArray();

    private static string? SelectedEquipmentExhaustCost(AssembleEquipmentProfile profile, IReadOnlyList<string> costs)
        => profile.HasExhaustUnitAlternative && costs.Count == 1
            && TryParseExhaustFriendlyUnitOptionalCost(costs[0], out var id) ? id : null;

    private static GameEvent ExhaustEquipmentCost(IReadOnlyDictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards, string id, string source, string player)
    {
        cards[id] = ExhaustForInstruction(zones, cards, cards[id]);
        return new("UNIT_EXHAUSTED", "支付装配费用，让友方单位变为休眠状态", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = source, ["targetObjectId"] = id,
            ["wasExhausted"] = false, ["isExhausted"] = true, ["reason"] = "ASSEMBLE_COST" });
    }
}
