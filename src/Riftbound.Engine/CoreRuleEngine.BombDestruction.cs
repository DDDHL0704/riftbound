using Riftbound.Contracts;
using System.Text.Json.Serialization;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BombDestructionOrigin(
    ObjectBinding Bomb,
    string OwnerId,
    string ControllerId,
    string BattlefieldId,
    long CapturedTick,
    string BombTokenFactoryCardNo);

public sealed partial class CoreRuleEngine
{
    internal const string BombDestructionKind = "RAD_BOMB_DESTROYED";
    private const string BombDestructionCaptureKey = "bombDestructionObservers";
    private sealed record CapturedBombDestructionObserver(string SourceId, string Controller, FieldTriggerContext Context);

    private static bool IsBomb(CardObjectState card)
        => P6TokenFactoryCatalog.IsBombTokenFactory(card.TokenFactoryCardNo)
            && card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal);

    private static CapturedBombDestructionObserver? CaptureBombDestructionObserver(
        MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones,
        CardObjectState bomb)
    {
        if (!IsBomb(bomb)
            || !TryGetPreciseFieldLocation(zones, state.ObjectLocations, bomb.ObjectId, out var location)
            || location.Zone != "BATTLEFIELD"
            || string.IsNullOrWhiteSpace(location.BattlefieldObjectId)
            || string.IsNullOrWhiteSpace(bomb.ControllerId)
            || !state.Seats.ContainsKey(bomb.ControllerId))
            return null;

        var context = new FieldTriggerContext(
            bomb.CardNo ?? P6TokenFactoryCatalog.BombTokenCardNo,
            BombDestructionKind,
            bomb.ObjectGeneration,
            location.BattlefieldObjectId)
        {
            SourceTokenFactoryCardNo = P6TokenFactoryCatalog.BombTokenCardNo,
            SourceCeasedToExist = true,
            BombDestruction = new(
                new(bomb.ObjectId, bomb.ObjectGeneration),
                bomb.OwnerId ?? bomb.ControllerId,
                bomb.ControllerId,
                location.BattlefieldObjectId,
                state.Tick + 1,
                P6TokenFactoryCatalog.BombTokenCardNo)
        };
        return new(bomb.ObjectId, bomb.ControllerId, context);
    }

    private static bool ValidBombDestructionShape(FieldTriggerContext context, string effect, string? cardNo)
        => effect == FieldTriggerEffect
            && context.Kind == BombDestructionKind
            && context.SourceGeneration >= 0
            && context.SourceCeasedToExist
            && context.SourceTokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo
            && (cardNo is null || cardNo == context.CardNo)
            && context.BombDestruction is
            {
                Bomb: { Generation: >= 0 },
                OwnerId: { Length: > 0 },
                ControllerId: { Length: > 0 },
                BattlefieldId: { Length: > 0 },
                CapturedTick: > 0,
                BombTokenFactoryCardNo: P6TokenFactoryCatalog.BombTokenCardNo
            } origin
            && origin.Bomb.Generation == context.SourceGeneration
            && origin.BombTokenFactoryCardNo == context.SourceTokenFactoryCardNo
            && context.BattlefieldId == origin.BattlefieldId
            && context.SourceLocation is null
            && context.Origin is null
            && context.AffectedUnit is null
            && !context.AffectedUnitCeasedToExist
            && context.EquipmentDestruction is null
            && context.PaidUnitTrigger is null
            && context.Disarm is null
            && context.StunObserver is null
            && context.SelfAction is null;

    private static bool ValidBombDestructionSource(
        MatchState state,
        FieldTriggerContext context,
        string sourceId,
        string controller)
    {
        var origin = context.BombDestruction!;
        return origin.Bomb.ObjectId == sourceId
            && origin.Bomb.Generation == context.SourceGeneration
            && origin.ControllerId == controller
            && state.Seats.ContainsKey(controller)
            && state.Seats.ContainsKey(origin.OwnerId)
            && origin.CapturedTick <= state.Tick
            && BattlefieldLocalRules.Battlefield(state, origin.BattlefieldId) is not null
            && !state.CardObjects.ContainsKey(sourceId);
    }

    internal static bool ValidBombDestructionStack(StackItemState item)
        => item.FieldContext?.Kind != BombDestructionKind || item.FieldContext.BombDestruction is not null
            && item.OptionalCosts.Count == 0 && item.DamageAmount == 0
            && item.EffectRepeatCount == 1 && item.Destination.Length == 0;

    private static ResolutionResult QueueBombDestructionObservers(ResolutionResult result)
    {
        if (!result.Accepted || !result.Events.Any(e => e.Payload.ContainsKey(BombDestructionCaptureKey)))
            return result;

        var events = new List<GameEvent>();
        var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((ev, index) => (ev, index)))
        {
            events.Add(ev with
            {
                Payload = ev.Payload.Where(p => p.Key != BombDestructionCaptureKey)
                    .ToDictionary(p => p.Key, p => p.Value)
            });
            if (result.State.Status != MatchStatuses.InProgress
                || ev.Payload.GetValueOrDefault(BombDestructionCaptureKey) is not CapturedBombDestructionObserver[] captures)
                continue;
            foreach (var (capture, ordinal) in captures.Select((capture, ordinal) => (capture, ordinal)))
            {
                var trigger = new TriggerQueueItemState(
                    $"bomb-death-{result.State.Tick}-{index}-{ordinal}",
                    capture.Controller,
                    capture.SourceId,
                    FieldTriggerEffect,
                    ev.Kind)
                {
                    SourceCardNo = capture.Context.CardNo,
                    FieldContext = capture.Context with
                    {
                        BombDestruction = capture.Context.BombDestruction! with
                        {
                            CapturedTick = RuleChoices.Value?.CompletionTick ?? result.State.Tick
                        }
                    }
                };
                queue.Add(trigger);
                events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }

        var state = result.State with { TriggerQueue = queue };
        return result with
        {
            State = state,
            Events = events,
            Snapshots = ResolutionResult.BuildSnapshots(state),
            Prompts = BuildCorePrompts(state)
        };
    }

    private static StackResolutionResult ResolveBombDestruction(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured bomb destruction trigger.");
        if (!ValidBombDestructionStack(item))
            throw new InvalidOperationException("Invalid captured bomb destruction stack item.");

        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is null
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !FieldTriggerTargets(state, item).Contains(target, StringComparer.Ordinal))
        {
            return NoopStackResolutionResult(state) with
            {
                Events = [new("TRIGGER_RESOLVED", "炸弹被摧毁后的技能结算失败", new Dictionary<string, object?>
                {
                    ["sourceObjectId"] = item.SourceObjectId,
                    ["effectKind"] = context.Kind
                })]
            };
        }

        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>
        {
            new("TRIGGER_RESOLVED", "炸弹被摧毁后的技能结算", new Dictionary<string, object?>
            {
                ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = target,
                ["effectKind"] = context.Kind
            })
        };
        var damageTriggered = new HashSet<string>(StringComparer.Ordinal);
        var damage = ApplyDamageToCardObject(
            state with { PlayerZones = zones, CardObjects = cards },
            cards,
            item.ControllerId,
            target,
            2,
            damageTriggered,
            state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId),
            PreventSpellAndSkillDamageThisTurnEffectId);
        events.Add(new("DAMAGE_APPLIED", "炸弹对敌方单位造成2点伤害", BuildDamagePayload(item.SourceObjectId, target, damage)));

        var otherBombs = cards.Values
            .Where(IsBomb)
            .Where(card => !string.Equals(card.ObjectId, item.SourceObjectId, StringComparison.Ordinal))
            .Where(card => EffectiveFieldControllerId(zones, card.ObjectId, card) == item.ControllerId)
            .Where(card => state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
                && location.Zone == "BATTLEFIELD"
                && location.BattlefieldObjectId == context.BattlefieldId)
            .Select(card => card.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
        var chainedTriggers = state.TriggerQueue;
        if (otherBombs.Count > 0)
        {
            var destruction = ResolveFieldDestructions(
                state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations },
                zones,
                cards,
                item,
                damageTriggered,
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(),
                pools,
                battlefieldId: context.BattlefieldId,
                objectLocations: locations,
                explicitDestroyObjectIds: otherBombs,
                explicitDestroyReason: "BOMB_CHAIN");
            events.AddRange(destruction.Events);
            pools = destruction.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            chainedTriggers = chainedTriggers.Concat(destruction.TriggerQueue).ToArray();
        }

        var cleanup = RunStateBasedCleanupLoop(
            state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = pools,
                TriggerQueue = chainedTriggers },
            zones,
            cards,
            item,
            pools,
            damageTriggeredDestroyTargetObjectIds: damageTriggered,
            objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return NoopStackResolutionResult(state) with
        {
            PlayerZones = zones,
            CardObjects = cards,
            RunePools = cleanup.RunePools,
            TriggerQueue = cleanup.TriggerQueue,
            DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            Events = events,
            ObjectLocations = ReconcileObjectLocations(locations, zones)
        };
    }
}
