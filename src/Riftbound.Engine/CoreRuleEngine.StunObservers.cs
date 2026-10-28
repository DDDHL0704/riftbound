using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StunObserverOrigin(ObjectBinding Source, ObjectBinding Stunned, string StunnedCardNo,
    string Actor, string EventGroup, long CapturedTick, long SourceFaceRevision, long StunnedFaceRevision,
    string RuleTextInstanceId, bool SourceIsLegend, string? StunnedTokenFactoryCardNo = null, CeasedEmpoweredSource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    internal const string StunReadyPowerKind = "ENEMY_STUNNED_SOURCE_READY_POWER";
    internal const string StunBoonKind = "ENEMY_STUNNED_FRIENDLY_BOON";
    private const string StunCaptureKey = "stunObserverSources";
    private sealed record CapturedStunObserver(string SourceId, string Controller, string InstanceId, FieldTriggerContext Context);

    private static readonly Lazy<IReadOnlySet<string>> StunBoonFaces = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(card => Regex.IsMatch(card.CardEffect,
                @"每当你眩晕任意数量的敌方单位时[，,]\s*给予一名友方单位(?:\{\{)?增益(?:\}\})?[。.]?",
                RegexOptions.CultureInvariant))
            .Select(card => card.CardNo).ToHashSet(StringComparer.Ordinal));

    private static bool IsStunObserverKind(string kind) => kind is StunReadyPowerKind or StunBoonKind;
    private static CardBehaviorDefinition? StunReadyBehavior(string? cardNo)
        => CardBehaviorRegistry.TryGetByCardNo(cardNo ?? "", out var behavior)
            && behavior.SourceReadiesWhenControllerStunsEnemyUnit
            && behavior.SourcePowerOnControllerStunsEnemyUnitAmount > 0
            && !string.IsNullOrWhiteSpace(behavior.SourceStunEnemyUnitTriggerEffectKind) ? behavior : null;

    // Capture the responsible player and every active text instance at the
    // actual stun instruction, before later instructions can move or copy them.
    private static GameEvent CaptureStunObservers(GameEvent ev, IReadOnlyDictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, StackItemState cause, string targetId)
    {
        var actor = cause.ControllerId;
        if (!cards.TryGetValue(targetId, out var target) || !IsEnemyFieldUnit(zones, cards, actor, targetId)
            || target.CardNo is not { Length: > 0 } targetCard) return ev;
        var observed = new ObjectBinding(targetId, target.ObjectGeneration);
        var captures = new List<CapturedStunObserver>();
        void Add(CardObjectState source, RuleTextInstance instance, bool isLegend, string kind)
        {
            var birth = new StunObserverOrigin(new(source.ObjectId, source.ObjectGeneration), observed, targetCard, actor,
                cause.StackItemId, 0, instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
                target.CopyFaceTransitions?.Count ?? 0, instance.Id, isLegend, target.TokenFactoryCardNo);
            captures.Add(new(source.ObjectId, actor, instance.Id, new(instance.CardNo, kind, source.ObjectGeneration) {
                Origin = instance.Origin, SourceTokenFactoryCardNo = source.TokenFactoryCardNo, StunObserver = birth }));
        }
        foreach (var id in GetControlledFieldUnitObjectIds(zones, cards, actor).Order(StringComparer.Ordinal)) {
            if (!cards.TryGetValue(id, out var source) || !IsFaceUpNonStandbyUnit(source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, source)) {
                if (StunReadyBehavior(instance.CardNo) is not null) Add(source, instance, false, StunReadyPowerKind);
                if (StunBoonFaces.Value.Contains(instance.CardNo)) Add(source, instance, false, StunBoonKind);
            }
        }
        if (zones.TryGetValue(actor, out var own))
            foreach (var id in own.LegendZone.Order(StringComparer.Ordinal)) {
                if (!cards.TryGetValue(id, out var source) || source.IsFaceDown
                    || !SourceObjectControlledByPlayerOrLegacyOwned(source, actor)) continue;
                foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, source))
                    if (StunBoonFaces.Value.Contains(instance.CardNo)) Add(source, instance, true, StunBoonKind);
            }
        return captures.Count == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [StunCaptureKey] = captures.ToArray() } };
    }

    private static ResolutionResult QueueStunObservers(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList(); var queue = result.State.TriggerQueue.ToList(); var found = false;
        var legendGroups = new HashSet<(string Group, string Source, string Instance)>();
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index))) {
            if (ev.Payload.GetValueOrDefault(StunCaptureKey) is not CapturedStunObserver[] captures) continue;
            found = true;
            events[index] = ev with { Payload = ev.Payload.Where(p => p.Key != StunCaptureKey).ToDictionary(p => p.Key, p => p.Value) };
            if (result.State.Status != MatchStatuses.InProgress) continue;
            foreach (var (capture, ordinal) in captures.Select((capture, ordinal) => (capture, ordinal))) {
                var context = capture.Context; var birth = context.StunObserver!;
                if (context.Kind == StunBoonKind && !legendGroups.Add((birth.EventGroup, capture.SourceId, capture.InstanceId))) continue;
                var generation = context.SourceGeneration;
                if (before.CardObjects.TryGetValue(capture.SourceId, out var old)
                    && !IsObjectOnField(before.PlayerZones, capture.SourceId) && !birth.SourceIsLegend)
                    generation = Math.Max(generation, old.ObjectGeneration + 1);
                birth = birth with { Source = birth.Source with { Generation = generation },
                    CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick,
                    RuleTextInstanceId = context.Origin is null ? $"PRINTED:{capture.SourceId}:{generation}" : capture.InstanceId };
                context = context with { SourceGeneration = generation, StunObserver = birth };
                var trigger = new TriggerQueueItemState($"stun-observer-{result.State.Tick}-{index}-{ordinal}", capture.Controller,
                    capture.SourceId, FieldTriggerEffect, "STATUS_EFFECT_APPLIED") { FieldContext = context, SourceCardNo = context.CardNo };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, capture.SourceId, "__spectator__") } });
            }
        }
        if (!found) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidStunObserverShape(FieldTriggerContext context, string effect, string? cardNo)
    {
        if (!IsStunObserverKind(context.Kind) || effect != FieldTriggerEffect || cardNo is not null && cardNo != context.CardNo
            || context.SourceGeneration < 0 || context.StunObserver is not { Source: { Generation: >= 0 } source,
                Stunned: { Generation: >= 0 } stunned, CapturedTick: >= 0, SourceFaceRevision: >= 0, StunnedFaceRevision: >= 0 } origin
            || source.Generation != context.SourceGeneration || string.IsNullOrWhiteSpace(source.ObjectId)
            || string.IsNullOrWhiteSpace(stunned.ObjectId) || string.IsNullOrWhiteSpace(origin.StunnedCardNo)
            || string.IsNullOrWhiteSpace(origin.Actor) || string.IsNullOrWhiteSpace(origin.EventGroup)
            || string.IsNullOrWhiteSpace(origin.RuleTextInstanceId) || context.BattlefieldId is not null
            || context.ReturnFocusPlayerId is not null || context.RecycledBoon is not null || context.EmpoweredTransition is not null
            || context.SourceLocation is not null || context.AffectedUnit is not null || context.AffectedUnitCeasedToExist
            || context.AttackConditionFrame is not null
            || (context.Kind == StunReadyPowerKind ? origin.SourceIsLegend || StunReadyBehavior(context.CardNo) is null
                : !StunBoonFaces.Value.Contains(context.CardNo))
            || origin.SourceIsLegend && (context.Origin is not null || context.SourceTokenFactoryCardNo is not null)
            || context.SourceTokenFactoryCardNo is null && (context.SourceCeasedToExist || origin.CeasedSource is not null)
            || context.SourceTokenFactoryCardNo is not null && (!P6TokenFactoryCatalog.TryGetByCardNo(context.SourceTokenFactoryCardNo, out var token)
                || !token.Tags.Contains(CardObjectTags.UnitCard) || context.SourceCeasedToExist != (origin.CeasedSource is not null))) return false;
        return true;
    }

    private static bool ValidStunObserverSource(MatchState state, FieldTriggerContext context, string sourceId, string controller)
    {
        var birth = context.StunObserver!;
        if (birth.Source.ObjectId != sourceId || birth.Actor != controller || !state.Seats.ContainsKey(controller)
            || birth.CapturedTick > state.Tick || !PredictBindingMatches(state, birth.Stunned, birth.StunnedCardNo,
                birth.StunnedFaceRevision, birth.CapturedTick, birth.StunnedTokenFactoryCardNo)) return false;
        if (context.SourceCeasedToExist) {
            if (state.CardObjects.ContainsKey(sourceId) || birth.CeasedSource is not { } ceased
                || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick || ceased.CardNo != context.CardNo) return false;
        } else if (!state.CardObjects.TryGetValue(sourceId, out var current) || current.ObjectGeneration < context.SourceGeneration
            || current.TokenFactoryCardNo != context.SourceTokenFactoryCardNo) return false;
        return context.Origin is null
            ? birth.RuleTextInstanceId == $"PRINTED:{sourceId}:{context.SourceGeneration}"
                && PredictBindingMatches(state, birth.Source, context.CardNo, birth.SourceFaceRevision, birth.CapturedTick, context.SourceTokenFactoryCardNo)
            : context.Origin.Equipment is { } equipment
                && birth.RuleTextInstanceId == $"ATTACHED:{equipment.ObjectId}:{equipment.Generation}:{context.Origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, context.Origin, sourceId, context.SourceGeneration, context.CardNo);
    }

    private static StackResolutionResult ResolveStunObserver(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured stun observer.");
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        var visibleSource = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        if (context.Kind == StunReadyPowerKind && cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.SourceGeneration && IsFaceUpNonStandbyUnit(source)
            && IsObjectOnField(state.PlayerZones, source.ObjectId) && StunReadyBehavior(context.CardNo) is { } behavior) {
            var effect = behavior with { ManaCost = 0, EffectKind = behavior.SourceStunEnemyUnitTriggerEffectKind,
                PowerModifierAmount = behavior.SourcePowerOnControllerStunsEnemyUnitAmount };
            source = ApplyReadyState(source, effect, item, source.ObjectId, out var ready);
            if (ready is not null) events.Add(ready);
            cards[source.ObjectId] = ApplyPowerModifier(state, source, effect, item, source.ObjectId,
                effect.PowerModifierAmount, out var power);
            events.Add(power);
        }
        else if (context.Kind == StunBoonKind && item.TargetObjectIds.SingleOrDefault() is { } target
            && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards.GetValueOrDefault(target)?.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(target, StringComparer.Ordinal)) {
            using var instruction = new MightInstructionScope(state, NormalizeZonesForSeats(state), cards, events);
            GrantLegendBoon(cards, target, item.ControllerId, item.SourceObjectId, "LEGEND_TRIGGER_ENEMY_STUNNED_GRANT_BOON", events);
        }
        events.Add(new("TRIGGER_RESOLVED", "眩晕观察者技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = visibleSource, ["controllerId"] = item.ControllerId,
            ["effectKind"] = context.Kind == StunReadyPowerKind ? StunReadyBehavior(context.CardNo)!.SourceStunEnemyUnitTriggerEffectKind : StunBoonKind }));
        if (visibleSource == "HIDDEN") events = events.Select(ev => ev.Payload.GetValueOrDefault("sourceObjectId") as string == item.SourceObjectId
            ? ev with { Payload = new Dictionary<string, object?>(ev.Payload) { ["sourceObjectId"] = "HIDDEN" } } : ev).ToList();
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
