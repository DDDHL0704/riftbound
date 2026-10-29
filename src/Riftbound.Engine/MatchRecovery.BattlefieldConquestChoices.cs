using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{
    private static bool ValidBattlefieldConquestQueue(MatchState state, TriggerQueueItemState item)
    {
        var (source, tick, kind, valid) = item.BattlefieldConquestDiscardDraw is { } discard
            ? (discard.Source, discard.ConquestTick, "CONQUEST-DISCARD-DRAW", CoreRuleEngine.ValidBattlefieldConquestDiscardDrawContext(
                state, discard, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo))
            : item.BattlefieldConquestReadyEquipment is { } ready
                ? (ready.Source, ready.ConquestTick, "CONQUEST-READY-EQUIPMENT", CoreRuleEngine.ValidBattlefieldConquestReadyEquipmentContext(
                    state, ready, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
                    && !ready.ReadyCompleted && ready.DetachChosen is null && ready.TargetTokenFactoryCardNo is null)
                : item.BattlefieldConquestConsumeBoon is { } boon
                    ? (boon.Source, boon.ConquestTick, "CONQUEST-BOON-DRAW", CoreRuleEngine.ValidBattlefieldConquestConsumeBoonContext(
                        state, boon, item.EffectKind, item.SourceObjectId, item.ControllerId, item.SourceCardNo))
                    : (null, -1L, "", false);
        if (!valid || source is null || item.EnqueuedAtTick != tick || item.TriggeredByEventKind != "BATTLEFIELD_CONQUERED"
            || item.TriggerId != $"{kind}:{tick}:{source.ObjectId}") return false;
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(new TriggerQueueItemState(
            item.TriggerId, item.ControllerId, item.SourceObjectId, item.EffectKind, item.TriggeredByEventKind, item.TimingContext) {
            EnqueuedAtTick = item.EnqueuedAtTick, ControllerOrderConfirmed = item.ControllerOrderConfirmed,
            SourceCardNo = item.SourceCardNo, BattlefieldConquestDiscardDraw = item.BattlefieldConquestDiscardDraw,
            BattlefieldConquestReadyEquipment = item.BattlefieldConquestReadyEquipment,
            BattlefieldConquestConsumeBoon = item.BattlefieldConquestConsumeBoon });
    }

    private static bool ValidBattlefieldConquestStack(MatchState state, StackItemState item)
    {
        var (source, tick, kind, valid) = item.BattlefieldConquestDiscardDraw is { } discard
            ? (discard.Source, discard.ConquestTick, "CONQUEST-DISCARD-DRAW", CoreRuleEngine.ValidBattlefieldConquestDiscardDrawContext(
                state, discard, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
            : item.BattlefieldConquestReadyEquipment is { } ready
                ? (ready.Source, ready.ConquestTick, "CONQUEST-READY-EQUIPMENT", CoreRuleEngine.ValidBattlefieldConquestReadyEquipmentContext(
                    state, ready, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
                    && ValidBattlefieldConquestEquipmentTarget(state, item, ready))
                : item.BattlefieldConquestConsumeBoon is { } boon
                    ? (boon.Source, boon.ConquestTick, "CONQUEST-BOON-DRAW", CoreRuleEngine.ValidBattlefieldConquestConsumeBoonContext(
                        state, boon, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo))
                    : (null, -1L, "", false);
        if (!valid || source is null || item.StackItemId != $"ordered-{kind}:{tick}:{source.ObjectId}") return false;
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(new StackItemState(
            item.StackItemId, item.ControllerId, item.SourceObjectId, item.EffectKind, item.CardNo,
            targetObjectIds: item.BattlefieldConquestReadyEquipment is null ? null : item.TargetObjectIds,
            timingContext: item.TimingContext,
            targetGenerations: item.BattlefieldConquestReadyEquipment is null ? null : item.TargetGenerations) {
            BattlefieldConquestDiscardDraw = item.BattlefieldConquestDiscardDraw,
            BattlefieldConquestReadyEquipment = item.BattlefieldConquestReadyEquipment,
            BattlefieldConquestConsumeBoon = item.BattlefieldConquestConsumeBoon, TriggerCost = item.TriggerCost });
    }

    private static bool ValidBattlefieldConquestEquipmentTarget(MatchState state, StackItemState item,
        BattlefieldConquestReadyEquipmentContext context)
    {
        if (item.TargetGenerations is null)
            return item.TargetObjectIds.Count == 0 && context.TargetTokenFactoryCardNo is null
                && !context.ReadyCompleted && context.DetachChosen is null;
        if (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1
            || !item.TargetGenerations.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0) return false;
        var id = item.TargetObjectIds[0];
        return state.CardObjects.TryGetValue(id, out var target)
            ? target.ObjectGeneration >= generation
                && (TokenObjectRules.IsToken(target) ? target.TokenFactoryCardNo ?? target.CardNo : null) == context.TargetTokenFactoryCardNo
            : context.TargetTokenFactoryCardNo is not null && P6TokenFactoryCatalog.IsTokenFactory(context.TargetTokenFactoryCardNo);
    }

    private static bool IsBattlefieldConquestChoiceEffect(string effect)
        => effect is TriggerKinds.BattlefieldConquerDiscardDraw or TriggerKinds.BattlefieldConquerReadyEquipment
            or TriggerKinds.BattlefieldConquerConsumeBoonDraw;

    private static void ValidateBattlefieldConquestChoiceContinuations(MatchState state, List<string> errors)
    {
        foreach (var item in state.TriggerQueue)
            if (item.BattlefieldConquestDiscardDraw is not null || item.BattlefieldConquestReadyEquipment is not null
                || item.BattlefieldConquestConsumeBoon is not null ? !ValidBattlefieldConquestQueue(state, item)
                : IsBattlefieldConquestChoiceEffect(item.EffectKind)) errors.Add("invalid captured battlefield conquest choice queue");
        foreach (var item in state.StackItems)
        {
            if (item.BattlefieldConquestDiscardDraw is not null || item.BattlefieldConquestReadyEquipment is not null
                || item.BattlefieldConquestConsumeBoon is not null ? !ValidBattlefieldConquestStack(state, item)
                : IsBattlefieldConquestChoiceEffect(item.EffectKind)) errors.Add("invalid captured battlefield conquest choice stack");
            if (item.BattlefieldConquestReadyEquipment is { ReadyCompleted: true, DetachChosen: null }
                && (state.PendingCardChoice is not { } choice
                    || !CoreRuleEngine.ValidBattlefieldConquestReadyEquipmentDetachChoice(state, choice)))
                errors.Add("invalid battlefield equipment detach continuation");
            if (item.BattlefieldConquestReadyEquipment is { DetachChosen: not null })
                errors.Add("completed battlefield equipment detach choice cannot remain suspended");
        }
        if (state.PendingHandChoice is { } hand
            && (hand.BattlefieldConquestDiscardDraw is not null && !CoreRuleEngine.ValidBattlefieldConquestDiscardHandChoice(state, hand)
                || hand.ChoiceId.StartsWith("CONQUEST-HAND:", StringComparison.Ordinal) && hand.BattlefieldConquestDiscardDraw is null))
            errors.Add("invalid battlefield conquest discard hand continuation");
        if (state.PendingCardChoice is { ChoiceWindow: CoreRuleEngine.BattlefieldConquestEquipmentDetachWindow } detach
            && !CoreRuleEngine.ValidBattlefieldConquestReadyEquipmentDetachChoice(state, detach))
            errors.Add("invalid battlefield equipment detach choice");
    }

    private static T? ReadBattlefieldConquestPublicContext<T>(object? payload, string key) where T : class
    {
        if (!TryReadObjectValue(payload, key, out var value) || IsNullSnapshotPayloadValue(value)) return null;
        try { return JsonSerializer.SerializeToElement(value).Deserialize<T>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return null; }
    }

    private static void ValidateBattlefieldConquestChoiceView(object? payload, MatchState? authority, bool stack, string label, List<string> errors)
    {
        TryReadObjectString(payload, "effectKind", out var effect);
        TryReadObjectString(payload, stack ? "stackItemId" : "triggerId", out var id);
        var actualDiscard = ReadBattlefieldConquestPublicContext<BattlefieldConquestDiscardDrawPublicContext>(payload, "battlefieldConquestDiscardDraw");
        var actualReady = ReadBattlefieldConquestPublicContext<BattlefieldConquestReadyEquipmentPublicContext>(payload, "battlefieldConquestReadyEquipment");
        var actualBoon = ReadBattlefieldConquestPublicContext<BattlefieldConquestConsumeBoonPublicContext>(payload, "battlefieldConquestConsumeBoon");
        if ((effect == TriggerKinds.BattlefieldConquerDiscardDraw) != (actualDiscard is not null)
            || (effect == TriggerKinds.BattlefieldConquerReadyEquipment) != (actualReady is not null)
            || (effect == TriggerKinds.BattlefieldConquerConsumeBoonDraw) != (actualBoon is not null)
            || TryReadObjectValue(payload, "battlefieldConquestDiscardDraw", out _) && actualDiscard is null
            || TryReadObjectValue(payload, "battlefieldConquestReadyEquipment", out _) && actualReady is null
            || TryReadObjectValue(payload, "battlefieldConquestConsumeBoon", out _) && actualBoon is null)
            errors.Add($"{label} invalid public battlefield conquest context");
        if (authority is null) return;
        var item = stack ? authority.StackItems.FirstOrDefault(i => i.StackItemId == id) : null;
        var trigger = stack ? null : authority.TriggerQueue.FirstOrDefault(i => i.TriggerId == id);
        if (MatchStateHasher.HashValue(actualDiscard) != MatchStateHasher.HashValue(CoreRuleEngine.PublicBattlefieldConquestDiscardDraw(item?.BattlefieldConquestDiscardDraw ?? trigger?.BattlefieldConquestDiscardDraw))
            || MatchStateHasher.HashValue(actualReady) != MatchStateHasher.HashValue(CoreRuleEngine.PublicBattlefieldConquestReadyEquipment(item?.BattlefieldConquestReadyEquipment ?? trigger?.BattlefieldConquestReadyEquipment))
            || MatchStateHasher.HashValue(actualBoon) != MatchStateHasher.HashValue(CoreRuleEngine.PublicBattlefieldConquestConsumeBoon(item?.BattlefieldConquestConsumeBoon ?? trigger?.BattlefieldConquestConsumeBoon)))
            errors.Add($"{label} public battlefield conquest context differs from authoritative state");
    }

    private static void ValidateBattlefieldConquestChoiceStackViews(IReadOnlyList<object?> payloads,
        MatchState authority, string label, List<string> errors)
    {
        foreach (var payload in payloads) ValidateBattlefieldConquestChoiceView(payload, authority, true, label, errors);
    }

    private static bool HasAcceptedBattlefieldChoice(IReadOnlyList<RecoveredCommand> commands, RecoveredEvent occurred,
        string player, string choiceId, string window, string chosen)
        => commands.Any(command => command.Accepted && command.PlayerId == player && command.CommandType == CommandTypes.ChooseCards
            && command.CompletedTick == occurred.Tick && command.StartedEventSequence < occurred.Sequence
            && command.CompletedEventSequence >= occurred.Sequence
            && (command.RawCommand ?? command.TypedCommand?.Value) is { } input
            // Typed callers retain their original arguments; the choice executor trims text.
            && TryReadObjectString(input, "choiceId", out var id) && id?.Trim() == choiceId
            && TryReadObjectString(input, "choiceWindow", out var commandWindow) && commandWindow?.Trim() == window
            && TryReadObjectStringList(input, "chosenObjectIds", out var selected)
            && selected.Count == 1 && selected[0]?.Trim() == chosen);

    private static void ValidateBattlefieldConquestChoiceHistory(MatchState state,
        IReadOnlyList<RecoveredCommand> commands, IReadOnlyList<RecoveredEvent> events, List<string> errors)
    {
        var pending = state.TriggerQueue.Select(t => (t.BattlefieldConquestDiscardDraw, t.BattlefieldConquestReadyEquipment, t.BattlefieldConquestConsumeBoon))
            .Concat(state.StackItems.Select(i => (i.BattlefieldConquestDiscardDraw, i.BattlefieldConquestReadyEquipment, i.BattlefieldConquestConsumeBoon)))
            .Concat(state.PendingHandChoice?.BattlefieldConquestDiscardDraw is { } continuation
                ? [(continuation.Context, null, null)] : [])
            .Where(p => p.BattlefieldConquestDiscardDraw is not null || p.BattlefieldConquestReadyEquipment is not null || p.BattlefieldConquestConsumeBoon is not null);
        foreach (var (discard, ready, boon) in pending)
        {
            var source = discard?.Source ?? ready?.Source ?? boon?.Source;
            var player = discard?.ControllerId ?? ready?.ControllerId ?? boon?.ControllerId;
            var conqueror = discard?.ConquestSourceObjectId ?? ready?.ConquestSourceObjectId ?? boon?.ConquestSourceObjectId;
            var tick = discard?.ConquestTick ?? ready?.ConquestTick ?? boon?.ConquestTick ?? -1;
            var kind = discard is not null ? "CONQUEST-DISCARD-DRAW" : ready is not null ? "CONQUEST-READY-EQUIPMENT" : "CONQUEST-BOON-DRAW";
            var effect = discard is not null ? TriggerKinds.BattlefieldConquerDiscardDraw
                : ready is not null ? TriggerKinds.BattlefieldConquerReadyEquipment : TriggerKinds.BattlefieldConquerConsumeBoonDraw;
            if (source is null) { errors.Add("captured battlefield conquest choice has no source"); continue; }
            var triggerId = $"{kind}:{tick}:{source.ObjectId}";
            var conquests = events.Where(e => e.Tick == tick && e.Event.Kind == "BATTLEFIELD_CONQUERED"
                && TryReadObjectString(e.Event.Payload, "playerId", out var p) && p == player
                && TryReadObjectString(e.Event.Payload, "battlefieldId", out var b) && b == source.ObjectId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var s) && s == conqueror).Take(2).ToArray();
            var queued = events.Where(e => e.Tick == tick && e.Event.Kind == "TRIGGER_QUEUED"
                && TryReadObjectString(e.Event.Payload, "triggerId", out var id) && id == triggerId
                && TryReadObjectString(e.Event.Payload, "controllerId", out var p) && p == player
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var s) && s == source.ObjectId
                && TryReadObjectLong(e.Event.Payload, "sourceGeneration", out var g) && g == source.Generation
                && TryReadObjectString(e.Event.Payload, "effectKind", out var k) && k == effect
                && TryReadObjectString(e.Event.Payload, "triggeredByEventKind", out var origin) && origin == "BATTLEFIELD_CONQUERED").Take(2).ToArray();
            if (conquests.Length != 1 || queued.Length != 1 || queued[0].Sequence <= conquests[0].Sequence
                || !commands.Any(c => c.Accepted && c.CompletedTick == tick && c.StartedEventSequence < conquests[0].Sequence
                    && c.CompletedEventSequence >= queued[0].Sequence))
                errors.Add("captured battlefield conquest choice is not bound to accepted conquest history");
        }

        foreach (var item in state.StackItems.Where(i => i.BattlefieldConquestReadyEquipment is not null && i.TargetGenerations is not null))
        {
            if (item.TargetObjectIds.Count != 1) continue;
            var id = item.TargetObjectIds[0];
            if (!item.TargetGenerations!.TryGetValue(id, out var generation)) continue;
            var confirmed = events.Where(e => e.Event.Kind == "TRIGGER_CONFIRMED"
                && TryReadObjectString(e.Event.Payload, "stackItemId", out var stackId) && stackId == item.StackItemId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == item.SourceObjectId
                && TryReadObjectStringList(e.Event.Payload, "targetObjectIds", out var targets) && targets.SequenceEqual([id])
                && TryReadObjectLong(e.Event.Payload, "targetGeneration", out var g) && g == generation
                && (item.BattlefieldConquestReadyEquipment!.TargetTokenFactoryCardNo is null
                    ? !TryReadObjectValue(e.Event.Payload, "targetTokenFactoryCardNo", out _)
                    : TryReadObjectString(e.Event.Payload, "targetTokenFactoryCardNo", out var token)
                        && token == item.BattlefieldConquestReadyEquipment.TargetTokenFactoryCardNo)).Take(2).ToArray();
            if (confirmed.Length != 1 || !HasAcceptedBattlefieldChoice(commands, confirmed[0], item.ControllerId,
                "TRIGGER-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", id))
                errors.Add("captured battlefield equipment target is not bound to accepted confirmation history");
        }
        foreach (var item in state.StackItems.Where(i => i.BattlefieldConquestReadyEquipment is { ReadyCompleted: true, DetachChosen: null }))
        {
            var opened = events.Where(e => e.Event.Kind == "CARD_CHOICE_REQUESTED"
                && TryReadObjectString(e.Event.Payload, "choiceId", out var id) && id == "CONQUEST-DETACH:" + item.StackItemId
                && TryReadObjectString(e.Event.Payload, "choiceWindow", out var window) && window == CoreRuleEngine.BattlefieldConquestEquipmentDetachWindow
                && TryReadObjectString(e.Event.Payload, "playerId", out var player) && player == item.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == item.SourceObjectId
                && TryReadObjectStringList(e.Event.Payload, "targetObjectIds", out var targets) && targets.SequenceEqual(item.TargetObjectIds)).Take(2).ToArray();
            if (opened.Length != 1 || !events.Any(e => e.Tick == opened[0].Tick && e.Sequence < opened[0].Sequence
                    && e.Event.Kind == "STACK_ITEM_SUSPENDED" && TryReadObjectString(e.Event.Payload, "stackItemId", out var stackId) && stackId == item.StackItemId)
                || !commands.Any(c => c.Accepted && c.CompletedTick == opened[0].Tick && c.StartedEventSequence < opened[0].Sequence
                    && c.CompletedEventSequence >= opened[0].Sequence))
                errors.Add("captured battlefield equipment detach is not bound to accepted resolution history");
        }
        foreach (var item in state.StackItems.Where(i => i.BattlefieldConquestConsumeBoon is not null && i.TriggerCost?.ConsumedBoon is not null))
        {
            var paid = item.TriggerCost!.ConsumedBoon!;
            var consumed = events.Where(e => e.Event.Kind == "BOON_CONSUMED"
                && TryReadObjectString(e.Event.Payload, "stackItemId", out var stackId) && stackId == item.StackItemId
                && TryReadObjectString(e.Event.Payload, "playerId", out var p) && p == item.ControllerId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var sourceId) && sourceId == item.SourceObjectId
                && TryReadObjectLong(e.Event.Payload, "sourceGeneration", out var sg) && sg == item.BattlefieldConquestConsumeBoon!.Source.Generation
                && TryReadObjectString(e.Event.Payload, "targetObjectId", out var target) && target == paid.ObjectId
                && TryReadObjectLong(e.Event.Payload, "targetGeneration", out var tg) && tg == paid.Generation
                && TryReadObjectString(e.Event.Payload, "reason", out var reason) && reason == item.EffectKind).Take(2).ToArray();
            if (consumed.Length != 1 || !HasAcceptedBattlefieldChoice(commands, consumed[0], item.ControllerId,
                "TRIGGER-COST:" + item.StackItemId, CoreRuleEngine.TriggerCostWindow, paid.ObjectId))
                errors.Add("captured battlefield boon cost is not bound to accepted payment history");
        }
        if (state.PendingHandChoice is { BattlefieldConquestDiscardDraw: { } hand } choice)
        {
            var opened = events.Where(e => e.Event.Kind == "HAND_CHOICE_REQUESTED"
                && TryReadObjectString(e.Event.Payload, "choiceId", out var id) && id == choice.ChoiceId
                && TryReadObjectString(e.Event.Payload, "sourceObjectId", out var source) && source == choice.SourceObjectId
                && TryReadObjectString(e.Event.Payload, "effectKind", out var effect) && effect == choice.EffectKind).Take(2).ToArray();
            if (opened.Length != 1 || !events.Any(e => e.Tick == opened[0].Tick && e.Sequence < opened[0].Sequence
                    && e.Event.Kind == "STACK_ITEM_RESOLVED" && TryReadObjectString(e.Event.Payload, "stackItemId", out var stackId) && stackId == hand.StackItemId)
                || !commands.Any(c => c.Accepted && c.CompletedTick == opened[0].Tick && c.StartedEventSequence < opened[0].Sequence
                    && c.CompletedEventSequence >= opened[0].Sequence))
                errors.Add("captured battlefield discard choice is not bound to accepted resolution history");
        }
    }
}
