using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class UnitMovedTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

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

    public static IReadOnlyList<TriggerSpec> MovementTriggers(string? cardNo)
        => cardNo is not null && TriggersByCardNo.Value.TryGetValue(cardNo, out var triggers)
            ? triggers.Where(IsImplementedMovementTrigger).ToArray() : [];

    public static bool IsImplementedMovementTrigger(TriggerSpec trigger)
        => IsUnitMovedCreateDormantGoldTrigger(trigger) || IsOpponentMovementDrawTrigger(trigger)
            || IsUnitMovedRevealTopDrawUnitOrTrashPowerTrigger(trigger)
            || IsUnitMovedDrawDiscardTypesTrigger(trigger)
            || trigger.Timing == TriggerTimings.UnitMoved && trigger.TargetScope == TriggerTargetScopes.SourceUnit
                && (IsMovementUnitTokenTrigger(trigger)
                    || trigger.Kind == TriggerKinds.UnitMovedDiscardDraw && trigger.DiscardCount > 0 && trigger.DrawCount > 0
                    || trigger.Kind == TriggerKinds.UnitMovedDraw && trigger.DrawCount > 0
                    || trigger.Kind == TriggerKinds.UnitMovedGainExperience && trigger.ExperienceCount > 0
                    || trigger.Kind == TriggerKinds.UnitMovedPowerModifier && trigger.PowerDelta > 0
                        && trigger.Duration == TriggerDurations.UntilEndOfTurn);

    public static bool IsUnitMovedDrawDiscardTypesTrigger(TriggerSpec trigger)
        => trigger.Kind == TriggerKinds.UnitMovedDrawDiscardTypes && trigger.Timing == TriggerTimings.UnitMoved
            && trigger.TargetScope == TriggerTargetScopes.SourceUnit && trigger.DrawCount == 1 && trigger.DiscardCount == 1
            && trigger.RuneReadyCount == 2 && trigger.PowerDelta == 3 && trigger.Duration == TriggerDurations.UntilEndOfTurn;

    public static bool IsOpponentMovementDrawTrigger(TriggerSpec trigger)
        => trigger.Kind == TriggerKinds.OpponentMovedToOtherBattlefieldDraw && trigger.Timing == TriggerTimings.UnitMoved
            && trigger.TargetScope == TriggerTargetScopes.MovedUnit && trigger.DrawCount > 0
            && trigger.MovementDestinationZone == "BATTLEFIELD";

    public static bool IsUnitMovedRevealTopDrawUnitOrTrashPowerTrigger(TriggerSpec trigger)
        => trigger.Kind == TriggerKinds.UnitMovedRevealTopDrawUnitOrTrashPower && trigger.Timing == TriggerTimings.UnitMoved
            && trigger.TargetScope == TriggerTargetScopes.SourceUnit && trigger.RevealCount == 1 && trigger.DrawCount == 1
            && trigger.PowerDelta > 0 && trigger.Duration == TriggerDurations.UntilEndOfTurn
            && trigger.RevealSourceZone == TriggerZones.MainDeck
            && trigger.RevealMatchCardFilter == TriggerCardFilters.TagPrefix + CardObjectTags.UnitCard
            && trigger.RevealMatchDestinationZone == TriggerZones.Hand && trigger.RevealMissDestinationZone == TriggerZones.Graveyard;

    public static bool IsMovementUnitTokenTrigger(TriggerSpec trigger)
        => trigger.Kind == TriggerKinds.UnitMovedCreateUnitToken && trigger.CreatedTokenCount > 0
            && trigger.CreatedTokenExhausted == true
            && trigger.CreatedTokenDestination is TriggerTokenDestinations.MovementOrigin or TriggerTokenDestinations.SourceLocation
            && (trigger.CreatedTokenName == "随从" && trigger.CreatedTokenPower == 1
                || trigger.CreatedTokenName == "精灵" && trigger.CreatedTokenPower == 3
                    && trigger.CreatedTokenKeywords?.Contains("瞬息") == true);

    public static bool MatchesMovement(TriggerSpec trigger, MovedUnitFact unit)
        => (trigger.MovementOriginZone is null || trigger.MovementOriginZone == unit.Origin.Zone)
            && (trigger.MovementDestinationZone is null || trigger.MovementDestinationZone == unit.Destination.Zone);

    public static bool IsUnitMovedCreateDormantGoldTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.UnitMovedCreateDormantGold, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.UnitMoved, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SourceUnit, StringComparison.Ordinal)
            && trigger.CreatedTokenCount is > 0
            && !string.IsNullOrWhiteSpace(trigger.CreatedTokenName)
            && string.Equals(
                trigger.CreatedTokenDestination,
                TriggerTokenDestinations.OwnerBase,
                StringComparison.Ordinal);
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
