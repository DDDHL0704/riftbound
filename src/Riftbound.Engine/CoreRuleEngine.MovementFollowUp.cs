using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record MovementFollowUpContext(ObjectBinding Unit, ObjectBinding Equipment, int ExecutionIndex, bool? AttachmentChosen = null);

public sealed partial class CoreRuleEngine
{
    internal const string MovementAttachmentWindow = "MOVEMENT_OPTIONAL_ATTACHMENT";

    internal static bool IsMovementEquipmentCandidate(MatchState state, CardBehaviorDefinition behavior, string id, int index)
        => behavior.MovesFriendlyEquipmentToChosenBattlefield && index == 0
            ? IsFriendlyEquipmentMovementCandidate(state, state.PendingEffectPlay?.PlayerId ?? state.TurnPlayerId ?? "", id)
            : !behavior.OptionalEquipmentAfterMovement || index == 0 || string.IsNullOrEmpty(behavior.OptionalEquipmentRequiredTag)
                || state.CardObjects.TryGetValue(id, out var gear) && gear.Tags.Contains(behavior.OptionalEquipmentRequiredTag, StringComparer.Ordinal);

    internal static bool IsFriendlyEquipmentMovementCandidate(MatchState state, string playerId, string id)
        => IsFriendlyEquipmentObject(state, playerId, id)
            && state.CardObjects.TryGetValue(id, out var equipment)
                && !equipment.IsFaceDown
                && equipment.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                && equipment.Tags.Contains(CardEquipmentKeywordNames.Deploy, StringComparer.Ordinal)
                && string.IsNullOrWhiteSpace(equipment.AttachedToObjectId)
                && IsObjectOnField(state.PlayerZones, id);

    internal static bool IsMovementEquipmentRelationAllowed(MatchState state, string player,
        CardBehaviorDefinition behavior, IReadOnlyList<string> targets, int index)
    {
        if (!behavior.OptionalEquipmentAfterMovement || index == 0) return true;
        return targets.Count == 2 && index == 1
            && state.CardObjects.TryGetValue(targets[0], out var unit)
            && state.CardObjects.TryGetValue(targets[1], out var gear)
            && IsFaceUpNonStandbyUnit(unit) && !gear.IsFaceDown && gear.Tags.Contains(CardObjectTags.EquipmentCard)
            && IsMovementEquipmentCandidate(state, behavior, gear.ObjectId, index)
            && IsObjectOnField(state.PlayerZones, unit.ObjectId) && IsObjectOnField(state.PlayerZones, gear.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) == player
            && EffectiveFieldControllerId(state.PlayerZones, gear.ObjectId, gear) == player;
    }

    private static bool MovementTargetIsCurrent(MatchState state, StackItemState item, string id)
        => item.TargetGenerations is { } generations && generations.TryGetValue(id, out var generation)
            && state.CardObjects.TryGetValue(id, out var card) && generation == card.ObjectGeneration;

    private static StackItemState MovementExecution(StackItemState parent)
        => parent.RepeatExecutions is { } executions && parent.CompletedRepeatExecutions < executions.Count
            ? parent with { EffectKind = executions[parent.CompletedRepeatExecutions].EffectKind,
                TargetObjectIds = executions[parent.CompletedRepeatExecutions].TargetObjectIds,
                TargetGenerations = executions[parent.CompletedRepeatExecutions].TargetGenerations,
                Destination = executions[parent.CompletedRepeatExecutions].Destination }
            : parent;

    private static PendingCardChoiceState MovementAttachmentChoice(StackItemState parent)
        => new("MOVE-ATTACH:" + parent.StackItemId, MovementAttachmentWindow, parent.ControllerId, 0, 1,
            [parent.MovementFollowUp!.Equipment.ObjectId], [parent.MovementFollowUp.Unit.ObjectId],
            "移动已完成。可将已确认的武装贴附给该单位；不选则跳过。", parent.SourceObjectId, parent.EffectKind) {
                ResolvingStackItemId = parent.StackItemId };

