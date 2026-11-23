namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static int AnyNumberTargetCount(MatchState state, string playerId, CardBehaviorDefinition behavior)
        => behavior.DamageDistributionUsesFirstTargetMight
            ? 1 + state.CardObjects.Keys.Count(id => IsLegalChosenCardTarget(state, playerId, id, 1, [id],
                PlayCardTargetScopeForBehavior(behavior), behavior) && HasValidTargetGroup(state, behavior, [id]))
            : state.CardObjects.Keys.Count(id => IsLegalChosenCardTarget(state, playerId, id, 0, [id],
            PlayCardTargetScopeForBehavior(behavior), behavior) && HasValidTargetGroup(state, behavior, [id]));

    internal static bool HasTargetGroupConstraint(CardBehaviorDefinition behavior)
        => behavior.MaxTotalTargetPower > 0 || behavior.TargetsShareController
            || behavior.TargetsShareBattlefield || behavior.TargetsShareLocation;

    internal static bool HasValidTargetGroup(MatchState state, CardBehaviorDefinition behavior, IReadOnlyList<string> targets)
    {
        if (!HasValidTotalTargetPower(state, behavior, targets)) return false;
        if (targets.Count < 2) return true;
        if (behavior.TargetsShareController && targets.Select(id =>
            FindFieldObjectLocation(state.PlayerZones, id)?.PlayerId).Distinct(StringComparer.Ordinal).Count() != 1)
            return false;
        if (!behavior.TargetsShareBattlefield && !behavior.TargetsShareLocation) return true;
        if (!state.ObjectLocations.TryGetValue(targets[0], out var first)) return false;
        if (first.Zone == "BATTLEFIELD" && !string.IsNullOrWhiteSpace(first.BattlefieldObjectId))
            return targets.All(id => state.ObjectLocations.TryGetValue(id, out var location)
                && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == first.BattlefieldObjectId);
        return behavior.TargetsShareLocation && first.Zone == "BASE"
            && targets.All(id => state.ObjectLocations.TryGetValue(id, out var location)
                && location.Zone == "BASE" && location.PlayerId == first.PlayerId);
    }

    private static StackItemState ResolveTargetGroup(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
    {
        if (!HasTargetGroupConstraint(behavior)) return item;
        var original = item.TargetObjectIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        if (HasValidTargetGroup(state, behavior, original)) return item;
        var remaining = original.ToList();
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Target selection requires a rule command context.");
        // CN 355.11.b–b.1: only original targets are eligible, and a legal subset
        // must keep every target that can be added without breaking the group.
        while (true)
        {
            var options = remaining.Select(id => new RuleChoiceOption("REMOVE:" + id,
                $"移除 {Label(id)}（{ResolveCurrentFieldUnitPower(state, state.CardObjects[id])} 战力）", [id])).ToList();
            if (HasValidTargetGroup(state, behavior, remaining))
            {
                var restorable = original.Where(id => !remaining.Contains(id)
                    && HasValidTargetGroup(state, behavior, [.. remaining, id])).ToArray();
                options.AddRange(restorable.Select(id => new RuleChoiceOption("RESTORE:" + id, $"恢复 {Label(id)}", [id])));
                if (restorable.Length == 0)
                    options.Add(new("CONFIRM", $"确认剩余 {remaining.Count} 个目标", remaining.ToArray()));
            }

            var selected = frame.Choose(item.ControllerId,
                $"{behavior.DisplayName}：原目标组合已不合法。剩余目标：{string.Join("、", remaining.Select(Label))}；调整目标后确认。",
                options, kind: "TARGET_GROUP");
            if (selected == "CONFIRM") break;
            if (selected.StartsWith("RESTORE:", StringComparison.Ordinal))
                remaining.Add(selected["RESTORE:".Length..]);
            else
                remaining.Remove(selected["REMOVE:".Length..]);
        }
        // Keep original positions for effect instructions and target-generation bindings.
        return item with { TargetObjectIds = item.TargetObjectIds.Select(id => remaining.Contains(id) ? id : string.Empty).ToArray() };

        string Label(string id) => CardBehaviorRegistry.TryGetByCardNo(state.CardObjects[id].CardNo ?? "", out var card)
            ? card.DisplayName : state.CardObjects[id].CardNo ?? id;
    }
}
