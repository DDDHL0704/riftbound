using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DockCageExileContext(ObjectBinding ExiledCard, string CardNo, long FaceRevision,
    string OwnerId, string? TokenFactoryCardNo = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeploymentHoldOrigin(ObjectBinding Source, ObjectBinding Battlefield,
    string ControllerId, string HoldingPlayerId, long CapturedTick, long SourceFaceRevision,
    string? TokenFactoryCardNo = null, DockCageExileContext? ExiledCard = null);

public sealed partial class CoreRuleEngine
{
    internal const string DeploymentHeldKind = "DEPLOYMENT_OPPONENT_HELD_DESTROY";

    // CN 830.1.b / 830.2: one mandatory ability for each deployed equipment,
    // including unit equipment. Hold belongs to the opponent, the ability to
    // the equipment's controller. Queue it before CHANNEL, with other Holds.
    private static MatchState QueueDeploymentHeldAbilities(MatchState state,
        IReadOnlyList<string> fields, List<GameEvent> events)
    {
        var queue = state.TriggerQueue.ToList();
        foreach (var card in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (card.IsFaceDown || string.IsNullOrWhiteSpace(card.CardNo)
                || !card.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal)
                || !card.Tags.Contains(CardEquipmentKeywordNames.Deploy, StringComparer.Ordinal)
                || !IsObjectOnField(state.PlayerZones, card.ObjectId)
                || !state.ObjectLocations.TryGetValue(card.ObjectId, out var location)
                || location.Zone != "BATTLEFIELD" || location.BattlefieldObjectId is not { } field
                || !fields.Contains(field, StringComparer.Ordinal)
                || BattlefieldLocalRules.Battlefield(state, field) is not { } battlefield) continue;
            var controller = EffectiveFieldControllerId(state.PlayerZones, card.ObjectId, card);
            if (!state.Seats.ContainsKey(controller) || controller == state.TurnPlayerId) continue;
            var context = new HeldTriggerContext(card.CardNo, field, DeploymentHeldKind, SourceGeneration: card.ObjectGeneration)
            {
                Deployment = new(new(card.ObjectId, card.ObjectGeneration), new(field, battlefield.ObjectGeneration),
                    controller, state.TurnPlayerId!, state.Tick, card.CopyFaceTransitions?.Count ?? 0, card.TokenFactoryCardNo,
                    card.CardNo == Rad136CardNo ? card.DockCageExile : null)
            };
            var trigger = new TriggerQueueItemState($"deploy-held-{state.TurnNumber}-{state.Tick}-{card.ObjectId}",
                controller, card.ObjectId, "HOLD_" + DeploymentHeldKind, "BATTLEFIELD_HELD")
                { SourceCardNo = card.CardNo, HeldContext = context };
            queue.Add(trigger);
            events.Add(BuildTriggerQueuedEvent(trigger));
        }
        return state with { TriggerQueue = queue };
    }

    private static bool ValidDeploymentHeldContext(MatchState state, HeldTriggerContext c,
        string effect, string controller, string source, string? cardNo)
        => c.Kind == DeploymentHeldKind && effect == "HOLD_" + DeploymentHeldKind && cardNo == c.CardNo
            && c.Amount == 0 && c.SourceGeneration >= 0 && c.RequiredExactOtherFriendlyBattlefieldUnits is null
            && c.Origin is null && c.RuleTextInstanceId is null
            && state.Phase == MatchPhases.TurnStart && state.TurnStartStep == "CHANNEL"
            && c.Deployment is { Source: { Generation: >= 0 } binding, Battlefield: { Generation: >= 0 } field,
                CapturedTick: > 0, SourceFaceRevision: >= 0 } origin
            && binding.ObjectId == source && binding.Generation == c.SourceGeneration
            && binding.ObjectId != field.ObjectId && field.ObjectId == c.BattlefieldObjectId
            && origin.ControllerId == controller && state.Seats.ContainsKey(controller)
            && origin.HoldingPlayerId == state.TurnPlayerId && origin.HoldingPlayerId != controller
            && origin.CapturedTick <= state.Tick
            && BattlefieldLocalRules.Battlefield(state, field.ObjectId) is { } battlefield
            && battlefield.ObjectGeneration >= field.Generation
            && (c.CardNo != Rad136CardNo
                ? origin.ExiledCard is null
                : origin.ExiledCard is null || ValidDockCageExileContext(state, origin.ExiledCard, controller))
            && PredictBindingMatches(state, binding, c.CardNo, origin.SourceFaceRevision, origin.CapturedTick, origin.TokenFactoryCardNo);

    private static bool ValidDeploymentHeldStack(MatchState state, StackItemState item)
        => ValidDeploymentHeldContext(state, item.HeldContext!, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null && item.OptionalCosts.Count == 0
            && item.DamageAmount == 0 && item.EffectRepeatCount == 1 && item.Destination.Length == 0;

    private static StackResolutionResult ResolveDeploymentHeld(MatchState state, StackItemState item)
    {
        if (!ValidDeploymentHeldStack(state, item)) throw new InvalidOperationException("Invalid captured deployment Hold ability.");
        var events = new List<GameEvent> { new("TRIGGER_RESOLVED", "部署的对手据守技能结算", new Dictionary<string, object?> {
            ["triggerId"] = item.StackItemId,
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
            ["controllerId"] = item.ControllerId, ["effectKind"] = item.EffectKind,
            ["battlefieldObjectId"] = item.HeldContext!.BattlefieldObjectId }) };
        var result = NoopStackResolutionResult(state);
        // CN 191.5.b / 383.2.a.1: later source changes do not undo a captured trigger.
        // A source that left the field and re-entered is a different object.
        if (item.EffectPlayCompleted)
            return result;
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration != item.HeldContext!.SourceGeneration
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return result with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (item.CardNo == Rad106CardNo)
        {
            if (!TryReturnTargetToHand(zones, cards, source.ObjectId, out var owner, out var equipment))
                return result with { Events = events };
            events.Add(BuildReturnedToHandEvent("好东西", item, source.ObjectId, owner, equipment));
            return result with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
        }
        var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
            state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(), state.RunePools, objectLocations: state.ObjectLocations,
            explicitDestroyObjectIds: new HashSet<string> { source.ObjectId }, explicitDestroyReason: "SKILL_EFFECT");
        var resolved = result with { PlayerZones = zones, CardObjects = cards, RunePools = removal.RunePools,
            Events = events.Concat(removal.Events).ToArray(), TriggerQueue = removal.TriggerQueue,
            DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        if (item.CardNo == Rad136CardNo && item.HeldContext.Deployment?.ExiledCard is { } exiled
            && !string.IsNullOrWhiteSpace(exiled.OwnerId)
            && !P6TokenFactoryCatalog.IsTokenFactory(exiled.TokenFactoryCardNo)
            && state.PlayerZones.TryGetValue(exiled.OwnerId, out var ownerZones)
            && ownerZones.Banished.Contains(exiled.ExiledCard.ObjectId, StringComparer.Ordinal)
            && state.CardObjects.TryGetValue(exiled.ExiledCard.ObjectId, out var exiledState)
            && exiledState.ObjectGeneration == exiled.ExiledCard.Generation
            && exiledState.CardNo == exiled.CardNo
            && exiledState.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal))
        {
            var parent = item with { EffectPlayCompleted = false };
            var pending = new PendingEffectPlayState(
                $"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId, parent,
                "BANISHED", new Dictionary<string, long>(StringComparer.Ordinal) {
                    [exiled.ExiledCard.ObjectId] = exiled.ExiledCard.Generation },
                false, false, 0, "", false, IgnoreAllCosts: true)
            {
                SourcePlayerId = exiled.OwnerId == item.ControllerId ? null : exiled.OwnerId
            };
            return resolved with { PendingEffectPlay = pending };
        }
        return resolved;
    }

    internal static bool ValidDockCageExileContext(MatchState state, DockCageExileContext context, string controller)
    {
        if (context.ExiledCard is not { Generation: > 0 } binding
            || string.IsNullOrWhiteSpace(context.CardNo)
            || !state.Seats.ContainsKey(context.OwnerId)
            || !state.PlayerZones.TryGetValue(context.OwnerId, out var zones)
            || context.TokenFactoryCardNo is not null)
            return false;
        return state.ObjectLocations.GetValueOrDefault(binding.ObjectId) == new ObjectLocationState(context.OwnerId, "BANISHED")
            && state.CardObjects.TryGetValue(binding.ObjectId, out var card)
            && card.ObjectGeneration == binding.Generation && card.CardNo == context.CardNo
            && card.OwnerId == context.OwnerId && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal);
    }
}