    internal static bool ValidMovementFollowUp(MatchState state, StackItemState parent)
    {
        if (parent.MovementFollowUp is not { } context) return true;
        if (context.AttachmentChosen is not null || context.ExecutionIndex != parent.CompletedRepeatExecutions) return false;
        var item = MovementExecution(parent);
        if (!CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior) || !behavior.OptionalEquipmentAfterMovement
            || item.CardNo != behavior.CardNo
            || item.TargetObjectIds.Count != 2 || item.TargetGenerations is not { } generations
            || context.Unit.ObjectId != item.TargetObjectIds[0] || context.Equipment.ObjectId != item.TargetObjectIds[1]
            || context.Unit.Generation < 0 || context.Equipment.Generation < 0
            || generations.GetValueOrDefault(context.Unit.ObjectId, -1) != context.Unit.Generation
            || generations.GetValueOrDefault(context.Equipment.ObjectId, -1) != context.Equipment.Generation
            || !state.CardObjects.TryGetValue(context.Unit.ObjectId, out var unit) || unit.ObjectGeneration != context.Unit.Generation
            || !state.CardObjects.TryGetValue(context.Equipment.ObjectId, out var gear) || gear.ObjectGeneration != context.Equipment.Generation
            || !IsMovementEquipmentRelationAllowed(state, parent.ControllerId, behavior, item.TargetObjectIds, 1)) return false;
        var location = state.ObjectLocations.GetValueOrDefault(context.Unit.ObjectId);
        var actualDestination = location?.Zone == "BASE" ? "BASE:" + location.PlayerId
            : location?.Zone == "BATTLEFIELD" ? "BATTLEFIELD:" + location.BattlefieldObjectId : "";
        if (actualDestination != item.Destination) return false;
        return state.PendingCardChoice is { } choice && state.PriorityPlayerId is null
            && choice.ChoiceWindow == MovementAttachmentWindow && choice.ChoiceId == "MOVE-ATTACH:" + parent.StackItemId
            && choice.ResolvingStackItemId == parent.StackItemId && choice.PlayerId == parent.ControllerId
            && choice.SourceObjectId == parent.SourceObjectId && choice.EffectKind == parent.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual([context.Equipment.ObjectId])
            && choice.ContextObjectIds.SequenceEqual([context.Unit.ObjectId]);
    }

    private static ResolutionResult ResolveMovementAttachmentChoice(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        var parent = state.StackItems.LastOrDefault();
        if (parent is null || !ValidMovementFollowUp(state, parent) || parent.MovementFollowUp is not { } context)
            return RejectWithCorePrompts(state, "移动后贴附上下文已失效。", ErrorCodes.InvalidTarget);
        var next = state with { PendingCardChoice = null,
            StackItems = state.StackItems.Take(state.StackItems.Count - 1).Append(parent with {
                MovementFollowUp = context with { AttachmentChosen = selected.Count != 0 } }).ToArray() };
        return ResolvePassPriority(next, new("movement-attachment-continuation", parent.ControllerId,
            CommandTypes.PassPriority), forceResolve: true);
    }

    private static StackResolutionResult ResolveMovementFollowUp(MatchState state, StackItemState item, CardBehaviorDefinition behavior, bool deferCompletion)
    {
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones).ToDictionary(p => p.Key, p => p.Value);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value);
        var events = new List<GameEvent>();
        if (item.MovementFollowUp is { AttachmentChosen: not null } followUp)
        {
            IReadOnlyDictionary<string, ObjectLocationState> nextLocations = locations;
            if (followUp.AttachmentChosen == true)
                TryAttachEquipment(state, zones, cards, locations, item.ControllerId, item.SourceObjectId,
                    followUp.Equipment.ObjectId, followUp.Unit.ObjectId, item.EffectKind, events, out nextLocations);
            GrantConquestReturn(state, cards, item, followUp.Unit.ObjectId, events);
            return FinishMovementSpell(state, item, behavior, zones, cards, nextLocations, pools, events, deferCompletion);
        }
        var unitId = item.TargetObjectIds.FirstOrDefault();
        if (unitId is null || !MovementTargetIsCurrent(state, item, unitId)
            || !IsLegalChosenCardTarget(state, item.ControllerId, unitId, 0, item.TargetObjectIds, behavior.TargetScope, behavior))
            return FinishMovementSpell(state, item, behavior, zones, cards, locations, pools, events, deferCompletion);
        var moved = MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [unitId], item.Destination, events);
        if (moved.Length == 0) return FinishMovementSpell(state, item, behavior, zones, cards, locations, pools, events, deferCompletion);
        var current = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools };
        if (item.TargetObjectIds.Count == 2 && MovementTargetIsCurrent(current, item, item.TargetObjectIds[1])
            && IsLegalChosenCardTarget(current, item.ControllerId, item.TargetObjectIds[1], 1, item.TargetObjectIds, behavior.TargetScope, behavior))
        {
            var suspended = item with { MovementFollowUp = new(new(unitId, cards[unitId].ObjectGeneration),
                new(item.TargetObjectIds[1], cards[item.TargetObjectIds[1]].ObjectGeneration), item.CompletedRepeatExecutions) };
            return NoopStackResolutionResult(current) with { Events = events, ObjectLocations = locations,
                PendingCardChoice = MovementAttachmentChoice(suspended),
                StackItems = state.StackItems.Where(i => i.StackItemId != item.StackItemId).Append(suspended).ToArray() };
        }
        GrantConquestReturn(state, cards, item, unitId, events);
        return FinishMovementSpell(state, item, behavior, zones, cards, locations, pools, events, deferCompletion);
    }
    private static StackResolutionResult FinishMovementSpell(MatchState state, StackItemState item,
        CardBehaviorDefinition behavior, Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, ObjectLocationState> locations, Dictionary<string, RunePool> pools, List<GameEvent> events, bool deferCompletion)
    {
        if (!deferCompletion) CompleteSpellSource(zones, cards, item, behavior, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools, Events = events };
    }

}
