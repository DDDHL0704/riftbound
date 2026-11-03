using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveBorrowedMovement(MatchState state, StackItemState item, BorrowedExhaustSpec spec)
    {
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "获得的移动技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId }) };
        var id = item.TargetObjectIds[0];
        if (!StackObjectRules.ReferencesCurrentObject(state, item, id)
            || !IsBorrowedExhaustTarget(state, item.ControllerId, id, spec, item.SourceObjectId)
            || item.BorrowedExhaust!.DuelBattlefield is { } field
                && (state.SpellDuelState.BattlefieldObjectId != field.ObjectId
                    || state.CardObjects[field.ObjectId].ObjectGeneration != field.Generation)
            || spec.ResolveOnlyOnControllerTurn && state.TurnPlayerId != item.ControllerId)
            return NoopStackResolutionResult(state) with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        var pools = state.RunePools.ToDictionary();
        var destination = new ObjectLocationState(item.ControllerId, "BASE");
        if (spec.Kind == "MOVE_BETWEEN_BASE_AND_BATTLEFIELD"
            && TryGetPreciseFieldLocation(zones, locations, id, out var origin) && origin.Zone == "BASE")
        {
            var choices = ChosenMovementDestinations(state, item.ControllerId)
                .Where(choice => choice.Id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)).ToArray();
            if (choices.Length == 0) return NoopStackResolutionResult(state) with { Events = events };
            var selected = RuleChoices.Value!.Choose(item.ControllerId, "选择此单位移动到的战场。",
                choices.Select(choice => new RuleChoiceOption(choice.Id, choice.Label)).ToArray(),
                kind: "MOVEMENT_DESTINATION", preview: state);
            destination = new(item.ControllerId, "BATTLEFIELD", selected[12..]);
        }
        TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, id, destination,
            item.ControllerId, item, events, frame => StackObjectRules.ReferencesCurrentObject(frame, item, id)
                && IsBorrowedExhaustTarget(frame, item.ControllerId, id, spec, item.SourceObjectId));
        var context = item.BorrowedExhaust!;
        var sourceEmpowered = cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.Borrower.Object.Generation && IsObjectOnField(zones, source.ObjectId)
                ? source.IsEmpowered : context.LastKnownSourceEmpowered;
        // The movement and ready instructions are independent. A prohibition
        // on moving does not prevent a legal subsequent ready action.
        if (spec.ReadyTargetIfSourceEmpowered && sourceEmpowered == true
            && cards.TryGetValue(id, out var target) && target.ObjectGeneration == item.TargetGenerations![id]
            && IsObjectOnField(zones, id)
            && !AttachedRuleTextRules.Instances(state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations }, target)
                .Any(instance => CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)))
        {
            cards[id] = ApplyReadyState(target, new(context.Donor.CardNo, "获得的活跃技能", 0, item.EffectKind, 0, 1),
                item, id, out var readied, zones, cards);
            if (readied is not null) events.Add(readied);
        }
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, RunePools = pools, Events = events };
    }

    private static StackResolutionResult ResolveBorrowedBattlefieldEquipmentTokens(MatchState state,
        StackItemState item, BorrowedExhaustSpec spec)
    {
        var battlefield = item.TargetObjectIds[0];
        if (!StackObjectRules.ReferencesCurrentObject(state, item, battlefield)
            || BattlefieldLocalRules.Battlefield(state, battlefield) is null) return NoopStackResolutionResult(state);
        var factory = P6TokenFactoryCatalog.GetAll().First(f => f.TokenFamilyName == spec.TokenName
            && f.Tags.Contains(CardObjectTags.EquipmentCard));
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        var locations = CreateBattlefieldEquipmentTokenBatch(zones, cards, item.ControllerId, item.SourceObjectId,
            item.BorrowedExhaust!.Donor.CardNo, "获得的指示物技能", factory.TokenFamilyName, factory.Tags,
            spec.TokenCount, false, battlefield, events, state.ObjectLocations);
        return NoopStackResolutionResult(state) with {
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }
}
