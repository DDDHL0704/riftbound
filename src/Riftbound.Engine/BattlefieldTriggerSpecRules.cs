using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class BattlefieldTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool HasImplementedBattlefieldTrigger(string? cardNo)
    {
        return TriggersForCard(cardNo).Any(IsImplementedBattlefieldTrigger);
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

    public static bool IsBattlefieldHeldPayPowerScoreTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldPayPowerScore, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && trigger.PowerCost is > 0
            && trigger.ScoreAmount is > 0;
    }

    public static bool IsBattlefieldHeldDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldDrawOne, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && trigger.DrawCount is > 0;
    }

    public static bool IsBattlefieldHeldCallRuneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldCallRune, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && trigger.RuneCallCount is > 0;
    }

    public static bool IsBattlefieldHeldUnitCostIncreaseTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldUnitCostIncrease, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal)
            && trigger.ManaDelta is > 0;
    }

    public static bool IsBattlefieldHeldNextSpellEchoTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldNextSpellEcho, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal);
    }

    public static bool IsBattlefieldHeldEachPlayerCallRuneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldEachPlayerCallRune, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.EachPlayer, StringComparison.Ordinal)
            && trigger.RuneCallCount is > 0;
    }

    public static bool IsBattlefieldHeldMoveUnitToBaseTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldMoveUnitToBase, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.UnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.MoveCount.GetValueOrDefault() == 1
            && string.Equals(trigger.MoveDestination, TriggerMoveDestinations.OwnerBase, StringComparison.Ordinal);
    }

    public static bool IsBattlefieldDefendMoveFriendlyUnitToBaseTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldDefendMoveFriendlyUnitToBase, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldDefended, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.FriendlyUnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.MoveCount.GetValueOrDefault() == 1
            && string.Equals(trigger.MoveDestination, TriggerMoveDestinations.OwnerBase, StringComparison.Ordinal)
            && trigger.Optional == true;
    }

    public static bool IsBattlefieldDefendGrantSteadfastTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldDefendGrantSteadfast, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldDefended, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.DefenderUnitAtThisBattlefield, StringComparison.Ordinal)
            && string.Equals(trigger.GrantedKeyword, CardCombatKeywordNames.Steadfast, StringComparison.Ordinal)
            && trigger.KeywordBonus.GetValueOrDefault() > 0;
    }

    public static bool IsBattlefieldHeldGrantBoonTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldGrantBoon, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.UnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.BoonCount.GetValueOrDefault() == 1;
    }

    public static bool IsBattlefieldHeldCreateMinionTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldCreateMinion, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && trigger.CreatedTokenCount is > 0
            && !string.IsNullOrWhiteSpace(trigger.CreatedTokenName)
            && trigger.CreatedTokenPower is > 0
            && string.Equals(
                trigger.CreatedTokenDestination,
                TriggerTokenDestinations.OwnerBase,
                StringComparison.Ordinal);
    }

    public static bool IsBattlefieldHeldReturnHeroTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldReturnHero, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OwnedHeroUnitInGraveyard, StringComparison.Ordinal)
            && trigger.ReturnCount is > 0
            && string.Equals(trigger.RequiredEmptyZone, TriggerZones.Champion, StringComparison.Ordinal)
            && string.Equals(trigger.ReturnOriginZone, TriggerZones.Graveyard, StringComparison.Ordinal)
            && string.Equals(trigger.ReturnDestinationZone, TriggerZones.Champion, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(trigger.ReturnCardFilter);
    }

    public static bool IsBattlefieldHeldSevenUnitsWinTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldSevenUnitsWin, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledUnitsAtThisBattlefield, StringComparison.Ordinal)
            && trigger.RequiredUnitCount is > 0
            && trigger.WinsGame == true;
    }

    public static bool IsBattlefieldHeldActivateUnitConquestEffectsTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldHeldActivateUnitConquestEffects, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldHeld, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.UnitAtThisBattlefield, StringComparison.Ordinal);
    }

    public static bool IsBattlefieldFirstTurnScoreTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldFirstTurnScore, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.TurnStart, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.EachPlayer, StringComparison.Ordinal)
            && trigger.FirstTurnOnly == true
            && trigger.ScoreAmount is > 0;
    }

    public static bool IsBattlefieldFirstTurnExtraRuneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldFirstTurnExtraRune, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.TurnStart, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.EachPlayer, StringComparison.Ordinal)
            && trigger.FirstTurnOnly == true
            && trigger.RuneCallCount is > 0;
    }

    public static bool IsBattlefieldMovedUnitPowerModifierTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldUnitMovedAwayPowerModifier, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldUnitMovedAway, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.MovedUnit, StringComparison.Ordinal)
            && string.Equals(trigger.Duration, TriggerDurations.UntilEndOfTurn, StringComparison.Ordinal)
            && trigger.PowerDelta is > 0;
    }

    public static bool IsBattlefieldTurnStartDamageAllUnitsTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldTurnStartDamageAllUnits, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.TurnStart, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.UnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.DamageAmount is > 0;
    }

    public static bool IsBattlefieldTurnStartDestroyUnitDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldTurnStartDestroyUnitDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.TurnStart, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledUnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.DestroyCount is > 0
            && trigger.DrawCount is > 0
            && trigger.Optional == true;
    }

    public static bool IsBattlefieldConquerRevealRecycleTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerRevealRecycle, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.RevealSourceZone, TriggerZones.MainDeck, StringComparison.Ordinal)
            && string.Equals(trigger.RecycleDestinationZone, TriggerZones.MainDeck, StringComparison.Ordinal)
            && trigger.RevealCount is > 0
            && trigger.RecycleCount is > 0;
    }

    public static bool IsBattlefieldConquerMillTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerMill, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.MillSourceZone, TriggerZones.MainDeck, StringComparison.Ordinal)
            && string.Equals(trigger.MillDestinationZone, TriggerZones.Graveyard, StringComparison.Ordinal)
            && trigger.MillCount is > 0;
    }

    public static bool IsBattlefieldConquerRecycleRuneTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerRecycleRune, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OwnedRuneInBase, StringComparison.Ordinal)
            && string.Equals(trigger.RecycleSourceZone, TriggerZones.Base, StringComparison.Ordinal)
            && string.Equals(trigger.RecycleDestinationZone, TriggerZones.MainDeck, StringComparison.Ordinal)
            && trigger.RecycleCount is > 0;
    }

    public static bool IsBattlefieldConquerConsumeBoonDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerConsumeBoonDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledBoonUnitOnField, StringComparison.Ordinal)
            && trigger.ConsumedBoonCount is > 0
            && trigger.DrawCount is > 0;
    }

    public static bool IsBattlefieldConquerDiscardDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerDiscardDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.ControlledHandCard, StringComparison.Ordinal)
            && trigger.DiscardCount is > 0
            && string.Equals(trigger.DiscardSourceZone, TriggerZones.Hand, StringComparison.Ordinal)
            && string.Equals(trigger.DiscardDestinationZone, TriggerZones.Graveyard, StringComparison.Ordinal)
            && trigger.DrawCount is > 0;
    }

    public static bool IsBattlefieldConquerDrawForOtherBattlefieldsTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerDrawForOtherBattlefields, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OtherControlledBattlefields, StringComparison.Ordinal)
            && trigger.DrawCountPerParticipant is > 0;
    }

    public static bool IsBattlefieldConquerPowerfulPayDrawTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerPowerfulPayDraw, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.SurvivingPowerfulUnitAtThisBattlefield, StringComparison.Ordinal)
            && trigger.ManaCost is > 0
            && trigger.DrawCount is > 0
            && trigger.RequiredPowerThreshold is > 0;
    }

    public static bool IsBattlefieldConquerReadyRunesAtEndTrigger(TriggerSpec trigger)
    {
        return string.Equals(trigger.Kind, TriggerKinds.BattlefieldConquerReadyRunesAtEnd, StringComparison.Ordinal)
            && string.Equals(trigger.Timing, TriggerTimings.BattlefieldConquered, StringComparison.Ordinal)
            && string.Equals(trigger.TargetScope, TriggerTargetScopes.OwnedRuneInBase, StringComparison.Ordinal)
            && trigger.RuneReadyCount.GetValueOrDefault() > 0
            && string.Equals(trigger.ReadyTiming, TriggerReadyTimings.EndOfTurn, StringComparison.Ordinal);
    }

    public static bool IsImplementedBattlefieldTrigger(TriggerSpec trigger)
    {
        return trigger.Kind switch
        {
            TriggerKinds.BattlefieldUnitMovedAwayPowerModifier => true,
            TriggerKinds.BattlefieldHeldNextSpellEcho => true,
            TriggerKinds.BattlefieldHeldUnitCostIncrease => true,
            TriggerKinds.BattlefieldHeldDrawOne => true,
            TriggerKinds.BattlefieldHeldCallRune => true,
            TriggerKinds.BattlefieldHeldEachPlayerCallRune => true,
            TriggerKinds.BattlefieldHeldMoveUnitToBase => true,
            TriggerKinds.BattlefieldDefendMoveFriendlyUnitToBase => true,
            TriggerKinds.BattlefieldDefendGrantSteadfast => true,
            TriggerKinds.BattlefieldHeldGrantBoon => true,
            TriggerKinds.BattlefieldHeldCreateMinion => true,
            TriggerKinds.BattlefieldHeldReturnHero => true,
            TriggerKinds.BattlefieldHeldSevenUnitsWin => true,
            TriggerKinds.BattlefieldHeldPayPowerScore => true,
            TriggerKinds.BattlefieldHeldActivateUnitConquestEffects => true,
            TriggerKinds.BattlefieldConquerRevealRecycle => true,
            TriggerKinds.BattlefieldConquerMill => true,
            TriggerKinds.BattlefieldConquerRecycleRune => true,
            TriggerKinds.BattlefieldConquerConsumeBoonDraw => true,
            TriggerKinds.BattlefieldConquerDiscardDraw => true,
            TriggerKinds.BattlefieldConquerDrawForOtherBattlefields => true,
            TriggerKinds.BattlefieldConquerPowerfulPayDraw => true,
            TriggerKinds.BattlefieldConquerReadyRunesAtEnd => true,
            TriggerKinds.BattlefieldConquerReadyEquipment => true,
            TriggerKinds.BattlefieldConquerPayCreateGold => true,
            TriggerKinds.BattlefieldConquerPayReturnUnitCreateSandSoldier => true,
            TriggerKinds.BattlefieldConquerPayReadyLegend => true,
            TriggerKinds.BattlefieldDefendRevealTopDrawSpellOrRecycle => true,
            TriggerKinds.BattlefieldConquerOverkillCreateWarhawk => true,
            TriggerKinds.BattlefieldFriendlySpellDraw => true,
            TriggerKinds.BattlefieldSpellPowerBonus => true,
            TriggerKinds.BattlefieldHighCostSpellInsightRecycle => true,
            TriggerKinds.BattlefieldPlayUnitPayBoon => true,
            TriggerKinds.BattlefieldUnitReturnedPayCallRune => true,
            TriggerKinds.BattlefieldFirstUnitPlayedMoveOtherToBase => true,
            TriggerKinds.BattlefieldTurnStartDamageAllUnits => true,
            TriggerKinds.BattlefieldTurnStartDestroyUnitDraw => true,
            TriggerKinds.BattlefieldFirstTurnExtraRune => true,
            TriggerKinds.BattlefieldFirstTurnScore => true,
            _ => false,
        };
    }

    public static bool TryGetBattlefieldConquerReadyEquipmentTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldConquerReadyEquipment,
            out trigger);
    }

    public static bool TryGetBattlefieldConquerPayCreateGoldTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldConquerPayCreateGold,
            out trigger);
    }

    public static bool TryGetBattlefieldConquerPayReturnUnitCreateSandSoldierTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldConquerPayReturnUnitCreateSandSoldier,
            out trigger);
    }

    public static bool TryGetBattlefieldConquerPayReadyLegendTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldConquerPayReadyLegend,
            out trigger);
    }

    public static bool TryGetBattlefieldDefendRevealTopDrawSpellOrRecycleTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldDefendRevealTopDrawSpellOrRecycle,
            out trigger);
    }

    public static bool TryGetBattlefieldConquerOverkillCreateWarhawkTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldConquerOverkillCreateWarhawk,
            out trigger);
    }

    public static bool TryGetBattlefieldFriendlySpellDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldFriendlySpellDraw,
            out trigger);
    }

    public static bool TryGetBattlefieldSpellPowerBonusTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldSpellPowerBonus,
            out trigger);
    }

    public static bool TryGetBattlefieldHighCostSpellInsightRecycleTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHighCostSpellInsightRecycle,
            out trigger);
    }

    public static bool TryGetBattlefieldPlayUnitPayBoonTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldPlayUnitPayBoon,
            out trigger);
    }

    public static bool TryGetBattlefieldUnitReturnedPayCallRuneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldUnitReturnedPayCallRune,
            out trigger);
    }

    public static bool TryGetBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldFirstUnitPlayedMoveOtherToBase,
            out trigger);
    }

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

    private static bool TryGetTrigger(string? cardNo, string kind, out TriggerSpec trigger)
    {
        trigger = default!;
        var match = TriggersForCard(cardNo)
            .FirstOrDefault(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
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
