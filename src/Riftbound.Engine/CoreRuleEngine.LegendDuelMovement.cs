using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendDuelMovementContext(string AbilityId, long SourceGeneration, ObjectBinding Battlefield);

public sealed partial class CoreRuleEngine
{
    internal static bool IsLegendDuelMovementEffect(string effect) => effect == LegendAbilityEffectKinds.MoveDuelUnit;

    // A duel concerns the units currently on its battlefield, including during
    // the closed response window before the skill resolves.
    internal static bool IsLegendDuelUnit(MatchState state, string player, string id)
    {
        var duel = state.SpellDuelState;
        return duel.IsActive && BattlefieldLocalRules.Battlefield(state, duel.BattlefieldObjectId) is not null
            && state.PlayerZones.TryGetValue(player, out var zones) && zones.Battlefields.Contains(id)
            && state.CardObjects.TryGetValue(id, out var card) && IsFaceUpNonStandbyUnit(card)
            && EffectiveFieldControllerId(state.PlayerZones, id, card) == player
            && state.ObjectLocations.TryGetValue(id, out var at) && at.Zone == "BATTLEFIELD"
            && at.BattlefieldObjectId == duel.BattlefieldObjectId;
    }

    internal static bool ValidLegendDuelMovement(MatchState state, StackItemState item)
    {
        if (item.LegendDuelMovement is not { } context) return !IsLegendDuelMovementEffect(item.EffectKind);
        if (!IsLegendDuelMovementEffect(item.EffectKind) || !TryGetLegendAbility(context.AbilityId, out var ability)
            || ability.EffectKind != item.EffectKind || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            || context.SourceGeneration < 0 || !state.Seats.ContainsKey(item.ControllerId)
            || string.IsNullOrWhiteSpace(item.SourceObjectId) || item.SourceConfirmed || item.EffectRepeatCount != 1
            || item.DamageAmount != 0 || item.OptionalCosts.Count != 0 || item.Destination.Length != 0
            || item.TargetObjectIds is not { Count: 1 } || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } bindings
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && generation > target.ObjectGeneration
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && (context.SourceGeneration > source.ObjectGeneration
                    || context.SourceGeneration == source.ObjectGeneration && source.CardNo != item.CardNo)
            || context.Battlefield is not { Generation: >= 0 } field || string.IsNullOrWhiteSpace(field.ObjectId)
            || !state.CardObjects.TryGetValue(field.ObjectId, out var battlefield)
            || battlefield.ObjectGeneration < field.Generation
            || battlefield.ObjectGeneration == field.Generation && !battlefield.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag)) return false;
        return item.TimingContext == TimingStates.SpellDuelOpen;
    }

    private static ResolutionResult QueueLegendDuelMovement(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets,
        IReadOnlyList<string> optionalCosts, List<GameEvent> events)
    {
        var field = state.SpellDuelState.BattlefieldObjectId!;
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-DUEL-MOVE", player, sourceId,
            ability.EffectKind, cardNo, targets, optionalCosts: optionalCosts,
            timingContext: StackTimingContextForNewStackItem(state)) {
            LegendDuelMovement = new(ability.AbilityId, state.CardObjects[sourceId].ObjectGeneration,
                new(field, state.CardObjects[field].ObjectGeneration)),
            TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal)
        };
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = player,
            TimingState = TimingStates.SpellDuelClosed, PriorityPlayerId = player,
            PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = targets.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendDuelMovement(MatchState state, StackItemState item)
    {
        if (!ValidLegendDuelMovement(state, item)) return NoopStackResolutionResult(state);
        var context = item.LegendDuelMovement!;
        TryGetLegendAbility(context.AbilityId, out var ability);
        var events = new List<GameEvent> { new("LEGEND_ABILITY_RESOLVED", "传奇迅捷移动技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId }) };
        var id = item.TargetObjectIds[0];
        if (!IsLegendDuelUnit(state, item.ControllerId, id)
            || state.SpellDuelState.BattlefieldObjectId != context.Battlefield.ObjectId
            || state.CardObjects[context.Battlefield.ObjectId].ObjectGeneration != context.Battlefield.Generation
            || state.CardObjects[id].ObjectGeneration != item.TargetGenerations![id])
            return NoopStackResolutionResult(state) with { Events = events };
        // CN 135.2.b.5.b: the turn condition governs both instructions;
        // the empowered condition governs only the subsequent ready instruction.
        if (ability.ResolveOnlyOnControllerTurn && state.TurnPlayerId != item.ControllerId)
            return NoopStackResolutionResult(state) with { Events = events };
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var locations = state.ObjectLocations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        TryMoveFieldUnitByEffect(state, zones, cards, locations, id, new(item.ControllerId, "BASE"),
            item.ControllerId, item, events);
        if (ability.ReadyTargetIfSourceEmpowered && cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == context.SourceGeneration
            && source.IsEmpowered && state.PlayerZones[item.ControllerId].LegendZone.Contains(source.ObjectId)
            && cards.TryGetValue(id, out var unit) && unit.ObjectGeneration == item.TargetGenerations![id]
            && !AttachedRuleTextRules.Instances(state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations }, unit)
                .Any(instance => CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)))
            ReadyLegendFriendlyUnit(zones, cards, id, item.ControllerId, item.SourceObjectId, context.AbilityId, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
            ObjectLocations = locations, Events = events };
    }
}
