using System.Text.RegularExpressions;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

internal static class PermanentPlayTriggerRecoveryRules
{
    // Rebuild the restricted source family from immutable printed text rather
    // than trusting the producer's decision or any reconstructed player view.
    private static readonly Lazy<IReadOnlySet<string>> BattlefieldOnlySources = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Where(card => Regex.IsMatch(card.CardEffect,
                    @"(?:^|[。\n])\s*每?当你将我打出到一处战场时", RegexOptions.CultureInvariant)
                && !Regex.IsMatch(card.CardEffect,
                    @"(?:^|[。\n])\s*每?(?:当你打出(?:我|此牌|此装备)|当此牌被打出|当你将我打出)时",
                    RegexOptions.CultureInvariant))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (!item.SourceConfirmed || !BattlefieldOnlySources.Value.Contains(item.CardNo)
            || !CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var behavior)
            || item.EffectKind != behavior.EffectKind
            || !(behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment)) return true;

        // This is the recorded play destination, not the source's current
        // location: a response can move the permanent after its ability triggers.
        if (item.Destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            && item.Destination.Length > "BATTLEFIELD:".Length) return true;

        // The current permanent flow also carries the controller's independent
        // legend ability. An opponent's legend cannot authorize this item.
        return behavior.PlaysSourceToBaseAsUnit
            && state.PlayerZones.TryGetValue(item.ControllerId, out var zones)
            && zones.LegendZone.Any(id => state.CardObjects.TryGetValue(id, out var legend)
                && CardControlRules.IsControlledByPlayerOrLegacyOwned(legend, item.ControllerId)
                && LegendIdentityCatalog.IsSourceCardNoForIdentity(LegendIdentityCatalog.RengarLegendIdentityId, legend.CardNo));
    }
}
