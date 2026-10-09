using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record LegendConquestContext(string CardNo, string Kind, string BattlefieldId, long SourceGeneration, int Overkill);

public sealed partial class CoreRuleEngine
{
    internal const string LegendConquestEffect = "LEGEND_CONQUEST_READY";

    private static (string Kind, TriggerSpec Spec)? LegendConquestDefinition(string? no)
    {
        if (!LegendConquestTriggerSpecRules.TryGetTrigger(no, t =>
            LegendConquestTriggerSpecRules.IsLegendConquestPayReadySelfTrigger(t)
            || LegendConquestTriggerSpecRules.IsLegendConquestReadySelfTrigger(t)
            || LegendConquestTriggerSpecRules.IsLegendConquestOverkillExhaustReadyUnitTrigger(t), out var spec)) return null;
        return (LegendConquestTriggerSpecRules.IsLegendConquestPayReadySelfTrigger(spec) ? "PAY_READY_SELF"
            : LegendConquestTriggerSpecRules.IsLegendConquestReadySelfTrigger(spec) ? "READY_SELF" : "EXHAUST_READY_UNIT", spec);
    }

    internal static bool ValidLegendConquest(LegendConquestContext context, string effect, string? no = null)
        => effect == LegendConquestEffect && (no is null || no == context.CardNo) && context.SourceGeneration >= 0
            && !string.IsNullOrWhiteSpace(context.BattlefieldId) && context.Overkill >= 0
            && LegendConquestDefinition(context.CardNo) is { } definition && definition.Kind == context.Kind
            && context.Overkill >= definition.Spec.RequiredOverkillDamage.GetValueOrDefault();

    private static IEnumerable<TriggerQueueItemState> CaptureLegendConquest(
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        string player, string field, int overkill, long tick)
    {
        foreach (var id in zones[player].LegendZone)
        {
            if (!cards.TryGetValue(id, out var card) || card.ControllerId != player || card.IsFaceDown
                || LegendConquestDefinition(card.CardNo) is not { } definition
                || overkill < definition.Spec.RequiredOverkillDamage.GetValueOrDefault()) continue;
            yield return new TriggerQueueItemState($"conquest-ready-{tick}-{field}-{id}", player, id, LegendConquestEffect, "BATTLEFIELD_CONQUERED") {
                LegendConquest = new(card.CardNo!, definition.Kind, field, card.ObjectGeneration, overkill) };
        }
    }

    private static bool HasLegendConquestTarget(StackItemState item) => item.LegendConquest?.Kind == "EXHAUST_READY_UNIT";
    private static bool CanPayLegendConquestExhaustion(MatchState state, StackItemState item)
        => state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.LegendConquest!.SourceGeneration && source.ControllerId == item.ControllerId
            && !source.IsExhausted && !source.IsFaceDown && state.PlayerZones[item.ControllerId].LegendZone.Contains(source.ObjectId);

    private static string[] LegendConquestTargets(MatchState state, StackItemState item)
        => state.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.UnitCard) && !c.IsFaceDown
            && !c.Tags.Contains(CardObjectTags.Standby) && IsObjectOnField(state.PlayerZones, c.ObjectId)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, c.ObjectId))
            .Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static PendingCardChoiceState LegendConquestChoice(MatchState state, StackItemState item)
        => new("CONQUEST-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 0, 1,
            LegendConquestTargets(state, item), [item.LegendConquest!.BattlefieldId],
            "选择一名单位，并横置传奇作为费用；不选则放弃。支付适用的法盾费用后双方响应，结算时才活跃所选单位。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareLegendConquestTarget(ResolutionResult result, StackItemState item)
    {
        var choice = LegendConquestChoice(result.State, item);
        if (!CanPayLegendConquestExhaustion(result.State, item) || choice.LegalObjectIds.Count == 0)
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, PriorityPlayerId = null, ActivePlayerId = item.ControllerId };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidLegendConquestChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.LegendConquest is not { } context || !HasLegendConquestTarget(item) || item.TriggerCost is not null
            || item.TargetGenerations is not null || !ValidLegendConquest(context, item.EffectKind, item.CardNo)) return false;
        var expected = LegendConquestChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId
            && choice.EffectKind == item.EffectKind && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.RequiredCount == 0 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult ResolveLegendConquest(MatchState state, StackItemState item)
    {
        var context = item.LegendConquest!;
        if (context.Kind != "READY_SELF" && item.TriggerCost is null) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var events = new List<GameEvent>();
        if (HasLegendConquestTarget(item))
        {
            if (item.TargetObjectIds.SingleOrDefault() is { } target && LegendConquestTargets(state, item).Contains(target)
                && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards[target].ObjectGeneration && cards[target].IsExhausted)
            {
                cards[target] = cards[target] with { IsExhausted = false };
                events.Add(new("UNIT_READIED", "征服技能使所选单位活跃", new Dictionary<string, object?> {
                    ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target,
                    ["reason"] = item.EffectKind }));
            }
        }
        else if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.SourceGeneration
            && state.PlayerZones.Values.Any(z => z.LegendZone.Contains(source.ObjectId)) && source.IsExhausted)
        {
            cards[source.ObjectId] = source with { IsExhausted = false };
            events.Add(new("LEGEND_READIED", "征服技能使传奇活跃", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = source.ObjectId }));
        }
        events.Add(new("LEGEND_TRIGGER_RESOLVED", "征服技能结算完成", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["legendObjectId"] = item.SourceObjectId, ["legendCardNo"] = item.CardNo,
            ["trigger"] = LegendConquestDefinition(context.CardNo)!.Value.Spec.Kind, ["battlefieldId"] = context.BattlefieldId }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
