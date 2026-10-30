namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static IReadOnlyList<string> SameLocationOtherFriendlySameNameUnitIds(MatchState state, string sourceId)
    {
        if (!state.CardObjects.TryGetValue(sourceId, out var source) || source.IsFaceDown
            || !source.Tags.Contains(CardObjectTags.UnitCard)
            || FindFieldObjectLocation(state.PlayerZones, sourceId) is not { } sourceAt) return [];
        var controller = EffectiveFieldControllerId(state.PlayerZones, sourceId, source);
        var name = DeckChoiceLabel(state, sourceId);
        var battlefield = BattlefieldLocalRules.AtUnit(state, sourceId);
        return state.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields)).Distinct(StringComparer.Ordinal)
            .Where(id => id != sourceId && state.CardObjects.TryGetValue(id, out var other)
                && !other.IsFaceDown && other.Tags.Contains(CardObjectTags.UnitCard)
                && EffectiveFieldControllerId(state.PlayerZones, id, other) == controller
                && DeckChoiceLabel(state, id) == name
                && FindFieldObjectLocation(state.PlayerZones, id) is { } at && at.Zone == sourceAt.Zone
                && (at.Zone == "BASE" ? at.PlayerId == sourceAt.PlayerId
                    : battlefield is not null && BattlefieldLocalRules.AtUnit(state, id)?.ObjectId == battlefield.ObjectId))
            .Order(StringComparer.Ordinal).ToArray();
    }
}
