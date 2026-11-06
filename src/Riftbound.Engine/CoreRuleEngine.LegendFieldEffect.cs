using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendFieldEffectContext(string AbilityId, ObjectBinding Source, string CardNo,
    string ControllerId, string StackItemId, long CapturedTick, long FaceRevision, int SpellshieldTaxPower,
    string OriginZone, ObjectBinding? DestinationBattlefield);

public sealed partial class CoreRuleEngine
{
    internal static bool IsLegendFieldEffect(string effect)
        => effect is LegendAbilityEffectKinds.MoveFriendlyUnit
            or LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin
            or LegendAbilityEffectKinds.ReturnOwnedTeemoUnitToHand;

    internal static string LegendFieldEffectLabel(string effect) => effect switch {
        LegendAbilityEffectKinds.MoveFriendlyUnit => "传奇技能：在战场与基地之间移动单位",
        LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin => "传奇技能：返回手牌，然后打出休眠金币",
        _ => "传奇技能：将拥有的提莫单位返回手牌" };

    private static bool IsLegendFieldTarget(MatchState state, string player, string id, LegendAbilityDefinition ability)
        => ability.RequiresOwnedTeemoUnitTarget ? IsValidOwnedTeemoUnitTarget(state, player, id)
            : IsValidLegendAbilityTarget(state, player, id, ability)
                && state.CardObjects.TryGetValue(id, out var target) && IsFaceUpNonStandbyUnit(target)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id);

    // Moving between a battlefield and a base declares the destination before
    // responses. The command checkpoint records this choice before paying costs.
    private static string DeclareLegendFieldDestination(MatchState state, string player, string target,
        LegendAbilityDefinition ability)
    {
        if (ability.EffectKind != LegendAbilityEffectKinds.MoveFriendlyUnit) return string.Empty;
        if (!TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, target, out var origin))
            throw new InvalidOperationException("A legend movement target must have a public field location.");
        if (origin.Zone == "BATTLEFIELD") return "BASE:" + player;
        var options = ChosenMovementDestinations(state, player)
            .Where(option => option.Id.StartsWith("BATTLEFIELD:", StringComparison.Ordinal))
            .Select(option => new RuleChoiceOption(option.Id, option.Label)).ToArray();
        return options.Length == 0 ? string.Empty : RuleChoices.Value!.Choose(player,
            "选择此单位移动到的战场。", options, kind: "MOVEMENT_DESTINATION", preview: state);
    }

    internal static bool ValidLegendFieldEffect(MatchState state, StackItemState item)
    {
        if (item.LegendFieldEffect is not { } context) return !IsLegendFieldEffect(item.EffectKind);
        if (!IsLegendFieldEffect(item.EffectKind)
            || !TryGetLegendAbility(context.AbilityId, out var ability) || ability.EffectKind != item.EffectKind
            || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, context.CardNo)
            || context.Source is not { Generation: >= 0 } || context.Source.ObjectId != item.SourceObjectId
            || context.CardNo != item.CardNo || context.ControllerId != item.ControllerId
            || !state.Seats.ContainsKey(context.ControllerId) || context.StackItemId != item.StackItemId
            || context.CapturedTick < 0 || context.CapturedTick > state.Tick || context.FaceRevision < 0
            || context.StackItemId != $"STACK-{context.CapturedTick}-{context.Source.ObjectId}-LEGEND-FIELD"
            || !PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision, context.CapturedTick, null)
            || context.SpellshieldTaxPower < 0 || ability.RequiresFriendlyUnitTarget && context.SpellshieldTaxPower != 0
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.DamageAmount != 0
            || item.TargetObjectIds is not { Count: 1 } || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } bindings
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && generation > target.ObjectGeneration
            || item.TimingContext != TimingStates.NeutralOpen) return false;
        var costs = string.IsNullOrWhiteSpace(ability.RequiredCostToken)
            ? Array.Empty<string>() : new[] { ability.RequiredCostToken };
        if (!item.OptionalCosts.SequenceEqual(costs, StringComparer.Ordinal)) return false;
        if (ability.EffectKind != LegendAbilityEffectKinds.MoveFriendlyUnit)
            return item.Destination.Length == 0 && context.OriginZone.Length == 0 && context.DestinationBattlefield is null;
        if (context.OriginZone == "BATTLEFIELD")
            return item.Destination == "BASE:" + item.ControllerId && context.DestinationBattlefield is null;
        if (context.OriginZone != "BASE" || ability.RequiresBattlefieldTarget) return false;
        // With no battlefield in play, the legal skill simply has no movement.
        if (item.Destination.Length == 0) return context.DestinationBattlefield is null;
        return context.DestinationBattlefield is { Generation: >= 0 } field
            && item.Destination == "BATTLEFIELD:" + field.ObjectId
            && state.CardObjects.TryGetValue(field.ObjectId, out var battlefield)
            && battlefield.ObjectGeneration >= field.Generation
            && (battlefield.ObjectGeneration != field.Generation
                || BattlefieldLocalRules.Battlefield(state, field.ObjectId) is not null);
    }

    private static ResolutionResult QueueLegendFieldEffect(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets,
        IReadOnlyList<string> optionalCosts, int spellshieldTax, string destination, List<GameEvent> events)
    {
        var source = state.CardObjects[sourceId]; var tick = state.Tick + 1;
        var stackId = $"STACK-{tick}-{sourceId}-LEGEND-FIELD";
        var originZone = ability.EffectKind == LegendAbilityEffectKinds.MoveFriendlyUnit
            && TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, targets[0], out var origin)
                ? origin.Zone : string.Empty;
        ObjectBinding? field = destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
            ? new(destination[12..], state.CardObjects[destination[12..]].ObjectGeneration) : null;
        var item = new StackItemState(stackId, player, sourceId, ability.EffectKind, cardNo, targets,
            optionalCosts: optionalCosts, destination: destination, timingContext: StackTimingContextForNewStackItem(state)) {
            LegendFieldEffect = new(ability.AbilityId, new(sourceId, source.ObjectGeneration), cardNo, player,
                stackId, tick, source.CopyFaceTransitions?.Count ?? 0, spellshieldTax, originZone, field),
            TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal) };
        var next = state with { Tick = tick, ActivePlayerId = player, TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = stackId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = targets.ToArray(), ["destination"] = destination }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendFieldEffect(MatchState state, StackItemState item)
    {
        if (!ValidLegendFieldEffect(state, item)) throw new InvalidOperationException("Invalid captured legend field effect.");
        var context = item.LegendFieldEffect!; TryGetLegendAbility(context.AbilityId, out var ability);
        bool LegalTarget(string id) => StackObjectRules.ReferencesCurrentObject(state, item, id)
            && IsLegendFieldTarget(state, item.ControllerId, id, ability);
        StackResolutionResult result;
        if (ability.EffectKind != LegendAbilityEffectKinds.MoveFriendlyUnit)
            result = ResolveUnitReturnInstructions(state, item, LegalTarget, ability.RequiresOwnedTeemoUnitTarget,
                ability.EffectKind == LegendAbilityEffectKinds.ReturnBattlefieldUnitAndCreateCoin, item.CardNo!);
        else
        {
            var id = item.TargetObjectIds[0];
            bool SameOrigin(MatchState frame) => IsLegendFieldTarget(frame, item.ControllerId, id, ability)
                && StackObjectRules.ReferencesCurrentObject(frame, item, id)
                && TryGetPreciseFieldLocation(frame.PlayerZones, frame.ObjectLocations, id, out var at)
                && at.Zone == context.OriginZone;
            var liveField = context.DestinationBattlefield is null
                || BattlefieldLocalRules.Battlefield(state, context.DestinationBattlefield.ObjectId) is { } battlefield
                    && battlefield.ObjectGeneration == context.DestinationBattlefield.Generation;
            if (item.Destination.Length == 0 || !LegalTarget(id) || !SameOrigin(state) || !liveField)
                result = NoopStackResolutionResult(state);
            else
            {
                var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary();
                var locations = state.ObjectLocations.ToDictionary(); var pools = state.RunePools.ToDictionary();
                var events = new List<GameEvent>();
                var destination = item.Destination.StartsWith("BATTLEFIELD:", StringComparison.Ordinal)
                    ? new ObjectLocationState(item.ControllerId, "BATTLEFIELD", item.Destination[12..])
                    : new(item.ControllerId, "BASE");
                TryMoveFieldUnitByEffect(state, zones, cards, locations, pools, id, destination,
                    item.ControllerId, item, events, SameOrigin);
                result = NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards,
                    ObjectLocations = locations, RunePools = pools, Events = events };
            }
        }
        var resolved = new GameEvent("LEGEND_ABILITY_RESOLVED", "传奇场地技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId });
        return result with { Events = new[] { resolved }.Concat(result.Events).ToArray() };
    }
}
