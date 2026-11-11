using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitPlayMightContext(string CardNo, long SourceGeneration, ObjectBinding PlayedUnit,
    long CapturedTick, string PlayedCardNo, string? PlayedUnitTokenFactoryCardNo = null,
    ObjectBinding? RequestedTarget = null, string? TargetTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string UnitPlayMightEffect = "UNIT_PLAYED_POWER_PLUS_1";
    private sealed record UnitPlayMightObservation(string Source, string Player, string CardNo,
        long SourceGeneration, ObjectBinding PlayedUnit, string PlayedCardNo, string? PlayedUnitTokenFactoryCardNo, string? PlayId);

    private static GameEvent CaptureUnitPlayMight(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardObjectState entered, string player)
    {
        if (entered.IsFaceDown || ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")
            || !zones.TryGetValue(player, out var own)) return ev;
        var observations = own.LegendZone.Where(id => cards.TryGetValue(id, out var c) && !c.IsFaceDown
                && SourceObjectControlledByPlayerOrLegacyOwned(c, player) && LegendCardHasIdentity(c.CardNo, RengarLegendIdentityId))
            .Select(id => new UnitPlayMightObservation(id, player, cards[id].CardNo!, cards[id].ObjectGeneration,
                new(entered.ObjectId, entered.ObjectGeneration), entered.CardNo!,
                TokenObjectRules.IsToken(entered) ? entered.TokenFactoryCardNo ?? entered.CardNo : null,
                ev.Payload.GetValueOrDefault("historyPlayId") as string)).ToArray();
        return observations.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["unitPlayMightObservation"] = observations } };
    }

    private static ResolutionResult QueueUnitPlayMight(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault("unitPlayMightObservation") is not UnitPlayMightObservation[] observations) continue;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != "unitPlayMightObservation").ToDictionary(p => p.Key, p => p.Value) };
            foreach (var observation in observations)
            {
                var entered = observation.PlayedUnit;
                if (before.CardObjects.TryGetValue(entered.ObjectId, out var old) && !IsObjectOnField(before.PlayerZones, entered.ObjectId))
                    entered = entered with { Generation = Math.Max(entered.Generation, old.ObjectGeneration + 1) };
                ObjectBinding? requested = null;
                if (observation.PlayId is { } play && TryGetRengarUnitPlayedTarget(result.State, play, out var target)
                    && UnitPlayMightTargets(result.State, observation.Player).Contains(target))
                    requested = new(target, target == entered.ObjectId ? entered.Generation : result.State.CardObjects[target].ObjectGeneration);
                var context = new UnitPlayMightContext(observation.CardNo, observation.SourceGeneration,
                    entered, result.State.Tick, observation.PlayedCardNo, observation.PlayedUnitTokenFactoryCardNo, requested);
                var trigger = new TriggerQueueItemState($"unit-play-might-{result.State.Tick}-{index}-{observation.Source}",
                    observation.Player, observation.Source, UnitPlayMightEffect, ev.Kind) {
                    UnitPlayMight = context, SourceCardNo = context.CardNo };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        if (events.Count == result.Events.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static string[] UnitPlayMightTargets(MatchState state, string player)
        => state.CardObjects.Values.Where(c => !c.IsFaceDown && c.Tags.Contains(CardObjectTags.UnitCard)
                && IsFieldObjectControlledByZonePlayer(state.PlayerZones, state.CardObjects, c.ObjectId)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static ObjectBinding? CompanionPlayedUnit(TriggerQueueItemState trigger)
        => trigger.UnitPlayMight?.PlayedUnit ?? trigger.TurnUnitPlayBoon?.Unit ?? trigger.TurnUnitPlayDraw?.Unit
            ?? (trigger.UnitEvent is { Timing: AgilePlayTiming } agile
                ? new ObjectBinding(trigger.SourceObjectId, agile.SourceGeneration) : null)
            ?? (HasArmamentPlayAttachment(trigger.UnitEvent) && trigger.UnitEvent!.ReflexiveTick is not null
                ? trigger.UnitEvent.Other?.Binding : null)
            ?? trigger.UnitEvent?.SpellInstruction?.PlayedUnit?.Binding
            ?? trigger.FieldContext?.BattlefieldUnitPlay?.Unit;

    private static bool CompanionPlayControllerMatches(TriggerQueueItemState companion, StackItemState item)
        => companion.ControllerId == item.ControllerId || companion.UnitEvent?.SpellInstruction?.PlayedUnit is not null;

    private static ResolutionResult DeferCompanionPermanentPlayOrdering(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var state = result.State; var queue = state.TriggerQueue.ToList(); var events = result.Events.ToList();
        var companions = state.TriggerQueue.Where(t => CompanionPlayedUnit(t) is not null
            && !before.TriggerQueue.Any(old => old.TriggerId == t.TriggerId)).ToArray();
        foreach (var item in state.StackItems.Where(i => i.SourceConfirmed
            && !before.StackItems.Any(old => old.StackItemId == i.StackItemId)))
        {
            var companion = companions.FirstOrDefault(t => CompanionPlayControllerMatches(t, item)
                && CompanionPlayedUnit(t)?.ObjectId == item.SourceObjectId);
            if (companion is null || queue.Any(t => t.PermanentPlayStackItemId == item.StackItemId)) continue;
            // CN383.3.d / 388–389: independent play skills, delayed grants and
            // embedded skills earned from the same actual play share an ordering
            // window, including when the instruction's controller is its opponent.
            var trigger = new TriggerQueueItemState("permanent-play-order-" + item.StackItemId, item.ControllerId,
                item.SourceObjectId, item.EffectKind, companion.TriggeredByEventKind, item.TimingContext) {
                PermanentPlayStackItemId = item.StackItemId, SourceCardNo = item.CardNo };
            queue.Add(trigger);
            events.Add(new("TRIGGER_ORDERING_DEFERRED", "同时触发的打出技能等待控制者排序", new Dictionary<string, object?> {
                ["triggerId"] = trigger.TriggerId, ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = item.SourceObjectId }));
        }
        if (queue.Count == state.TriggerQueue.Count) return result;
        state = state with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidPermanentPlayCompanion(MatchState state, TriggerQueueItemState companion,
        StackItemState item, long? enqueuedAtTick)
    {
        if (enqueuedAtTick is not { } capturedTick) return false;
        if (!CompanionPlayControllerMatches(companion, item) || companion.EnqueuedAtTick != capturedTick
            || CompanionPlayedUnit(companion) is not { } unit || unit.ObjectId != item.SourceObjectId
            || !state.CardPlays.Resolved.Any(play => play.PlayId == item.StackItemId && play.Source == unit
                && play.CardNo == item.CardNo && play.PlayerId == item.ControllerId && play.Tick == capturedTick)) return false;
        if (HasArmamentPlayAttachment(companion.UnitEvent))
            return companion.UnitEvent!.ReflexiveTick == capturedTick && companion.UnitEvent.Other?.CardNo == item.CardNo
                && ValidArmamentPlayAttachmentContext(state, companion.UnitEvent, companion.ControllerId);
        if (companion.UnitEvent is { Timing: AgilePlayTiming } agile)
            return agile.CapturedTick == capturedTick && ValidAgilePlayGrant(state, agile,
                companion.SourceObjectId, companion.ControllerId);
        if (companion.FieldContext is { BattlefieldUnitPlay: { } battlefieldPlay } battlefield)
            return battlefieldPlay.CapturedTick == capturedTick && battlefieldPlay.UnitCardNo == item.CardNo
                && ValidFieldContextState(state, battlefield, companion.SourceObjectId, companion.ControllerId,
                    companion.EffectKind, companion.SourceCardNo);
        if (companion.UnitPlayMight is { } might)
            return might.CapturedTick == capturedTick
                && RengarPlayRecoveryRules.Valid(state, might, companion.EffectKind, companion.SourceObjectId, companion.ControllerId);
        if (companion.TurnUnitPlayBoon is { } boon)
            return boon.CapturedTick == capturedTick && boon.UnitCardNo == item.CardNo
                && ValidTurnUnitPlayBoon(state, boon, companion.EffectKind, companion.SourceObjectId,
                    companion.ControllerId, companion.SourceCardNo);
        if (companion.UnitEvent is { SpellInstruction.PlayedUnit: { } played } context)
            return context.CapturedTick == capturedTick && played.CardNo == item.CardNo
                && ValidUnitEvent(state, context, companion.EffectKind, companion.SourceObjectId,
                    companion.SourceCardNo, companion.ControllerId);
        return companion.TurnUnitPlayDraw is { } draw && draw.CapturedTick == capturedTick && draw.UnitCardNo == item.CardNo
            && ValidTurnUnitPlayDraw(state, draw, companion.EffectKind, companion.SourceObjectId,
                companion.ControllerId, companion.SourceCardNo);
    }

    private static PendingCardChoiceState UnitPlayMightChoice(MatchState state, StackItemState item)
    {
        var legal = UnitPlayMightTargets(state, item.ControllerId);
        return new("UNIT-PLAY-MIGHT:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            1, 1, legal, legal, "傲之追猎者：选择一名单位本回合战力 +1，确认目标与适用法盾费用后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareUnitPlayMight(ResolutionResult result, StackItemState item)
    {
        var choice = UnitPlayMightChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        var pending = result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
        if (item.UnitPlayMight!.RequestedTarget is not { } requested || !choice.LegalObjectIds.Contains(requested.ObjectId)
            || state.CardObjects[requested.ObjectId].ObjectGeneration != requested.Generation) return pending;
        var confirmed = ResolveTriggerTargetConfirmation(state, choice, [requested.ObjectId], advanceTick: false);
        return confirmed with { Events = result.Events.Concat(confirmed.Events).ToArray() };
    }

    internal static bool ValidUnitPlayMightChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.UnitPlayMight is null || item.TargetGenerations is not null || !RengarPlayRecoveryRules.ValidStack(state, item)) return false;
        var expected = UnitPlayMightChoice(state, item);
        return choice.ChoiceWindow == expected.ChoiceWindow && choice.ChoiceId == expected.ChoiceId
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId
            && choice.EffectKind == expected.EffectKind && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult ResolveUnitPlayMight(MatchState state, StackItemState item)
    {
        if (!RengarPlayRecoveryRules.ValidStack(state, item) || item.TargetGenerations is null)
            return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var events = ResolveRengarUnitPlayedPowerTrigger(state, NormalizeZonesForSeats(state), cards, item);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
