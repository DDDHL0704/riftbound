using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EquipmentDestructionOrigin(ObjectBinding Source, ObjectBinding Equipment, string EquipmentCardNo,
    string OwnerId, string ControllerId, string BattlefieldId, string InstanceId, long CapturedTick,
    long SourceFaceRevision, long EquipmentFaceRevision, string? EquipmentTokenFactoryCardNo = null,
    CeasedEmpoweredSource? CeasedSource = null);

public sealed partial class CoreRuleEngine
{
    internal const string EquipmentDeathReturnKind = "FRIENDLY_BATTLEFIELD_EQUIPMENT_DESTROYED_RETURN_HAND";
    private const string EquipmentDeathCaptureKey = "equipmentDestructionObservers";
    private sealed record CapturedEquipmentDestructionObserver(string SourceId, string Controller, FieldTriggerContext Context);
    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> EquipmentDeathReturnSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: TriggerParser.Parse(c.CardEffect).SingleOrDefault(t => t.Kind == EquipmentDeathReturnKind)))
            .Where(c => c.Spec is not null).ToDictionary(c => c.CardNo, c => c.Spec!, StringComparer.Ordinal));

    private static CapturedEquipmentDestructionObserver[] CaptureEquipmentDestructionObservers(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState gear)
    {
        var frame = RuleChoices.Value!;
        if (gear.IsFaceDown || !gear.Tags.Contains(CardObjectTags.EquipmentCard) || gear.CardNo is null
            || !TryGetPreciseFieldLocation(zones, frame.CurrentDestructionLocations ?? state.ObjectLocations, gear.ObjectId, out var place)
            || place.Zone != "BATTLEFIELD" || place.BattlefieldObjectId is not { } battlefield) return [];
        var controller = EffectiveFieldControllerId(zones, gear.ObjectId, gear);
        var owner = NonFieldDestinationOwner(zones, gear, controller);
        return cards.Values.Where(source => IsFaceUpNonStandbyUnit(source) && IsObjectOnField(zones, source.ObjectId)
                && !frame.CurrentDestructions.Contains(source.ObjectId)
                && EffectiveFieldControllerId(zones, source.ObjectId, source) == controller)
            .OrderBy(source => source.ObjectId, StringComparer.Ordinal)
            .SelectMany(source => AttachedRuleTextRules.Instances(zones, cards, source)
                .Where(i => EquipmentDeathReturnSpecs.Value.ContainsKey(i.CardNo)
                    && (i.Origin is null || !frame.CurrentDestructions.Contains(i.Origin.Equipment.ObjectId)))
                .Select(i => new CapturedEquipmentDestructionObserver(source.ObjectId, controller,
                    new(i.CardNo, EquipmentDeathReturnKind, source.ObjectGeneration) { Origin = i.Origin,
                        SourceTokenFactoryCardNo = source.TokenFactoryCardNo,
                        EquipmentDestruction = new(new(source.ObjectId, source.ObjectGeneration), new(gear.ObjectId, gear.ObjectGeneration),
                            gear.CardNo, owner, controller, battlefield, i.Id, state.Tick + 1,
                            source.CopyFaceTransitions?.Count ?? 0, gear.CopyFaceTransitions?.Count ?? 0, gear.TokenFactoryCardNo) })))
            .ToArray();
    }

    private static ResolutionResult QueueEquipmentDestructionObservers(ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Payload.ContainsKey(EquipmentDeathCaptureKey))) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index)))
        {
            events.Add(ev with { Payload = ev.Payload.Where(p => p.Key != EquipmentDeathCaptureKey).ToDictionary(p => p.Key, p => p.Value) });
            if (result.State.Status != MatchStatuses.InProgress
                || ev.Payload.GetValueOrDefault(EquipmentDeathCaptureKey) is not CapturedEquipmentDestructionObserver[] captures) continue;
            foreach (var (capture, ordinal) in captures.Select((capture, ordinal) => (capture, ordinal)))
            {
                var context = capture.Context with { EquipmentDestruction = capture.Context.EquipmentDestruction! with {
                    CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick } };
                var trigger = new TriggerQueueItemState($"equipment-death-{result.State.Tick}-{index}-{ordinal}", capture.Controller,
                    capture.SourceId, FieldTriggerEffect, ev.Kind) { SourceCardNo = context.CardNo, FieldContext = context };
                queue.Add(trigger);
                var queued = BuildTriggerQueuedEvent(trigger);
                events.Add(queued with { Payload = new Dictionary<string, object?>(queued.Payload) {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, capture.SourceId, "__spectator__") } });
            }
        }
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidEquipmentDeathShape(FieldTriggerContext c, string effect, string? cardNo)
        => effect == FieldTriggerEffect && c.Kind == EquipmentDeathReturnKind && (cardNo is null || c.CardNo == cardNo)
            && EquipmentDeathReturnSpecs.Value.ContainsKey(c.CardNo) && c.SourceGeneration >= 0
            && c.EquipmentDestruction is { Source: { Generation: >= 0 } source, Equipment: { Generation: >= 0 } gear,
                CapturedTick: > 0, SourceFaceRevision: >= 0, EquipmentFaceRevision: >= 0 } birth
            && source.Generation == c.SourceGeneration && source.ObjectId != gear.ObjectId
            && !string.IsNullOrWhiteSpace(source.ObjectId) && !string.IsNullOrWhiteSpace(gear.ObjectId)
            && !string.IsNullOrWhiteSpace(birth.EquipmentCardNo) && !string.IsNullOrWhiteSpace(birth.InstanceId)
            && !string.IsNullOrWhiteSpace(birth.BattlefieldId) && !string.IsNullOrWhiteSpace(birth.ControllerId)
            && !string.IsNullOrWhiteSpace(birth.OwnerId) && c.BattlefieldId is null && c.ReturnFocusPlayerId is null
            && c.PaidUnitTrigger is null && c.Disarm is null && c.StunObserver is null && c.SelfAction is null
            && c.AttackMightDamage is null && c.RecycledBoon is null && c.EmpoweredTransition is null
            && c.SourceLocation is null && c.AffectedUnit is null && !c.AffectedUnitCeasedToExist && c.AttackConditionFrame is null
            && (c.SourceTokenFactoryCardNo is null ? !c.SourceCeasedToExist && birth.CeasedSource is null
                : P6TokenFactoryCatalog.IsTokenFactory(c.SourceTokenFactoryCardNo) && c.SourceCeasedToExist == (birth.CeasedSource is not null))
            && (birth.EquipmentTokenFactoryCardNo is null || P6TokenFactoryCatalog.TryGetByCardNo(birth.EquipmentTokenFactoryCardNo, out var token)
                && token.Tags.Contains(CardObjectTags.EquipmentCard));

    private static bool ValidEquipmentDeathSource(MatchState state, FieldTriggerContext c, string sourceId, string controller)
    {
        var birth = c.EquipmentDestruction!;
        if (birth.Source.ObjectId != sourceId || birth.ControllerId != controller || !state.Seats.ContainsKey(controller)
            || !state.Seats.ContainsKey(birth.OwnerId) || birth.CapturedTick > state.Tick
            || BattlefieldLocalRules.Battlefield(state, birth.BattlefieldId) is null
            || !PredictBindingMatches(state, birth.Equipment, birth.EquipmentCardNo, birth.EquipmentFaceRevision,
                birth.CapturedTick, birth.EquipmentTokenFactoryCardNo)) return false;
        if (c.SourceCeasedToExist && (state.CardObjects.ContainsKey(sourceId) || birth.CeasedSource is not { } ceased
            || ceased.Tick < birth.CapturedTick || ceased.Tick > state.Tick || ceased.CardNo != c.CardNo)) return false;
        return c.Origin is { } copy
            ? birth.InstanceId == $"ATTACHED:{copy.Equipment.ObjectId}:{copy.Equipment.Generation}:{copy.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, copy, sourceId, c.SourceGeneration, c.CardNo)
            : birth.InstanceId == $"PRINTED:{sourceId}:{c.SourceGeneration}"
                && PredictBindingMatches(state, birth.Source, c.CardNo, birth.SourceFaceRevision, birth.CapturedTick, c.SourceTokenFactoryCardNo);
    }

    internal static bool ValidEquipmentDeathStack(StackItemState item)
        => item.FieldContext?.Kind != EquipmentDeathReturnKind || item.FieldContext.EquipmentDestruction is not null
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
            && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.Destination.Length == 0;

    private static StackResolutionResult ResolveEquipmentDeathReturn(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            || !ValidEquipmentDeathStack(item) || item.TriggerCost is null) throw new InvalidOperationException("Invalid captured equipment destruction skill.");
        var birth = context.EquipmentDestruction!; var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
        var visible = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__");
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "装备被摧毁后的返回技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = visible, ["effectKind"] = context.Kind, ["controllerId"] = item.ControllerId }) };
        if (birth.EquipmentTokenFactoryCardNo is null && cards.TryGetValue(birth.Equipment.ObjectId, out var gear)
            && gear.ObjectGeneration == birth.Equipment.Generation + 1 && zones[birth.OwnerId].Graveyard.Contains(gear.ObjectId))
        {
            zones[birth.OwnerId] = zones[birth.OwnerId] with {
                Graveyard = RemoveFromZone(zones[birth.OwnerId].Graveyard, gear.ObjectId), Hand = [..zones[birth.OwnerId].Hand, gear.ObjectId] };
            ResetCardOutsidePlay(zones, cards, gear.ObjectId, gear, birth.OwnerId);
            var returned = BuildReturnedToHandEvent("装备触发技能", item, gear.ObjectId, birth.OwnerId, true);
            events.Add(returned with { Payload = new Dictionary<string, object?>(returned.Payload) { ["sourceObjectId"] = visible } });
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
    }
}
