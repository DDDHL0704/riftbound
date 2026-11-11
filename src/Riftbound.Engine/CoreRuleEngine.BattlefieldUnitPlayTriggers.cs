using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattlefieldUnitPlayOrigin(
    [property: JsonRequired] ObjectBinding Unit,
    [property: JsonRequired] string UnitCardNo,
    [property: JsonRequired] string PlayerId,
    [property: JsonRequired] long CapturedTick,
    [property: JsonRequired] int TurnNumber,
    string? UnitTokenFactoryCardNo = null);

public sealed partial class CoreRuleEngine
{
    internal const string BattlefieldEntryBoonKind = TriggerKinds.BattlefieldPlayUnitPayBoon;
    internal const string BattlefieldFirstEntryMoveKind = TriggerKinds.BattlefieldFirstUnitPlayedMoveOtherToBase;
    internal const string BattlefieldFirstEntryUsePrefix = "BATTLEFIELD_FIRST_NON_TOKEN_UNIT_PLAYED:";
    internal const string BattlefieldUnitPlayCaptureHashKey = "battlefieldUnitPlayCaptureHash";
    internal const string BattlefieldUnitPlayTurnKey = "battlefieldUnitPlayTurnNumber";
    internal const string BattlefieldFirstEntryUseKey = "battlefieldFirstEntryUse";
    internal const string BattlefieldUnitPlayTargetsHashKey = "battlefieldUnitPlayTargetsHash";
    private const string BattlefieldUnitPlayObservationKey = "battlefieldUnitPlayObservations";
    private sealed record BattlefieldUnitPlayObservation(string Source, string CardNo, long SourceGeneration,
        string Player, ObjectBinding Unit, string UnitCardNo, string? UnitTokenFactoryCardNo, string Kind);

    internal static bool IsBattlefieldUnitPlayKind(string? kind)
        => kind is BattlefieldEntryBoonKind or BattlefieldFirstEntryMoveKind;

    internal static TriggerSpec? BattlefieldUnitPlayDefinition(string? cardNo, string kind)
    {
        var specs = BattlefieldTriggerSpecRules.TriggersForCard(cardNo).Where(spec => spec.Kind == kind
            && (kind == BattlefieldEntryBoonKind
                ? BattlefieldTriggerSpecRules.IsBattlefieldPlayUnitPayBoonTrigger(spec)
                    && spec.ManaCost == 1 && spec.BoonCount == 1
                : kind == BattlefieldFirstEntryMoveKind
                    && BattlefieldTriggerSpecRules.IsBattlefieldFirstUnitPlayedMoveOtherToBaseTrigger(spec)
                    && spec.MoveCount == 1 && spec.OncePerTurn == true && spec.ExcludesTokens == true)).ToArray();
        return specs.Length == 1 ? specs[0] : null;
    }

    internal static string BattlefieldFirstEntryUse(ObjectBinding field)
        => BattlefieldFirstEntryUsePrefix + MatchStateHasher.HashValue(field);

