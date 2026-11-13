using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitControlGrant(string InstanceId, string CreatingStackItemId, ObjectBinding Source,
    string CardNo, string EffectKind, string ControllerId, long CreatedAtTick, int ExecutionIndex);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnitControlLease(ObjectBinding Unit, string UnderlyingControllerId,
    IReadOnlyList<UnitControlGrant> Grants);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DelayedControlLossContext(UnitControlGrant Grant, ObjectBinding Unit,
    int TurnNumber, string TurnPlayerId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? UnitTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string DelayedControlLossPrefix = "DELAYED_CONTROL_LOSS:";
    internal const string DelayedControlLossEffect = "DELAYED_LOSE_CONTROL_THEN_RECALL";

    // CN 478–482: resolution order is the timestamp order. Retain the earlier
    // effects so ending one grant can reveal an earlier, still valid controller.
    private static UnitControlGrant AddUnitControlGrant(MatchState state, StackItemState item,
        Dictionary<string, CardObjectState> cards, string target, string previousController)
    {
        var unit = cards[target];
        var binding = new ObjectBinding(target, unit.ObjectGeneration);
        var prior = unit.UnitControl?.Unit == binding ? unit.UnitControl : null;
        // Use the same replay-stable instruction clock as equipment control.
        var grant = new UnitControlGrant($"{item.StackItemId}:CONTROL:{item.CompletedRepeatExecutions}:{target}",
            item.StackItemId, new(item.SourceObjectId, state.CardObjects.GetValueOrDefault(item.SourceObjectId)?.ObjectGeneration ?? 0),
            item.CardNo!, item.EffectKind, item.ControllerId, RuleChoices.Value!.AnswerTick, item.CompletedRepeatExecutions);
        cards[target] = unit with { UnitControl = new(binding,
            prior?.UnderlyingControllerId ?? unit.EquipmentControl?.UnderlyingControllerId ?? previousController,
            [..prior?.Grants ?? [], grant]) };
        return grant;
    }

    private static void ScheduleControlLoss(MatchState state, UnitControlGrant grant, CardObjectState unit,
        List<string> effects, List<GameEvent> events)
    {
        // CN 359.3.e.16 explicitly cites Hostile Takeover: the end-step event
        // already passed, so this instruction creates no delayed ability.
        if (state.EndStep is not null || state.Phase == MatchPhases.TurnEnd) return;
        var context = new DelayedControlLossContext(grant, new(unit.ObjectId, unit.ObjectGeneration),
            state.TurnNumber, state.TurnPlayerId, unit.TokenFactoryCardNo);
        effects.Add(ControlLossMarker(context));
        events.Add(new("DELAYED_CONTROL_LOSS_SCHEDULED", "生成结束步骤失去本次控制效果并召回的延时技能",
            new Dictionary<string, object?> { ["sourceObjectId"] = grant.Source.ObjectId,
                ["targetObjectId"] = unit.ObjectId, ["turnNumber"] = state.TurnNumber }));
    }

    internal static string ControlLossMarker(DelayedControlLossContext context)
        => DelayedControlLossPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(context));

    internal static DelayedControlLossContext? ReadControlLoss(string marker)
    {
        try
        {
            if (!marker.StartsWith(DelayedControlLossPrefix, StringComparison.Ordinal)) return null;
            var context = JsonSerializer.Deserialize<DelayedControlLossContext>(Convert.FromBase64String(marker[DelayedControlLossPrefix.Length..]));
            return context is not null && ControlLossMarker(context) == marker ? context : null;
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException) { return null; }
    }

    internal static bool ValidUnitControlGrant(MatchState state, UnitControlGrant? grant, ObjectBinding unit)
        => grant is { Source: not null } && grant.Source.Generation >= 0 && unit.Generation >= 0
            && !string.IsNullOrWhiteSpace(unit.ObjectId) && !string.IsNullOrWhiteSpace(grant.CreatingStackItemId)
            && grant.ExecutionIndex >= 0 && grant.CreatedAtTick > 0 && grant.CreatedAtTick <= state.Tick
            && grant.InstanceId == $"{grant.CreatingStackItemId}:CONTROL:{grant.ExecutionIndex}:{unit.ObjectId}"
            && state.Seats.ContainsKey(grant.ControllerId ?? "")
            && CardBehaviorRegistry.TryGetByEffectKind(grant.EffectKind ?? "", out var behavior)
            && behavior.CardNo == grant.CardNo && (behavior.GainsControlOfTargetToBase || behavior.GainsControlOfTargetToBattlefield)
            && !string.IsNullOrWhiteSpace(grant.Source.ObjectId)
            // The effect is independent of its source after birth. Its accepted
            // capture receipt authenticates that birth, including ceased sources.
            && (!state.CardObjects.TryGetValue(grant.Source.ObjectId, out var source)
                || source.ObjectGeneration >= grant.Source.Generation);

    internal static bool ValidUnitControlLeases(MatchState state)
        => UnitControlFrames(state).All(frame => frame.CardObjects.Values.All(card =>
            card.UnitControl is not { } lease || lease.Unit is not null
                && lease.Unit == new ObjectBinding(card.ObjectId, card.ObjectGeneration)
                && IsObjectOnField(frame.PlayerZones, card.ObjectId)
                && frame.Seats.ContainsKey(lease.UnderlyingControllerId ?? "")
                && lease.Grants is { Count: > 0 } && lease.Grants.All(g => ValidUnitControlGrant(frame, g, lease.Unit))
                && lease.Grants.Select(g => g.InstanceId).Distinct(StringComparer.Ordinal).Count() == lease.Grants.Count
                && lease.Grants.Zip(lease.Grants.Skip(1)).All(pair => pair.First.CreatedAtTick <= pair.Second.CreatedAtTick)
                && card.ControllerId == LayeredControlController(card, lease.UnderlyingControllerId)));

    internal static string? LayeredControlController(CardObjectState card, string? fallback)
    {
        var unit = card.UnitControl?.Grants?.LastOrDefault();
        var equipment = card.EquipmentControl?.Grants?.LastOrDefault();
        return unit is not null && (equipment is null || unit.CreatedAtTick >= equipment.CreatedAtTick)
            ? unit.ControllerId : equipment?.ControllerId ?? card.UnitControl?.UnderlyingControllerId
                ?? card.EquipmentControl?.UnderlyingControllerId ?? fallback;
    }

    internal static CardObjectState EndUnitControlGrant(CardObjectState unit, string instanceId)
    {
        if (unit.UnitControl is not { } lease || !lease.Grants.Any(g => g.InstanceId == instanceId)) return unit;
        var remaining = lease.Grants.Where(g => g.InstanceId != instanceId).ToArray();
        unit = unit with { UnitControl = remaining.Length == 0 ? null : lease with { Grants = remaining } };
        return unit with { ControllerId = LayeredControlController(unit, lease.UnderlyingControllerId) };
    }

    internal static bool ValidControlLoss(MatchState state, DelayedControlLossContext? context)
        => context is { Unit: not null } && ValidUnitControlGrant(state, context.Grant, context.Unit)
            && CardBehaviorRegistry.TryGetByEffectKind(context.Grant.EffectKind, out var behavior) && behavior.LosesGrantedControlAtEndStep
            && context.TurnNumber == state.TurnNumber && context.TurnPlayerId == state.TurnPlayerId
            && (state.CardObjects.TryGetValue(context.Unit.ObjectId, out var unit)
                ? unit.ObjectGeneration >= context.Unit.Generation && unit.TokenFactoryCardNo == context.UnitTokenFactoryCardNo
                : context.UnitTokenFactoryCardNo is not null && P6TokenFactoryCatalog.TryGetByCardNo(context.UnitTokenFactoryCardNo, out var token)
                    && token.Tags.Contains(CardObjectTags.UnitCard));

    private static string ControlLossTriggerId(DelayedControlLossContext context) => context.Grant.InstanceId + ":END";

    private static bool ControlLossEndWindow(MatchState state)
        => state.EndStep is { } end && end.PlayerId == state.TurnPlayerId && end.TurnNumber == state.TurnNumber
            && state.Phase == MatchPhases.TurnEnd;

    internal static bool ValidControlLossStack(MatchState state, StackItemState item)
        => item.DelayedControlLoss is { } context && ValidControlLoss(state, context) && ControlLossEndWindow(state)
            && item.EffectKind == DelayedControlLossEffect && item.StackItemId == "ordered-" + ControlLossTriggerId(context)
            && item.SourceObjectId == context.Grant.Source.ObjectId && item.ControllerId == context.Grant.ControllerId
            && item.CardNo == context.Grant.CardNo && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null;

    internal static bool ValidControlLossTrigger(MatchState state, TriggerQueueItemState item)
        => item.DelayedControlLoss is { } context && ValidControlLoss(state, context) && ControlLossEndWindow(state)
            && item.EffectKind == DelayedControlLossEffect && item.TriggerId == ControlLossTriggerId(context)
            && item.TriggeredByEventKind == "TURN_END_STEP_STARTED"
            && item.SourceObjectId == context.Grant.Source.ObjectId && item.ControllerId == context.Grant.ControllerId
            && item.SourceCardNo == context.Grant.CardNo;

    internal static IEnumerable<DelayedControlLossContext> CurrentControlLosses(MatchState state)
        => state.UntilEndOfTurnEffects.Where(e => e.StartsWith(DelayedControlLossPrefix, StringComparison.Ordinal))
            .Select(ReadControlLoss).OfType<DelayedControlLossContext>()
            .Concat(state.StackItems.Select(s => s.DelayedControlLoss).OfType<DelayedControlLossContext>())
            .Concat(state.TriggerQueue.Select(t => t.DelayedControlLoss).OfType<DelayedControlLossContext>())
            .Where(context => context is { Grant: not null, Unit: not null });

    private static IEnumerable<TriggerQueueItemState> CaptureControlLossEndAbilities(MatchState state)
    {
        foreach (var marker in state.UntilEndOfTurnEffects.Where(e => e.StartsWith(DelayedControlLossPrefix, StringComparison.Ordinal)))
        {
            var context = ReadControlLoss(marker);
            if (!ValidControlLoss(state, context)) throw new InvalidOperationException("Invalid delayed control loss instruction.");
            yield return new(ControlLossTriggerId(context!), context!.Grant.ControllerId, context.Grant.Source.ObjectId,
                DelayedControlLossEffect, "TURN_END_STEP_STARTED", TimingStates.NeutralClosed)
                { SourceCardNo = context.Grant.CardNo, DelayedControlLoss = context };
        }
    }

    private static StackItemState ControlLossStack(TriggerQueueItemState trigger)
        => new("ordered-" + trigger.TriggerId, trigger.ControllerId, trigger.SourceObjectId, trigger.EffectKind,
            trigger.SourceCardNo, [], 0, 0, [], timingContext: trigger.TimingContext)
            { DelayedControlLoss = trigger.DelayedControlLoss };

    private static StackResolutionResult ResolveControlLoss(MatchState state, StackItemState item)
    {
        if (!ValidControlLossStack(state, item)) throw new InvalidOperationException("Invalid delayed control loss skill.");
        var context = item.DelayedControlLoss!;
        // CN 124 / 393: the independent skill survives its source, but can only
        // affect the originally referenced object, never a later incarnation.
        if (!state.CardObjects.TryGetValue(context.Unit.ObjectId, out var unit)
            || unit.ObjectGeneration != context.Unit.Generation || !IsObjectOnField(state.PlayerZones, unit.ObjectId))
            return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent>();
        if (unit.UnitControl is { } lease && lease.Grants.Any(g => g.InstanceId == context.Grant.InstanceId))
        {
            unit = EndUnitControlGrant(unit, context.Grant.InstanceId);
            var controller = unit.ControllerId!;
            cards[unit.ObjectId] = unit;
            events.Add(new("UNIT_CONTROL_RETURNED", "结束指定控制效果，按仍然有效的效果重新确定控制者",
                new Dictionary<string, object?> { ["sourceObjectId"] = context.Grant.Source.ObjectId,
                    ["targetObjectId"] = unit.ObjectId, ["ownerId"] = unit.OwnerId,
                    ["controllerId"] = controller, ["previousControllerId"] = state.CardObjects[unit.ObjectId].ControllerId }));
        }
        var currentController = unit.ControllerId ?? unit.OwnerId!;
        var origin = FindFieldObjectLocation(zones, unit.ObjectId)!.Value;
        // CN 456 / 719: recall uses its current controller and is not movement.
        RecallAttachedGroupToBase(zones, cards, unit.ObjectId, currentController);
        events.Add(new("UNIT_RECALLED_TO_CONTROLLER_BASE", "将单位及其附属牌召回当前控制者的基地",
            new Dictionary<string, object?> { ["targetObjectId"] = unit.ObjectId, ["ownerId"] = unit.OwnerId,
                ["controllerId"] = currentController, ["originZone"] = origin.Zone, ["destinationZone"] = "BASE" }));
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
    }
}
