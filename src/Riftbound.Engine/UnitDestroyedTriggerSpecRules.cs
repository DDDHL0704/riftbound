using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitDestroyedTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByKind =
        new(BuildTriggerKindMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetTrigger(string? cardNo, Func<TriggerSpec, bool> predicate, out TriggerSpec trigger)
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

        var match = triggers.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    public static bool TryGetTriggerByKind(
        string? kind,
        Func<TriggerSpec, bool> predicate,
        out TriggerSpec trigger)
    {
        trigger = default!;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        if (!TriggersByKind.Value.TryGetValue(kind.Trim(), out var triggers))
        {
            return false;
        }

        var match = triggers.FirstOrDefault(predicate);
        if (match is null)
        {
            return false;
        }

        trigger = match;
        return true;
    }

    public static bool IsFriendlyDestroyedGainExperienceTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitFriendlyDestroyedGainExperience, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OtherFriendlyDestroyedUnit, StringComparison.Ordinal)
            && trigger.ExperienceCount is > 0;
    }

    public static bool IsFriendlyDestroyedPowerUntilEndTrigger(TriggerSpec trigger)
    {
        return string.Equals(
                trigger.Kind,
                TriggerKinds.UnitFriendlyDestroyedPowerUntilEndOfTurn,
                StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OtherFriendlyDestroyedUnit, StringComparison.Ordinal)
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal)
            && trigger.PowerDelta is not null;
    }

    public static bool IsFirstFriendlyDestroyedDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitFirstFriendlyDestroyedDrawOne, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OtherFriendlyDestroyedUnit, StringComparison.Ordinal)
            && trigger.DrawCount is > 0
            && trigger.OncePerTurn == true;
    }

    public static bool IsDestroyedNonMinionCreateMinionTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitDestroyedNonMinionCreateMinion, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OtherFriendlyDestroyedUnit, StringComparison.Ordinal)
            && trigger.ExcludesTokens == true
            && trigger.CreatedTokenCount is > 0
            && string.Equals(trigger.CreatedTokenName, "随从", StringComparison.Ordinal)
            && trigger.CreatedTokenPower is > 0
            && string.Equals(
                trigger.CreatedTokenDestination,
                TriggerTokenDestinations.OwnerBase,
                StringComparison.Ordinal);
    }

    public static bool IsLastBreathDrawIfAloneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathDrawIfAlone, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.DrawCount is > 0
            && trigger.RequiresNoOtherFriendlyUnitAtSamePosition == true;
    }

    public static bool IsLastBreathDrawOneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathDrawOne, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.DrawCount is > 0;
    }

    public static bool IsLastBreathCallRuneOneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathCallRuneOne, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.RuneCallCount is > 0;
    }

    public static bool IsLastBreathCreateDormantGoldTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathCreateDormantGold, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.CreatedTokenCount is > 0
            && !string.IsNullOrWhiteSpace(trigger.CreatedTokenName)
            && string.Equals(
                trigger.CreatedTokenDestination,
                TriggerTokenDestinations.OwnerBase,
                StringComparison.Ordinal)
            && trigger.CreatedTokenExhausted == true;
    }

    public static bool IsLastBreathDiscardDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathDiscardDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.DiscardCount is > 0
            && trigger.DrawCount is > 0;
    }

    public static bool IsLastBreathPowerfulDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathPowerfulDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.DrawCount is > 0
            && trigger.RequiredPowerThreshold is > 0;
    }

    public static bool IsLastBreathSourceBattlefieldAoeDamageTrigger(TriggerSpec trigger)
    {
        return string.Equals(
                trigger.Kind,
                TriggerKinds.UnitLastBreathDamageSourceBattlefieldUnits,
                StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceBattlefieldUnits, StringComparison.Ordinal)
            && trigger.DamageAmount is > 0;
    }

    public static bool IsLastBreathCreateBaseUnitTrigger(TriggerSpec trigger)
    {
        return IsLastBreathCreateBaseUnitEffectKind(trigger.Kind)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.CreatedTokenCount is > 0
            && !string.IsNullOrWhiteSpace(trigger.CreatedTokenName)
            && trigger.CreatedTokenPower is > 0
            && string.Equals(
                trigger.CreatedTokenDestination,
                TriggerTokenDestinations.OwnerBase,
                StringComparison.Ordinal);
    }

    public static bool IsLastBreathCreateBaseUnitEffectKind(string? effectKind)
    {
        return string.Equals(effectKind, TriggerKinds.UnitLastBreathCreateMinions, StringComparison.Ordinal)
            || string.Equals(effectKind, TriggerKinds.UnitLastBreathCreateRobots, StringComparison.Ordinal)
            || string.Equals(effectKind, TriggerKinds.UnitLastBreathCreateWarhawk, StringComparison.Ordinal);
    }

    public static bool IsLastBreathDrawIfNotAloneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitLastBreathDrawIfNotAlone, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitDestroyed, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.DrawCount is > 0
            && trigger.RequiresOtherFriendlyUnitAtSamePosition == true;
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

    private static IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>> BuildTriggerKindMap()
    {
        return TriggersByCardNo.Value
            .Values
            .SelectMany(triggers => triggers)
            .GroupBy(trigger => trigger.Kind, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TriggerSpec>)group.ToArray(),
                StringComparer.Ordinal);
    }
}
