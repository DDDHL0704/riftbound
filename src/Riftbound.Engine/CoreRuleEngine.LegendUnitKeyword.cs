using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LegendUnitKeywordContext(string AbilityId, long SourceGeneration);

public sealed partial class CoreRuleEngine
{
    internal static bool IsLegendUnitKeywordEffect(string effect)
        => effect == LegendAbilityEffectKinds.GrantUnitKeyword;

    internal static bool ValidLegendUnitKeyword(MatchState state, StackItemState item)
    {
        if (item.LegendUnitKeyword is not { } context) return !IsLegendUnitKeywordEffect(item.EffectKind);
        if (!IsLegendUnitKeywordEffect(item.EffectKind) || !TryGetLegendAbility(context.AbilityId, out var ability)
            || ability.EffectKind != item.EffectKind || string.IsNullOrEmpty(ability.GrantedCombatKeyword)
            || !TemporaryCombatKeywordRules.IsValid("COMBAT_KEYWORD:" + ability.GrantedCombatKeyword + ":1")
            || !LegendActionAbilityCatalog.IsSourceCardNoForAbility(context.AbilityId, item.CardNo)
            || context.SourceGeneration < 0 || !state.Seats.ContainsKey(item.ControllerId)
            || string.IsNullOrWhiteSpace(item.SourceObjectId) || item.SourceConfirmed || item.EffectRepeatCount != 1
            || item.DamageAmount != 0 || item.OptionalCosts.Count != 0 || item.Destination.Length != 0
            || item.TargetObjectIds is not { Count: 1 } || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } bindings
            || !bindings.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && generation > target.ObjectGeneration
            || state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && (context.SourceGeneration > source.ObjectGeneration
                    || context.SourceGeneration == source.ObjectGeneration && source.CardNo != item.CardNo)) return false;
        return item.TimingContext is TimingStates.NeutralOpen or TimingStates.SpellDuelOpen;
    }

    private static ResolutionResult QueueLegendUnitKeyword(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, IReadOnlyList<string> targets,
        IReadOnlyList<string> optionalCosts, List<GameEvent> events)
    {
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-KEYWORD", player, sourceId,
            ability.EffectKind, cardNo, targets, optionalCosts: optionalCosts,
            timingContext: StackTimingContextForNewStackItem(state)) {
            LegendUnitKeyword = new(ability.AbilityId, state.CardObjects[sourceId].ObjectGeneration),
            TargetGenerations = targets.ToDictionary(id => id, id => state.CardObjects[id].ObjectGeneration, StringComparer.Ordinal)
        };
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = player,
            TimingState = state.TimingState == TimingStates.SpellDuelOpen ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = [..state.StackItems, item] };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = targets.ToArray() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendUnitKeyword(MatchState state, StackItemState item)
    {
        if (!ValidLegendUnitKeyword(state, item)) return NoopStackResolutionResult(state);
        var context = item.LegendUnitKeyword!;
        TryGetLegendAbility(context.AbilityId, out var ability);
        var events = new List<GameEvent> { new("LEGEND_ABILITY_RESOLVED", "传奇单位关键词技能结算", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
            ["cardNo"] = item.CardNo, ["abilityId"] = context.AbilityId, ["stackItemId"] = item.StackItemId }) };
        var id = item.TargetObjectIds[0];
        if (!IsValidLegendAbilityTarget(state, item.ControllerId, id, ability)
            || !state.CardObjects.TryGetValue(id, out var target) || !IsFaceUpNonStandbyUnit(target)
            || target.ObjectGeneration != item.TargetGenerations![id])
        {
            events.Add(new("ABILITY_NO_EFFECT", "目标不再合法，已支付的费用不退回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = context.AbilityId,
                ["stackItemId"] = item.StackItemId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
            return NoopStackResolutionResult(state) with { Events = events };
        }
        var definition = "COMBAT_KEYWORD:" + ability.GrantedCombatKeyword;
        var effect = TemporaryCombatKeywordRules.AppliedEffectId(target, definition);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        cards[id] = target with { UntilEndOfTurnEffects = target.UntilEndOfTurnEffects.Append(effect).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray() };
        events.Add(new("STATUS_EFFECT_APPLIED", "传奇技能给予单位关键词", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["effectId"] = definition }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
