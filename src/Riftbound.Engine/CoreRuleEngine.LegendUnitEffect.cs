using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendUnitEffectContext(string AbilityId, long SourceGeneration, int SpellshieldTaxPower);

public sealed partial class CoreRuleEngine
{
    internal static bool IsLegendUnitEffect(string effect)
        => effect is LegendAbilityEffectKinds.UnitPowerModifier or LegendAbilityEffectKinds.ReadyUnit;

    internal static bool IsLegendUnitEffectTarget(MatchState state, string player, string id, bool battlefieldOnly)
        => IsFieldObject(state.PlayerZones, id) && state.CardObjects.TryGetValue(id, out var card)
            && IsFaceUpNonStandbyUnit(card) && !string.IsNullOrWhiteSpace(card.CardNo)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id)
            && (!battlefieldOnly || state.ObjectLocations.TryGetValue(id, out var location) && location.Zone == MoveUnitBattlefieldZone);

    internal static bool ValidLegendUnitEffect(MatchState state, StackItemState item)
    {
        if (item.LegendUnitEffect is not { } context) return !IsLegendUnitEffect(item.EffectKind);
        if (!IsLegendUnitEffect(item.EffectKind) || !TryGetLegendAbility(context.AbilityId, out var ability)
            || ability.EffectKind != item.EffectKind || !ability.RemovesSourceEmpowerAsCost || !ability.RequiresAnyUnitTarget
            || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            || context.SourceGeneration < 0 || context.SpellshieldTaxPower < 0 || !state.Seats.ContainsKey(item.ControllerId)
            || string.IsNullOrWhiteSpace(item.SourceObjectId) || item.SourceConfirmed || item.EffectRepeatCount != 1
            || item.DamageAmount != 0 || item.OptionalCosts.Count != 0 || item.Destination.Length != 0
            || item.TargetObjectIds is not { Count: 1 } || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } bindings
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && generation > target.ObjectGeneration
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && (context.SourceGeneration > source.ObjectGeneration
                    || context.SourceGeneration == source.ObjectGeneration && source.CardNo != item.CardNo)) return false;
        return item.TimingContext == TimingStates.NeutralOpen;
    }

    private static ResolutionResult QueueLegendUnitEffect(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets, IReadOnlyList<string> optionalCosts,
        int spellshieldTax, List<GameEvent> events)
    {
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-LEGEND-UNIT", player, sourceId,
            ability.EffectKind, cardNo, targets, optionalCosts: optionalCosts,
            timingContext: StackTimingContextForNewStackItem(state)) {
            LegendUnitEffect = new(ability.AbilityId, state.CardObjects[sourceId].ObjectGeneration, spellshieldTax),
            TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal)
        };
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = player, TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = targets.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendUnitEffect(MatchState state, StackItemState item)
    {
        if (!ValidLegendUnitEffect(state, item)) throw new InvalidOperationException("Invalid captured legend unit effect.");
        var context = item.LegendUnitEffect!;
        TryGetLegendAbility(context.AbilityId, out var ability);
        var events = new List<GameEvent> { new("LEGEND_ABILITY_RESOLVED", "传奇单位技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId }) };
        var id = item.TargetObjectIds[0];
        if (!IsLegendUnitEffectTarget(state, item.ControllerId, id, ability.RequiresBattlefieldTarget)
            || !state.CardObjects.TryGetValue(id, out var target) || target.ObjectGeneration != item.TargetGenerations![id])
        {
            events.Add(new("ABILITY_NO_EFFECT", "目标不再合法，已支付的费用不退回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = context.AbilityId,
                ["stackItemId"] = item.StackItemId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
            return NoopStackResolutionResult(state) with { Events = events };
        }
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (ability.EffectKind == LegendAbilityEffectKinds.UnitPowerModifier)
        {
            var behavior = new CardBehaviorDefinition(item.CardNo, ability.DisplayName, 0, item.EffectKind, 0, 1,
                TargetScope: CardTargetScopes.AnyUnit, PowerModifierAmount: ability.PowerModifierAmount);
            cards[id] = ApplyPowerModifier(state, target, behavior, item, id, ability.PowerModifierAmount, out var modified);
            events.Add(modified);
        }
        else if (!AttachedRuleTextRules.Instances(state, target)
            .Any(instance => CardStaticAbilitySpecRules.CardCannotBecomeActive(instance.CardNo)))
        {
            AddReadiedObservation(state.PlayerZones, cards, target, events);
            cards[id] = target with { IsExhausted = false };
            events.Add(CaptureSelfReady(new("UNIT_READIED", "单位变为活跃状态", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                ["abilityId"] = context.AbilityId, ["wasExhausted"] = target.IsExhausted, ["isExhausted"] = false }), target, item.ControllerId, state.PlayerZones, cards));
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