    // CN190.6.c: the explicitly named playing player controls these skills,
    // irrespective of who controls the actual battlefield at that instant.
    private static GameEvent CaptureBattlefieldUnitPlay(GameEvent ev,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState entered, string player)
    {
        if (entered.IsFaceDown || ev.Kind is not ("UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")
            || !TryBattlefieldPermanentPlay(ev, out var actor, out var field) || actor != player
            || !TryGetBattlefieldCardObject(zones, cards, field, out var id, out var source)) return ev;
        var token = IsTokenObject(entered) ? entered.TokenFactoryCardNo ?? entered.CardNo : null;
        var observations = new[] { BattlefieldEntryBoonKind, BattlefieldFirstEntryMoveKind }
            .Where(kind => BattlefieldUnitPlayDefinition(source.CardNo, kind) is not null
                && (kind != BattlefieldFirstEntryMoveKind || token is null && ev.Kind != "UNIT_TOKEN_CREATED"))
            .Select(kind => new BattlefieldUnitPlayObservation(id, source.CardNo!, source.ObjectGeneration,
                player, new(entered.ObjectId, entered.ObjectGeneration), entered.CardNo!, token, kind)).ToArray();
        return observations.Length == 0 ? ev : ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            [BattlefieldUnitPlayObservationKey] = observations } };
    }

    private static ResolutionResult QueueBattlefieldUnitPlays(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList();
        var queue = result.State.TriggerQueue.ToList();
        var effects = result.State.UntilEndOfTurnEffects.ToList();
        var used = before.UntilEndOfTurnEffects.Where(value =>
            value.StartsWith(BattlefieldFirstEntryUsePrefix, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        var turn = before.TurnNumber;
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index)))
        {
            if (ev.Kind == "TURN_PLAYER_ADVANCED" && ev.Payload.GetValueOrDefault("turnNumber") is int advanced)
            {
                turn = advanced;
                used.Clear();
            }
            if (ev.Payload.GetValueOrDefault(BattlefieldUnitPlayObservationKey) is not BattlefieldUnitPlayObservation[] observations)
                continue;
            events[index] = ev with { Payload = ev.Payload.Where(pair => pair.Key != BattlefieldUnitPlayObservationKey)
                .ToDictionary(pair => pair.Key, pair => pair.Value) };
            foreach (var observation in observations)
            {
                var marker = BattlefieldFirstEntryUse(new(observation.Source, observation.SourceGeneration));
                if (observation.Kind == BattlefieldFirstEntryMoveKind)
                {
                    // This is "the first event", not "once if accepted": a
                    // decline or absence of another unit still uses that event.
                    if (!used.Add(marker)) continue;
                    if (turn == result.State.TurnNumber && !effects.Contains(marker, StringComparer.Ordinal)) effects.Add(marker);
                }
                var unit = observation.Unit;
                if (before.CardObjects.TryGetValue(unit.ObjectId, out var prior)
                    && !IsObjectOnField(before.PlayerZones, unit.ObjectId))
                    unit = unit with { Generation = Math.Max(unit.Generation, checked(prior.ObjectGeneration + 1)) };
                var context = new FieldTriggerContext(observation.CardNo, observation.Kind,
                    observation.SourceGeneration, observation.Source) {
                    BattlefieldUnitPlay = new(unit, observation.UnitCardNo, observation.Player,
                        result.State.Tick, turn, observation.UnitTokenFactoryCardNo) };
                var trigger = new TriggerQueueItemState($"battlefield-unit-play-{result.State.Tick}-{index}-{observation.Kind}",
                    observation.Player, observation.Source, FieldTriggerEffect, ev.Kind) {
                    FieldContext = context, SourceCardNo = observation.CardNo, EnqueuedAtTick = result.State.Tick };
                queue.Add(trigger);
                events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        return result with { State = result.State with { TriggerQueue = queue, UntilEndOfTurnEffects = effects }, Events = events };
    }

    internal static string BattlefieldUnitPlayCaptureHash(FieldTriggerContext context, string source, string player)
        => MatchStateHasher.HashValue(new { source, player, context.CardNo, context.Kind,
            context.SourceGeneration, context.BattlefieldId, context.BattlefieldUnitPlay });

    internal static string BattlefieldUnitPlayTargetHash(StackItemState item)
        => MatchStateHasher.HashValue(new {
            id = item.StackItemId.StartsWith("ordered-", StringComparison.Ordinal) ? item.StackItemId[8..] : item.StackItemId,
            capture = BattlefieldUnitPlayCaptureHash(item.FieldContext!, item.SourceObjectId, item.ControllerId),
            item.TargetObjectIds, item.TargetGenerations });

    private static void AddBattlefieldUnitPlayReceipt(TriggerQueueItemState trigger, Dictionary<string, object?> payload)
    {
        if (trigger.FieldContext is not { BattlefieldUnitPlay: { } origin } context) return;
        payload[BattlefieldUnitPlayCaptureHashKey] = BattlefieldUnitPlayCaptureHash(context, trigger.SourceObjectId, trigger.ControllerId);
        payload[BattlefieldUnitPlayTurnKey] = origin.TurnNumber;
        if (context.Kind == BattlefieldFirstEntryMoveKind)
            payload[BattlefieldFirstEntryUseKey] = BattlefieldFirstEntryUse(new(trigger.SourceObjectId, context.SourceGeneration));
    }

    private static bool ValidBattlefieldUnitPlayShape(FieldTriggerContext context, string effect, string? cardNo)
        => IsBattlefieldUnitPlayKind(context.Kind) && effect == FieldTriggerEffect
            && (cardNo is null || cardNo == context.CardNo) && context.SourceGeneration >= 0
            && !string.IsNullOrWhiteSpace(context.BattlefieldId) && context.ReturnFocusPlayerId is null
            && context.BattlefieldUnitPlay is { Unit.Generation: >= 0, CapturedTick: > 0, TurnNumber: >= 0 } origin
            && !string.IsNullOrWhiteSpace(origin.Unit.ObjectId) && !string.IsNullOrWhiteSpace(origin.UnitCardNo)
            && !string.IsNullOrWhiteSpace(origin.PlayerId)
            && BattlefieldUnitPlayDefinition(context.CardNo, context.Kind) is not null
            && (context.Kind != BattlefieldFirstEntryMoveKind || origin.UnitTokenFactoryCardNo is null)
            && context.BombDestruction is null && context.EquipmentDestruction is null && context.PaidUnitTrigger is null
            && context.Disarm is null && context.StunObserver is null && context.SelfAction is null
            && context.AttackMightDamage is null && context.RecycledBoon is null && context.EmpoweredTransition is null
            && context.SourceLocation is null && context.Origin is null && context.SourceTokenFactoryCardNo is null
            && !context.SourceCeasedToExist && context.AffectedUnit is null && !context.AffectedUnitCeasedToExist
            && context.AttackConditionFrame is null;

    private static bool ValidBattlefieldUnitPlayState(MatchState state, FieldTriggerContext context,
        string source, string player, string effect, string? cardNo)
        => ValidBattlefieldUnitPlayShape(context, effect, cardNo) && context.BattlefieldId == source
            && context.BattlefieldUnitPlay is { } origin && origin.PlayerId == player && state.Seats.ContainsKey(player)
            && origin.CapturedTick <= state.Tick && origin.TurnNumber <= state.TurnNumber
            && state.CardObjects.TryGetValue(source, out var field) && field.ObjectGeneration >= context.SourceGeneration;

    internal static bool ValidBattlefieldUnitPlayStack(MatchState state, StackItemState item)
    {
        if (item.FieldContext is not { } context
            || !IsBattlefieldUnitPlayKind(context.Kind) && context.BattlefieldUnitPlay is null) return true;
        return ValidBattlefieldUnitPlayState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo)
            && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.Destination.Length == 0 && item.OptionalCosts.Count == 0
            && (context.Kind == BattlefieldEntryBoonKind ? item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                : item.TriggerCost is null && (item.TargetGenerations is null ? item.TargetObjectIds.Count == 0
                    : item.TargetObjectIds.Count == 1 && item.TargetGenerations.Count == 1
                        && item.TargetGenerations.ContainsKey(item.TargetObjectIds[0])
                        && item.TargetGenerations.Values.All(value => value >= 0)));
    }

    private static string[] BattlefieldUnitPlayTargets(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (context.Kind != BattlefieldFirstEntryMoveKind || context.BattlefieldUnitPlay is not { } origin
            || BattlefieldLocalRules.Battlefield(state, context.BattlefieldId) is not { } field
            || field.ObjectGeneration != context.SourceGeneration) return [];
        return state.CardObjects.Values.Where(unit => unit.ObjectId != origin.Unit.ObjectId
            && IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(state.PlayerZones, unit.ObjectId)
            && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) == item.ControllerId
            && state.ObjectLocations.TryGetValue(unit.ObjectId, out var at) && at.Zone == "BATTLEFIELD"
            && at.BattlefieldObjectId == context.BattlefieldId
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, unit.ObjectId))
            .Select(unit => unit.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static ResolutionResult PrepareBattlefieldUnitPlayTarget(ResolutionResult result, StackItemState item)
    {
        var legal = BattlefieldUnitPlayTargets(result.State, item);
        if (legal.Length == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var choice = new PendingCardChoiceState("TRIGGER-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            0, 1, legal, legal, "选择此处另一名友方单位移回其基地，或放弃；确认后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        var state = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static StackResolutionResult ResolveBattlefieldUnitPlay(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidBattlefieldUnitPlayStack(state, item)
            || context.Kind == BattlefieldEntryBoonKind && (item.TriggerCost is null || !ValidTriggerCostReceipt(state, item)))
            throw new InvalidOperationException("Invalid confirmed battlefield unit-play skill.");
        var origin = context.BattlefieldUnitPlay!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (context.Kind == BattlefieldEntryBoonKind)
        {
            if (cards.TryGetValue(origin.Unit.ObjectId, out var unit) && unit.ObjectGeneration == origin.Unit.Generation
                && IsFaceUpNonStandbyUnit(unit) && IsObjectOnField(zones, unit.ObjectId))
            {
                using var instruction = new MightInstructionScope(state, zones, cards, events, locations);
                GrantLegendBoon(cards, unit.ObjectId, item.ControllerId, item.SourceObjectId, context.Kind, events, zones);
            }
        }
        else if (item.TargetObjectIds.SingleOrDefault() is { } target && cards.TryGetValue(target, out var unit)
            && item.TargetGenerations?.GetValueOrDefault(target, -1) == unit.ObjectGeneration
            && BattlefieldUnitPlayTargets(state, item).Contains(target))
        {
            var controller = EffectiveFieldControllerId(zones, target, unit);
            TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, target, new(controller, "BASE"),
                item.ControllerId, item, events, current => BattlefieldUnitPlayTargets(current, item).Contains(target));
        }
        events.Add(new("BATTLEFIELD_TRIGGER_RESOLVED", "战场打出单位技能独立结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["battlefieldObjectId"] = item.SourceObjectId,
            ["battlefieldCardNo"] = context.CardNo, ["trigger"] = context.Kind,
            ["sourceObjectId"] = item.SourceObjectId }));
        return new(zones, cards, state.PlayerScores, state.PlayerExperience, pools, state.UntilEndOfTurnEffects,
            null, events, [], null, [], null, [], state.RngCursor, ObjectLocations: locations);
    }
}
