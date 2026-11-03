using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<EmpoweredTargetReplacementSpec>>> EmpoweredTargetReplacements =
        new(() => OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(
            card => card.CardNo, card => EmpowerParser.TargetReplacements(card.CardEffect), StringComparer.Ordinal));

    // Call only after legality and event-existence checks. A cost, an already
    // stunned unit, and a floored-to-zero reduction supply no replaceable event.
    private static bool TryReplaceEmpoweredTargetEvent(MatchState? state, StackItemState item,
        CardObjectState target, string eventKind, out CardObjectState result, out GameEvent replaced)
    {
        result = target;
        replaced = default!;
        if (state is null || !StackObjectRules.ReferencesCurrentObject(state, item, target.ObjectId)
            || !StackObjectRules.IsSpell(item) && !StackObjectRules.IsAbility(item)) return false;
        return TryReplaceEmpoweredChosenEvent(state, target, eventKind, item.SourceObjectId, item.CardNo,
            item, out result, out replaced);
    }

    // Immediate legend actions have already validated their chosen target;
    // they use the same replacement without inventing a historical stack item.
    private static bool TryReplaceEmpoweredChosenEvent(MatchState state, CardObjectState target, string eventKind,
        string sourceId, string? sourceCardNo, StackItemState? item, out CardObjectState result, out GameEvent replaced)
    {
        result = target;
        replaced = default!;
        if (!target.IsEmpowered || target.IsFaceDown || !target.Tags.Contains(CardObjectTags.UnitCard)
            || !AttachedRuleTextRules.Field(state, target.ObjectId)) return false;
        var rule = AttachedRuleTextRules.Instances(state, target)
            .SelectMany(instance => EmpoweredTargetReplacements.Value.GetValueOrDefault(instance.CardNo, [])
                .Select(spec => (Instance: instance, Spec: spec)))
            .FirstOrDefault(rule => eventKind switch {
                "NEGATIVE_MIGHT" => rule.Spec.NegativeMight,
                "STUN" => rule.Spec.Stun,
                "RETURN_TO_HAND" => rule.Spec.ReturnToHand,
                _ => false
            });
        if (rule.Spec is null) return false;
        var behavior = new CardBehaviorDefinition(rule.Instance.CardNo, "已强化目标替换", 0,
            "EMPOWERED_TARGET_EVENT_REPLACEMENT", 0, 1, PowerModifierAmount: rule.Spec.Might);
        GameEvent powerEvent;
        if (item is not null)
            result = ApplyPowerModifier(state, target, behavior, item, target.ObjectId, rule.Spec.Might, out powerEvent);
        else
        {
            result = ApplyDirectUntilEndPowerModifier(target, target.ObjectId, sourceId, sourceCardNo,
                behavior.EffectKind, "CoreRuleEngine.TryReplaceEmpoweredChosenEvent", rule.Spec.Might,
                target.Power + rule.Spec.Might);
            powerEvent = new("POWER_MODIFIED_UNTIL_END_OF_TURN", "已强化目标替换为临时战力提升",
                new Dictionary<string, object?> { ["sourceObjectId"] = sourceId, ["targetObjectId"] = target.ObjectId,
                    ["powerDelta"] = rule.Spec.Might, ["appliedPowerDelta"] = rule.Spec.Might,
                    ["minimumPower"] = 0, ["resultingPower"] = result.Power });
        }
        replaced = powerEvent with { Payload = new Dictionary<string, object?>(powerEvent.Payload) {
            ["replacedEvent"] = eventKind, ["replacementCardNo"] = rule.Instance.CardNo,
            ["replacementInstanceId"] = rule.Instance.Id, ["targetGeneration"] = target.ObjectGeneration } };
        return true;
    }

    private static bool TryReplaceEmpoweredStun(MatchState state, StackItemState item,
        Dictionary<string, CardObjectState> cards, string targetId, List<GameEvent> events)
    {
        if (!TryReplaceEmpoweredTargetEvent(state with { CardObjects = cards }, item, cards[targetId],
            "STUN", out var replacement, out var replacementEvent)) return false;
        cards[targetId] = replacement;
        events.Add(replacementEvent);
        return true;
    }
}
