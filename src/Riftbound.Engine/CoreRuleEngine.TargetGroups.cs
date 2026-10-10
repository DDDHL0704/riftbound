namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static int AnyNumberTargetCount(MatchState state, string playerId, CardBehaviorDefinition behavior)
        => state.CardObjects.Keys.Count(id => IsLegalChosenCardTarget(state, playerId, id, 0, [id],
            PlayCardTargetScopeForBehavior(behavior), behavior) && HasValidTargetGroup(state, behavior, [id]));

    internal static bool HasValidTargetGroup(MatchState state, CardBehaviorDefinition behavior, IReadOnlyList<string> targets)
    {
        if (!HasValidTotalTargetPower(state, behavior, targets)) return false;
        if (targets.Count < 2) return true;
        if (behavior.TargetsShareController && targets.Select(id =>
            FindFieldObjectLocation(state.PlayerZones, id)?.PlayerId).Distinct(StringComparer.Ordinal).Count() != 1)
            return false;
        if (!behavior.TargetsShareBattlefield) return true;
        var field = state.ObjectLocations.GetValueOrDefault(targets[0])?.BattlefieldObjectId;
        return !string.IsNullOrWhiteSpace(field) && targets.All(id =>
            state.ObjectLocations.TryGetValue(id, out var location) && location.Zone == "BATTLEFIELD"
            && location.BattlefieldObjectId == field);
    }

    private static StackItemState ResolveTargetGroup(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        if (!behavior.TargetsShareBattlefield && !behavior.TargetsShareController) return item;
        var remaining = item.TargetObjectIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (HasValidTargetGroup(state, behavior, remaining)) return item;
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Target selection requires a rule command context.");
        // CN 355.11.b: the controller selects a subset of the original targets.
        // Removing one at a time represents every subset without an exponential menu.
        while (true) {
            var options = remaining.Select(id => new RuleChoiceOption("REMOVE:" + id,
                $"移除 {Label(id)}（{ResolveCurrentFieldUnitPower(state, state.CardObjects[id])} 战力）", [id])).ToList();
            if (HasValidTargetGroup(state, behavior, remaining))
                options.Add(new("CONFIRM", $"确认剩余 {remaining.Count} 个目标", remaining.ToArray()));
            var selected = frame.Choose(item.ControllerId,
                $"{behavior.DisplayName}：原目标组合已不合法。剩余目标：{string.Join("、", remaining.Select(Label))}；移除目标后确认。",
                options, kind: "TARGET_GROUP");
            if (selected == "CONFIRM") break;
            remaining.Remove(selected["REMOVE:".Length..]);
        }
        // Keep original positions for effect instructions and target-generation bindings.
        return item with { TargetObjectIds = item.TargetObjectIds.Select(id => remaining.Contains(id) ? id : string.Empty).ToArray() };

        string Label(string id) => CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo ?? "", out var card)
            ? card.DisplayName : state.CardObjects[id].CardNo ?? id;
    }
}
