using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitDestroyedTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> TriggersByKind =
        new(BuildTriggerKindMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetFriendlyDestroyedGainExperienceTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitFriendlyDestroyedGainExperience,
            out trigger);
    }

    public static bool TryGetFriendlyDestroyedPowerUntilEndTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitFriendlyDestroyedPowerUntilEndOfTurn,
            out trigger);
    }

    public static bool TryGetFirstFriendlyDestroyedDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitFirstFriendlyDestroyedDrawOne,
            out trigger);
    }

    public static bool TryGetDestroyedNonMinionCreateMinionTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitDestroyedNonMinionCreateMinion,
            out trigger);
    }

    public static bool TryGetLastBreathDrawIfAloneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathDrawIfAlone,
            out trigger);
    }

    public static bool TryGetLastBreathDrawOneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathDrawOne,
            out trigger);
    }

    public static bool TryGetLastBreathCallRuneOneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathCallRuneOne,
            out trigger);
    }

    public static bool TryGetLastBreathCreateDormantGoldTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathCreateDormantGold,
            out trigger);
    }

    public static bool TryGetLastBreathDiscardDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathDiscardDraw,
            out trigger);
    }

    public static bool TryGetLastBreathPowerfulDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathPowerfulDraw,
            out trigger);
    }

    public static bool TryGetLastBreathCreateBaseUnitTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
                cardNo,
                TriggerKinds.UnitLastBreathCreateMinions,
                out trigger)
            || TryGetTrigger(
                cardNo,
                TriggerKinds.UnitLastBreathCreateRobots,
                out trigger)
            || TryGetTrigger(
                cardNo,
                TriggerKinds.UnitLastBreathCreateWarhawk,
                out trigger);
    }

    public static bool TryGetLastBreathDrawIfNotAloneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.UnitLastBreathDrawIfNotAlone,
            out trigger);
    }

    public static bool TryGetTrigger(string? cardNo, string kind, out TriggerSpec trigger)
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

    public static bool TryGetTriggerByKind(string? kind, out TriggerSpec trigger)
    {
        trigger = default!;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        if (!TriggersByKind.Value.TryGetValue(kind.Trim(), out var match))
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

    private static IReadOnlyDictionary<string, TriggerSpec> BuildTriggerKindMap()
    {
        return TriggersByCardNo.Value
            .Values
            .SelectMany(triggers => triggers)
            .GroupBy(trigger => trigger.Kind, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
    }
}
