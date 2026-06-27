using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitTriggerPaymentSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetUnitArmamentAttachedPayDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(cardNo, TriggerKinds.UnitArmamentAttachedPayDraw, out trigger)
            && string.Equals(trigger.Timing, TriggerTimings.UnitArmamentAttached, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.FriendlyEquipment, StringComparison.Ordinal)
            && trigger.Optional == true
            && trigger.ManaCost is > 0
            && trigger.DrawCount is > 0;
    }

    public static bool TryGetUnitControlledUnitPowerfulPayPowerReadyTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(cardNo, TriggerKinds.UnitControlledUnitPowerfulPayPowerReady, out trigger)
            && string.Equals(trigger.Timing, TriggerTimings.ControlledUnitBecamePowerful, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledUnitOnField, StringComparison.Ordinal)
            && trigger.Optional == true
            && trigger.PowerCost is > 0
            && !string.IsNullOrWhiteSpace(trigger.PowerCostTrait)
            && trigger.RequiredPowerThreshold is > 0
            && trigger.UnitReadyCount is > 0;
    }

    private static bool TryGetTrigger(string? cardNo, string kind, out TriggerSpec trigger)
    {
        trigger = default!;
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return false;
        }

        if (!TriggersByCardNo.Value.TryGetValue(cardNo.Trim(), out var triggers))
        {
            return false;
        }

        var match = triggers.FirstOrDefault(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>> BuildTriggerMap()
    {
        var catalog = OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult();
        var units = FunctionalUnitBuilder.Build(catalog.Cards);
        var playCardBehaviors = CardBehaviorRegistry.GetAll()
            .Select(behavior => new ImplementedCardBehavior(
                behavior.CardNo,
                behavior.EffectKind,
                behavior.DisplayName))
            .ToArray();
        var implementedBehaviors = OfficialRuleDomainBehaviorCatalog.MergeWithNonPlayCardDomains(
            catalog.Cards,
            playCardBehaviors);

        return BehaviorSpecCatalogBuilder.Build(catalog.Cards, units, implementedBehaviors)
            .Where(spec => spec.Triggers.Count > 0)
            .ToDictionary(
                spec => spec.CardNo,
                spec => (IReadOnlyList<TriggerSpec>)spec.Triggers,
                StringComparer.Ordinal);
    }
}
