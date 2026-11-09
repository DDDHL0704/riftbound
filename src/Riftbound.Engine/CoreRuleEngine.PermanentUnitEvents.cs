using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string MoveFriendlyToArrivalBattlefieldEffect = "MOVE_ANOTHER_FRIENDLY_TO_ARRIVAL_BATTLEFIELD";

    // CN359.3.f.3: "the same battlefield" is the event's arrival field,
    // preserved even if the source moves again or leaves before resolution.
    private static string? UnitEventArrivalBattlefieldDestination(MatchState state, StackItemState item)
        => SelectedUnitEventSpec(item.UnitEvent!)?.RequiresMoveToBattlefield == true
            && item.UnitEvent!.Here is { Zone: "BATTLEFIELD", BattlefieldObjectId: { } field }
            && BattlefieldLocalRules.Battlefield(state, field) is not null ? "BATTLEFIELD:" + field : null;

    // CN359.3.f.2: an instruction reads the source's current location in the
    // captured generation. Leaving and re-entering cannot supply a new "here".
    private static string? UnitEventHereDestination(MatchState state, StackItemState item)
        => CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration)
            is { Zone: "BATTLEFIELD", BattlefieldObjectId: { } field } ? "BATTLEFIELD:" + field : null;

    private static bool UnitEventTargetIsHere(MatchState state, StackItemState item, string target)
        => CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration) is { } here
            && state.ObjectLocations.TryGetValue(target, out var at) && at.Zone == here.Zone
            && (here.Zone == "BASE" ? at.PlayerId == here.PlayerId : at.BattlefieldObjectId == here.BattlefieldObjectId);

    private static bool ConfirmUnitEventMovementDestination(MatchState state, ref StackItemState item, List<GameEvent> events)
    {
        if (item.UnitEvent is not { } context || !IsDeclaredUnitEventMovementEffect(context)) return true;
        var target = item.TargetObjectIds.Single();
        var options = UnitEventMovementDestinations(state, item, target).ToArray();
        if (options.Length == 0) return false;
        var destination = RuleChoices.Value!.Choose(item.ControllerId, "选择敌方单位的移动目的地，随后双方可以响应。",
            options,
            kind: "MOVEMENT_DESTINATION", preview: state);
        item = item with { Destination = destination };
        events.Add(new("TRIGGER_MOVEMENT_DECLARED", "已声明触发技能的移动目的地", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
            ["targetObjectIds"] = item.TargetObjectIds, ["destination"] = destination,
            ["declaredUnitEventHash"] = MatchStateHasher.HashValue(item.StackItemId),
            ["declaredMovementHash"] = DeclaredUnitEventMovementHash(item) }));
        return true;
    }

    internal static bool ValidUnitEventMovementDeclaration(MatchState state, StackItemState item)
        => item.UnitEvent is not { } context || !IsDeclaredUnitEventMovementEffect(context)
            || (item.TargetGenerations is null ? item.Destination.Length == 0
                : item.Destination.Length == 0 ? state.PendingPayment?.ResolvingStackItemId == item.StackItemId
                : item.TargetObjectIds.Count == 1
                    && (SelectedUnitEventSpec(context)?.Effect != MoveWeakerEnemyHereToAnotherBattlefieldEffect
                        || item.Destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal))
                    && (item.Destination.StartsWith("BASE:", StringComparison.Ordinal)
                    && state.Seats.ContainsKey(item.Destination[5..])
                    || item.Destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
                        && BattlefieldLocalRules.Battlefield(state, item.Destination[12..]) is not null));

    private static bool TryUnitEventTokenLocation(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec, out string? field)
    {
        field = null;
        if (!spec.CreatesUnitTokenHere) return true;
        var here = CombatMightSourceLocation(state, item.SourceObjectId, item.UnitEvent!.SourceGeneration);
        if (here is null || here.Zone is not ("BASE" or "BATTLEFIELD")
            || here.Zone == "BASE" && here.PlayerId != item.ControllerId
            || here.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, here.BattlefieldObjectId) is null)
            return false;
        field = here.BattlefieldObjectId;
        return true;
    }

    private static StackResolutionResult ResolvePermanentUnitEventInstruction(MatchState state,
        StackItemState item, UnitEventAbilitySpec spec)
    {
        if ((spec.LeadingManaCost > 0 || spec.LeadingPowerCost > 0) && item.TriggerCost is null)
            return NoopStackResolutionResult(state);
        if (spec.Effect == "CREATE_UNIT_TOKEN")
        {
            if (!TryUnitEventTokenLocation(state, item, spec, out var field)) return NoopStackResolutionResult(state);
            if (BeginTokenReplacement(state, item) is { } replacement) return replacement;
            return CreateUnitTokenBatch(state, item, spec.CreatedTokenFactoryCardNo!, spec.Amount + AdditionalUnitTokens(item), false,
                field, instructionAllowsLocationChoice: spec.CreatesUnitTokenHere ? false : null);
        }
        if (spec.Effect == FriendlyUnitTokensMoveHere)
            return ResolveFriendlyUnitTokenMovement(state, item);
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || !StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !UnitEventTargets(state, item).Contains(target)) return NoopStackResolutionResult(state);

        var cards = state.CardObjects.ToDictionary();
        var events = new List<GameEvent>();
        var behavior = new CardBehaviorDefinition(item.CardNo, "永久物触发技能", 0, item.EffectKind, 0, 1);
        if (spec.Effect is "MOVE_ENEMY" or MoveWeakerEnemyHereToAnotherBattlefieldEffect)
        {
            if (!ValidUnitEventMovementDeclaration(state, item)) throw new InvalidOperationException("Invalid movement declaration.");
            if (spec.Effect == MoveWeakerEnemyHereToAnotherBattlefieldEffect
                && !UnitEventMovementDestinations(state, item, target).Any(choice => choice.Id == item.Destination))
                return NoopStackResolutionResult(state);
            var zones = NormalizeZonesForSeats(state);
            var locations = state.ObjectLocations.ToDictionary();
            var pools = state.RunePools.ToDictionary();
            MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [target], item.Destination, events);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools, Events = events };
        }
        if (spec.Effect == "BOON_FRIENDLY")
        {
            cards[target] = ApplyBoon(cards[target], behavior, item, target, out var granted, state.PlayerZones, cards);
            events.AddRange(granted);
        }
        else if (spec.Effect == "READY_HERE")
        {
            cards[target] = ApplyReadyState(cards[target], behavior with { ReadiesTarget = true }, item, target,
                out var readied, state.PlayerZones, cards);
            if (readied is not null) events.Add(readied);
        }
        else if ((spec.Effect == "MOVE_ENEMY_HERE" ? UnitEventHereDestination(state, item)
            : spec.Effect is MoveFriendlyToArrivalBattlefieldEffect or MoveEnemyToArrivalBattlefieldEffect ? UnitEventArrivalBattlefieldDestination(state, item)
            : spec.Effect == MoveEnemyToCapturedBattlefieldEffect ? UnitEventCapturedBattlefieldDestination(state, item)
            : spec.Effect == DeploymentPlayMoveEffect ? DeploymentPlayDestination(state, item.UnitEvent!) : null) is { } destination)
        {
            var zones = NormalizeZonesForSeats(state);
            var locations = state.ObjectLocations.ToDictionary();
            var pools = state.RunePools.ToDictionary();
            MoveFieldUnitsToChosenLocation(state, zones, cards, locations, pools, item, [target], destination, events);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = pools, Events = events };
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
