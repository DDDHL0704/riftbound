using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static StackResolutionResult ResolveCombatKeywordDouble(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid combat keyword double context.");
        var targetId = item.TargetObjectIds.SingleOrDefault();
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        if (targetId is not null && cards.TryGetValue(targetId, out var target)
            && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == target.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(targetId))
        {
            var keyword = CombatMightSpec(context.CardNo, context.Kind)!.GrantedKeyword!;
            // CN433.1.a: doubling adds the value at resolution; later grants are not doubled.
            var amount = CurrentCombatKeywordValue(state, target, keyword);
            if (amount > 0)
            {
                var definition = TemporaryCombatKeywordRules.Prefix + keyword + amount;
                var effect = TemporaryCombatKeywordRules.AppliedEffectId(target, definition);
                cards[targetId] = target with { UntilEndOfTurnEffects = target.UntilEndOfTurnEffects.Append(effect)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                events.Add(new("STATUS_EFFECT_APPLIED", "本回合数值关键词翻倍", new Dictionary<string, object?> {
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__"),
                    ["targetObjectId"] = targetId, ["effectId"] = definition, ["keywordBonus"] = amount }));
            }
        }
        events.Add(new("TRIGGER_RESOLVED", "数值关键词翻倍触发技能结算完成", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
