using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PaidUnitTriggerOrigin(string Event, long CapturedTick, long FaceRevision,
    string RuleTextInstanceId, bool CostPaid = false, CeasedEmpoweredSource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    internal const string PaidUnitStunKind = "PLAY_OR_ATTACK_OPTIONAL_PAID_STUN";
    private const string PaidUnitPlayStunCapture = "paidUnitPlayStunSources";
    private sealed record PaidUnitStunCapture(string SourceId, string Controller, string InstanceId,
        FieldTriggerContext Context);

    private static TriggerSpec? PaidUnitStunSpec(string? cardNo) => SourceUnitPlayedTriggerSpecRules.TriggersForCard(cardNo)
        .FirstOrDefault(t => t.Kind == TriggerKinds.UnitPlayedOrAttacksOptionalPaidStun
            && t.Timing == TriggerTimings.SourceUnitPlayedOrAttack && t.TargetScope == TriggerTargetScopes.AnyUnitOnField
            && t.Optional == true && t.ManaCost is > 0);

    private static GameEvent CapturePaidUnitPlayStun(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardObjectState entered)
    {
        if (entered.IsFaceDown || ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        var controller = EffectiveFieldControllerId(zones, entered.ObjectId, entered);
        var sources = AttachedRuleTextRules.Instances(zones, cards, entered)
            .Where(instance => PaidUnitStunSpec(instance.CardNo) is not null)
            .Select(instance => new PaidUnitStunCapture(entered.ObjectId, controller, instance.Id,
                new(instance.CardNo, PaidUnitStunKind, entered.ObjectGeneration) {
                    Origin = instance.Origin, SourceTokenFactoryCardNo = entered.TokenFactoryCardNo,
                    PaidUnitTrigger = new("PLAY", 0, instance.Origin?.FaceRevision ?? entered.CopyFaceTransitions?.Count ?? 0, instance.Id) }))
            .ToArray();
        return sources.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [PaidUnitPlayStunCapture] = sources } };
    }

    private static ResolutionResult QueuePaidUnitPlayStuns(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList(); var found = false;
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index))) {
            if (ev.Payload.GetValueOrDefault(PaidUnitPlayStunCapture) is not PaidUnitStunCapture[] sources) continue;
            found = true;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != PaidUnitPlayStunCapture).ToDictionary(p => p.Key, p => p.Value) };
            foreach (var (source, ordinal) in sources.Select((source, ordinal) => (source, ordinal))) {
                var generation = source.Context.SourceGeneration;
                if (before.CardObjects.TryGetValue(source.SourceId, out var old) && !IsObjectOnField(before.PlayerZones, source.SourceId))
                    generation = Math.Max(generation, old.ObjectGeneration + 1);
                var origin = source.Context.PaidUnitTrigger! with {
                    CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick,
                    RuleTextInstanceId = source.Context.Origin is null ? $"PRINTED:{source.SourceId}:{generation}" : source.InstanceId };
                var context = source.Context with { SourceGeneration = generation, PaidUnitTrigger = origin };
                var trigger = new TriggerQueueItemState($"paid-unit-play-{result.State.Tick}-{index}-{ordinal}", source.Controller,
                    source.SourceId, FieldTriggerEffect, ev.Kind) { FieldContext = context, SourceCardNo = context.CardNo };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, source.SourceId, "__spectator__") } });
            }
        }
        if (!found) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static TriggerQueueItemState[] CapturePaidUnitAttackStuns(MatchState state, IEnumerable<string> attackers,
        string battlefield, string timing = TimingStates.NeutralClosed, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)) {
            if (!state.CardObjects.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            var controller = EffectiveFieldControllerId(state.PlayerZones, id, source);
            foreach (var instance in AttachedRuleTextRules.Instances(state, source)) {
                if (PaidUnitStunSpec(instance.CardNo) is null) continue;
                var context = new FieldTriggerContext(instance.CardNo, PaidUnitStunKind, source.ObjectGeneration,
                    battlefield, returnFocusPlayer) {
                    Origin = instance.Origin, SourceTokenFactoryCardNo = source.TokenFactoryCardNo,
                    PaidUnitTrigger = new("ATTACK", RuleChoices.Value?.CompletionTick ?? state.Tick,
                        instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, instance.Id) };
                queue.Add(new($"paid-unit-attack-{state.Tick + 1}-{queue.Count}", controller, id,
                    FieldTriggerEffect, "BATTLE_DECLARED", timing) { FieldContext = context, SourceCardNo = context.CardNo });
            }
        }
        return queue.ToArray();
    }

    private static bool ValidPaidUnitStunShape(FieldTriggerContext context, string effect, string? cardNo)
    {
        if (context.Kind != PaidUnitStunKind || effect != FieldTriggerEffect || cardNo is not null && cardNo != context.CardNo
            || PaidUnitStunSpec(context.CardNo) is null || context.SourceGeneration < 0
            || context.PaidUnitTrigger is not { CapturedTick: >= 0, FaceRevision: >= 0 } birth
            || birth.Event is not ("PLAY" or "ATTACK") || string.IsNullOrWhiteSpace(birth.RuleTextInstanceId)
            || context.RecycledBoon is not null || context.EmpoweredTransition is not null || context.SourceLocation is not null
            || context.AffectedUnit is not null || context.AffectedUnitCeasedToExist || context.AttackConditionFrame is not null
            || birth.Event == "PLAY" && (context.BattlefieldId is not null || context.ReturnFocusPlayerId is not null)
            || birth.Event == "ATTACK" && string.IsNullOrWhiteSpace(context.BattlefieldId)
            || context.SourceTokenFactoryCardNo is null && (context.SourceCeasedToExist || birth.CeasedSource is not null)
            || context.SourceTokenFactoryCardNo is not null && (!P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                || !token.Tags.Contains(CardObjectTags.UnitCard) || context.SourceCeasedToExist != (birth.CeasedSource is not null))) return false;
        return true;
    }

    private static bool ValidPaidUnitStunSource(MatchState state, FieldTriggerContext context, string source, string controller)
    {
        var birth = context.PaidUnitTrigger!;
        if (string.IsNullOrWhiteSpace(source) || !state.Seats.ContainsKey(controller) || birth.CapturedTick > state.Tick
            || context.ReturnFocusPlayerId is not null && !state.Seats.ContainsKey(context.ReturnFocusPlayerId)
            || birth.Event == "ATTACK" && BattlefieldLocalRules.Battlefield(state, context.BattlefieldId) is null) return false;
        if (context.SourceCeasedToExist) {
            if (state.CardObjects.ContainsKey(source) || birth.CeasedSource is not { } ceased
                || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick || ceased.CardNo != context.CardNo) return false;
        } else if (!state.CardObjects.TryGetValue(source, out var current) || current.ObjectGeneration < context.SourceGeneration
            || current.TokenFactoryCardNo != context.SourceTokenFactoryCardNo) return false;
        return context.Origin is null
            ? birth.RuleTextInstanceId == $"PRINTED:{source}:{context.SourceGeneration}"
                && PredictBindingMatches(state, new(source, context.SourceGeneration), context.CardNo, birth.FaceRevision,
                    birth.CapturedTick, context.SourceTokenFactoryCardNo)
            : context.Origin.Equipment is { } equipment
                && birth.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{context.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, source, context.SourceGeneration, context.CardNo);
    }

    private static StackResolutionResult ResolvePaidUnitStun(MatchState state, StackItemState item)
    {
        if (item.FieldContext?.PaidUnitTrigger is not { CostPaid: true }
            || !ValidFieldContextState(state, item.FieldContext, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid paid unit trigger context.");
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>(); var target = item.TargetObjectIds.SingleOrDefault();
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        if (target is not null && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards.GetValueOrDefault(target)?.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(target, StringComparer.Ordinal) && CanExecuteStun(cards[target])) {
            cards[target] = cards[target] with { UntilEndOfTurnEffects = cards[target].UntilEndOfTurnEffects
                .Append("STUNNED").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
            events.Add(new("STATUS_EFFECT_APPLIED", "眩晕目标单位", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = visibleSource, ["targetObjectId"] = target,
                ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED", ["duration"] = "UNTIL_END_OF_TURN" }));
            events.AddRange(ResolveEclipseVanguardStunTriggers(zones, cards, item, events));
        }
        events.Add(new("TRIGGER_RESOLVED", "触发技能结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = visibleSource }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events };
    }
}
