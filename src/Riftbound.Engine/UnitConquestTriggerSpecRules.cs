using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitConquestTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<TriggerSpec> TriggersForCard(string? cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return [];
        }

        return TriggersByCardNo.Value.TryGetValue(cardNo.Trim(), out var triggers)
            ? triggers
            : [];
    }

    public static bool TryGetTrigger(string? cardNo, Func<TriggerSpec, bool> predicate, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = TriggersForCard(cardNo).FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    public static bool TryGetTriggerByEffectKind(
        string? effectKind,
        Func<TriggerSpec, bool> predicate,
        out TriggerSpec trigger)
    {
        trigger = default!;
        if (string.IsNullOrWhiteSpace(effectKind))
        {
            return false;
        }

        var normalized = effectKind.Trim();
        var match = TriggersByCardNo.Value
            .SelectMany(entry => entry.Value)
            .FirstOrDefault(candidate =>
                predicate(candidate)
                && string.Equals(RuntimeEffectKind(candidate), normalized, StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    public static bool IsSupportedUnitConquestTrigger(TriggerSpec trigger)
    {
        if (!string.Equals(trigger.Timing, TriggerTimings.UnitConquest, StringComparison.Ordinal))
        {
            return false;
        }

        return trigger.Kind switch
        {
            TriggerKinds.UnitConquestOverkillCreateDormantGold => trigger.RequiredOverkillDamage is > 0
                && trigger.CreatedTokenCount is > 0,
            TriggerKinds.UnitConquestAttackOverkillGainScore => trigger.RequiredOverkillDamage is > 0
                && trigger.ScoreAmount is > 0,
            TriggerKinds.UnitConquestCreateDormantGold => trigger.CreatedTokenCount is > 0,
            TriggerKinds.UnitConquestDrawOne => trigger.DrawCount is > 0,
            TriggerKinds.UnitConquestDrawOneOrCallRune => trigger.DrawCount is > 0
                && trigger.RuneCallCount is > 0,
            TriggerKinds.UnitConquestGrantSelfBoon => string.Equals(
                trigger.TargetScope,
                TriggerTargetScopes.SourceUnit,
                StringComparison.Ordinal),
            TriggerKinds.UnitConquestReadySelfOncePerTurn => string.Equals(
                trigger.TargetScope,
                TriggerTargetScopes.SourceUnit,
                StringComparison.Ordinal),
            TriggerKinds.UnitConquestGrantFriendlyBoon => string.Equals(
                trigger.TargetScope,
                TriggerTargetScopes.ControlledUnitOnField,
                StringComparison.Ordinal),
            TriggerKinds.UnitConquestFriendlyPowerUntilEndOfTurn => string.Equals(
                    trigger.TargetScope,
                    TriggerTargetScopes.ControlledUnitOnField,
                    StringComparison.Ordinal)
                && trigger.PowerDelta is not null
                && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal),
            TriggerKinds.UnitConquestDestroyEquipmentGrantSelfBoon => string.Equals(
                    trigger.TargetScope,
                    TriggerTargetScopes.EquipmentOnField,
                    StringComparison.Ordinal)
                && trigger.DestroyCount is > 0
                && trigger.BoonCount is > 0,
            _ => false
        };
    }

    public static bool IsUnitConquestAdditionalActivationTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitConquestAdditionalActivation, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledUnitsAtThisBattlefield, StringComparison.Ordinal)
            && trigger.AdditionalTriggerCount is > 0;
    }

    public static bool IsUnitConquestPayReturnSelfToHandTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitConquestPayReturnSelfToHand, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitConquest, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.ManaCost is > 0
            && trigger.ReturnCount is > 0
            && string.Equals(trigger.ReturnOriginZone, TriggerZones.Battlefield, StringComparison.Ordinal)
            && string.Equals(trigger.ReturnDestinationZone, TriggerZones.Hand, StringComparison.Ordinal)
            && trigger.Optional == true;
    }

    private static string RuntimeEffectKind(TriggerSpec trigger)
    {
        return string.IsNullOrWhiteSpace(trigger.EffectKind)
            ? trigger.Kind
            : trigger.EffectKind!;
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
