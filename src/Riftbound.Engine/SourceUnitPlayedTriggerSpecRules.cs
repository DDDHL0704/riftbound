using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal static class SourceUnitPlayedTriggerSpecRules
{
    public static IReadOnlyList<TriggerSpec> TriggersForCard(string? cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return [];
        }

        return RuntimeCardBehaviorSpecs.TriggersByCardNo.TryGetValue(cardNo.Trim(), out var triggers)
            ? triggers
            : [];
    }

    public static bool IsSupportedSourceUnitPlayedTrigger(TriggerSpec trigger)
    {
        if (!string.Equals(trigger.Timing, TriggerTimings.SourceUnitPlayed, StringComparison.Ordinal))
        {
            return false;
        }

        return trigger.Kind switch
        {
            TriggerKinds.SourceUnitPlayedPlayLowCostGraveyardSpellRecycle => string.Equals(
                    trigger.TargetScope,
                    TriggerTargetScopes.ControlledSpellInGraveyard,
                    StringComparison.Ordinal)
                && trigger.PlayCount is > 0
                && string.Equals(trigger.PlayOriginZone, TriggerZones.Graveyard, StringComparison.Ordinal)
                && string.Equals(trigger.PlayDestinationZone, TriggerZones.Stack, StringComparison.Ordinal)
                && string.Equals(
                    trigger.PlayCardFilter,
                    TriggerCardFilters.TagPrefix + CardObjectTags.SpellCard,
                    StringComparison.Ordinal)
                && trigger.MaximumPlayedCardManaCost is >= 0
                && trigger.IgnorePlayManaCost == true
                && trigger.PayPlayPowerCosts == true
                && trigger.RecyclePlayedCardOnResolution == true,
            _ => false
        };
    }
}
