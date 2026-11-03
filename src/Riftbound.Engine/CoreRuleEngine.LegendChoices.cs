namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string ControlledLegendChoiceKind = "CONTROLLED_LEGEND_SELECTION";

    // A legend's owner and its physical zone do not choose the controller's
    // instruction. Active legends are also legal; readying one does nothing.
    private static string? ChooseControlledLegend(MatchState state, string playerId)
    {
        var candidates = state.PlayerZones.Values.SelectMany(zones => zones.LegendZone)
            .Distinct(StringComparer.Ordinal)
            .Where(id => state.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains("CARD_TYPE:LEGEND", StringComparer.Ordinal)
                && SourceObjectControlledByPlayerOrLegacyOwned(card, playerId))
            .Order(StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return null;
        if (candidates.Length == 1) return candidates[0];
        return RuleChoices.Value!.Choose(playerId, "选择你控制的一名传奇，使其变为活跃状态。",
            candidates.Select(id => new RuleChoiceOption(id,
                state.CardObjects[id].CardNo is { } cardNo
                    ? OfficialCardSourceIdentityGroups.DisplayName(cardNo) : "传奇", [id])).ToArray(),
            kind: ControlledLegendChoiceKind, preview: state with { PendingPayment = null });
    }
}
