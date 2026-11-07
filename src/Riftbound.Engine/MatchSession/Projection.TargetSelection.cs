using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record ResolutionResult
{
    internal static IReadOnlyList<string>? TargetSelectionSubjectObjects(MatchState state,
        UnitEventContext? context, string viewer)
    {
        if (context?.TargetSelection is null && context?.FriendlyUnitAction is null) return null;
        if (context.Other?.Binding is not { } binding || !state.CardObjects.TryGetValue(binding.ObjectId, out var unit)
            || unit.ObjectGeneration != binding.Generation || unit.IsFaceDown
            || !state.PlayerZones.Values.Any(zones => zones.Base.Contains(unit.ObjectId) || zones.Battlefields.Contains(unit.ObjectId)))
            return [];
        var visible = VisibleObjectIdForViewer(state, unit.ObjectId, viewer);
        return visible == unit.ObjectId ? [visible] : [];
    }

    internal static string? TargetSelectionSummary(MatchState state, UnitEventContext? context, string viewer)
    {
        if (TargetSelectionSubjectObjects(state, context, viewer) is not { } objects) return null;
        var id = objects.SingleOrDefault();
        if (context?.FriendlyUnitAction is not null) {
            var name = id is not null && CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo!, out var subject)
                ? subject.DisplayName : "事件中的单位";
            var spec = CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var source) ? source.UnitEventAbility : null;
            return spec?.Effect == "MIGHT_OBSERVED_UNIT" ? $"让{name}本回合内战力+{spec.Amount}"
                : $"让{name}变为活跃状态";
        }
        return id is not null && CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo!, out var card)
            ? $"活跃先前选中的{card.DisplayName}" : "活跃先前选中的单位";
    }
}
