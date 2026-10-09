using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static bool HasHeldTargetConfirmation(StackItemState item)
        => item.HeldContext?.Kind is "BOON" or "MOVE_BASE" or "RETURN_PERMANENT" or "RETURN_HERO";
    private static bool NeedsHeldTargetConfirmation(StackItemState item)
        => HasHeldTargetConfirmation(item) && item.TargetGenerations is null;

    private static string[] HeldTargetChoices(MatchState state, StackItemState item)
    {
        var player = item.ControllerId;
        IEnumerable<string> targets = item.HeldContext!.Kind switch {
            "BOON" => HeldUnitsAt(state, item.HeldContext.BattlefieldObjectId),
            "MOVE_BASE" => state.CardObjects.Keys.Where(id => BattlefieldLocalRules.AtUnit(state, id) is not null
                && MovementRestrictionRules.CanMove(state.CardObjects[id], player) && !BattlefieldLocalRules.PreventsMoveToBase(state, id)),
            "RETURN_HERO" => ChosenChampionRules.CanReturnToChampionZone(state, player)
                ? state.PlayerZones[player].Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card)
                    && ChosenChampionRules.Matches(state, player, card)) : [],
            "RETURN_PERMANENT" => state.PlayerZones[player].Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var card)
                && (card.Tags.Contains(CardObjectTags.UnitCard) || card.Tags.Contains(CardObjectTags.EquipmentCard))),
            _ => []
        };
        return targets.Where(id => TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id)).Order(StringComparer.Ordinal).ToArray();
    }

    private static PendingCardChoiceState HeldTargetChoice(MatchState state, StackItemState item)
    {
        var targets = HeldTargetChoices(state, item);
        var reason = item.HeldContext!.Kind switch {
            "BOON" => "选择此战场的一名单位获得增益；确认目标及法盾费用后，双方可以响应。",
            "MOVE_BASE" => "选择任意战场上的一名单位移回其基地；不选则放弃。确认目标及法盾费用后，双方可以响应。",
            "RETURN_HERO" => "选择废牌堆中与选定英雄同名的一张英雄单位返回英雄区；不选则放弃。确认后双方可以响应。",
            _ => "选择自己废牌堆的一名单位或一件装备返回手牌；不选则放弃。确认后双方可以响应。"
        };
        return new("HELD-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            item.HeldContext.Kind == "BOON" ? 1 : 0, 1, targets, [item.HeldContext.BattlefieldObjectId],
            reason, item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareHeldTargetConfirmation(ResolutionResult result, StackItemState item)
    {
        var choice = HeldTargetChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidHeldTargetChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item is null || !NeedsHeldTargetConfirmation(item) || !ValidHeldTargetStack(state, item)) return false;
        var expected = HeldTargetChoice(state, item);
        return choice.ChoiceWindow == expected.ChoiceWindow && choice.ChoiceId == expected.ChoiceId
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == expected.RequiredCount && choice.MaxCount == 1 && choice.LegalObjectIds.Count > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static bool ValidHeldTargetStack(MatchState state, StackItemState item)
    {
        if (!HasHeldTargetConfirmation(item)) return true;
        var context = item.HeldContext!;
        if (HeldDefinition(context.CardNo) is not { } definition || definition.Kind != context.Kind || definition.Amount != context.Amount
            || item.CardNo != context.CardNo || item.EffectKind != "HOLD_" + context.Kind) return false;
        return item.TargetGenerations is { } generations
            ? item.TargetObjectIds.Count == 1 && generations.Count == 1 && generations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0
            : item.TargetObjectIds.Count == 0 && state.PriorityPlayerId is null
                && (state.PendingCardChoice is not null || state.PendingPayment is not null || state.TriggerQueue.Count > 0);
    }

    private static string? HeldResolvedTarget(MatchState state, StackItemState item)
        => item.TargetObjectIds.SingleOrDefault() is { } id && item.TargetGenerations?.GetValueOrDefault(id, -1) == state.CardObjects.GetValueOrDefault(id)?.ObjectGeneration
            && HeldTargetChoices(state, item).Contains(id) ? id : null;
}
