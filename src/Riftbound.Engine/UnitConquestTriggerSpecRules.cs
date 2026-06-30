using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitConquestTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetUnitConquestDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestDrawOne,
            out trigger);
    }

    public static bool TryGetUnitConquestDrawOrCallRuneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestDrawOneOrCallRune,
            out trigger);
    }

    public static bool TryGetUnitConquestCreateDormantGoldTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestCreateDormantGold,
            out trigger);
    }

    public static bool TryGetUnitConquestOverkillCreateDormantGoldTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestOverkillCreateDormantGold,
            out trigger);
    }

    public static bool TryGetUnitConquestAttackOverkillGainScoreTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestAttackOverkillGainScore,
            out trigger);
    }

    public static bool TryGetUnitConquestPayReturnSelfToHandTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(cardNo, TriggerKinds.UnitConquestPayReturnSelfToHand, out trigger)
            && IsUnitConquestPayReturnSelfToHandTrigger(trigger);
    }

    public static bool TryGetUnitConquestPayReturnSelfToHandTriggerByEffectKind(
        string? effectKind,
        out TriggerSpec trigger)
    {
        return TryGetTriggerByEffectKind(effectKind, IsUnitConquestPayReturnSelfToHandTrigger, out trigger);
    }

    public static bool TryGetUnitConquestGrantSelfBoonTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestGrantSelfBoon,
            out trigger);
    }

    public static bool TryGetUnitConquestReadySelfOnceTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestReadySelfOncePerTurn,
            out trigger);
    }

    public static bool TryGetUnitConquestGrantFriendlyBoonTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestGrantFriendlyBoon,
            out trigger);
    }

    public static bool TryGetUnitConquestAdditionalActivationTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestAdditionalActivation,
            out trigger);
    }

    public static bool TryGetUnitConquestFriendlyPowerUntilEndTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestFriendlyPowerUntilEndOfTurn,
            out trigger);
    }

    public static bool TryGetUnitConquestDestroyEquipmentGrantSelfBoonTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitConquestDestroyEquipmentGrantSelfBoon,
            out trigger);
    }

    private static bool IsUnitConquestPayReturnSelfToHandTrigger(TriggerSpec trigger)
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

    private static bool TryGetTriggerByEffectKind(
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

    private static string RuntimeEffectKind(TriggerSpec trigger)
    {
        return string.IsNullOrWhiteSpace(trigger.EffectKind)
            ? trigger.Kind
            : trigger.EffectKind!;
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
