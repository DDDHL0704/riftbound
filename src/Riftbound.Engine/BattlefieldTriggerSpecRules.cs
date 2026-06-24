using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class BattlefieldTriggerSpecRules
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<TriggerSpec>>> TriggersByCardNo =
        new(BuildTriggerMap, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool TryGetBattlefieldMovedUnitPowerModifierTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldUnitMovedAwayPowerModifier,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldNextSpellEchoTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldNextSpellEcho,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldUnitCostIncreaseTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldUnitCostIncrease,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldDrawTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldDrawOne,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldCallRuneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldCallRune,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldEachPlayerCallRuneTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldEachPlayerCallRune,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldMoveUnitToBaseTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldMoveUnitToBase,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldGrantBoonTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldGrantBoon,
            out trigger);
    }

    public static bool TryGetBattlefieldHeldCreateMinionTrigger(string? cardNo, out TriggerSpec trigger)
    {
        return TryGetTrigger(
            cardNo,
            TriggerKinds.BattlefieldHeldCreateMinion,
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
