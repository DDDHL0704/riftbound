using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record ChosenActionTarget(string StackItemId, ObjectBinding Target, bool IsCurrent);
    private sealed record TargetedActionResult(CardObjectState Target, GameEvent? Event);
    private sealed record TargetedReplacementCandidate(string Kind, EmpoweredRuleContributor Rule, int Bonus)
    {
        internal string Id => Kind + ":" + Rule.InstanceId;
    }

    private static bool CanExecuteStun(CardObjectState target)
        => !target.UntilEndOfTurnEffects.Contains("STUNNED", StringComparer.Ordinal);

    // Call only for an instruction whose spell/ability really selected this unit.
    // A stale chosen target is represented explicitly; it never becomes null
    // and falls through to an ordinary action on the new incarnation.
    private static ChosenActionTarget? CaptureChosenActionTarget(MatchState state, StackItemState item, string id)
    {
        if (!item.TargetObjectIds.Contains(id, StringComparer.Ordinal)) return null;
        var generation = item.TargetGenerations?.GetValueOrDefault(id, -1) ?? -1;
        return new(item.StackItemId, new(id, generation), generation >= 0
            && state.CardObjects.TryGetValue(id, out var target) && target.ObjectGeneration == generation);
    }

    private static bool CanExecuteReturn(MatchState state, CardObjectState target)
        => state.PlayerZones.Any(p => (p.Value.Base.Contains(target.ObjectId, StringComparer.Ordinal)
                || p.Value.Battlefields.Contains(target.ObjectId, StringComparer.Ordinal))
            && IsCardObjectControlledByPlayerOrLegacyOwned(state.CardObjects, p.Key, target.ObjectId));

    private static TargetedActionResult? ResolveChosenActionReplacement(MatchState state, StackItemState item,
        CardObjectState target, ChosenActionTarget? chosen, string kind, int? requested = null,
        int? normalized = null, int minimum = 0, int? snapshot = null)
    {
        if (chosen is null) return null;
        if (!chosen.IsCurrent || chosen.StackItemId != item.StackItemId || chosen.Target.ObjectId != target.ObjectId
            || chosen.Target.Generation != target.ObjectGeneration) return new(target, null);
        if (kind == EmpoweredTargetReplacementRules.Stun && !CanExecuteStun(target)) return new(target, null);
        if (kind == EmpoweredTargetReplacementRules.ReturnToHand && !CanExecuteReturn(state, target)) return null;
        if (kind == EmpoweredTargetReplacementRules.NegativeMight && normalized is not < 0) return null;
        var currentCards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        currentCards[target.ObjectId] = target;
        state = state with { CardObjects = currentCards };
        var targetRules = EmpoweredTargetReplacementRules.Instances(state, target, kind).ToArray();
        if (targetRules.Length == 0) return null;
        var frame = RuleChoices.Value ?? throw new InvalidOperationException("Chosen action replacement needs its rule command context.");
        var occurrence = frame.TargetedActionSequence++;
        var affectedController = EffectiveFieldControllerId(state.PlayerZones, target.ObjectId, target)
            ?? throw new InvalidOperationException("An affected field unit requires its current controller.");
        var candidates = targetRules.Select(p => new TargetedReplacementCandidate(EmpoweredTargetReplacementRules.TargetRule,
            CaptureTargetedContributor(target, p.Instance, affectedController), p.Spec.MightBonus)).ToList();
        if (kind == EmpoweredTargetReplacementRules.NegativeMight)
            candidates.AddRange(EmpoweredSpellRules.Sources(state, item.ControllerId)
                .Where(p => EmpoweredSpellRules.Bonus(p.Instance.CardNo) > 0)
                .Select(p => new TargetedReplacementCandidate(EmpoweredTargetReplacementRules.CasterRule,
                    CaptureTargetedContributor(p.Source, p.Instance, item.ControllerId), EmpoweredSpellRules.Bonus(p.Instance.CardNo))));
        var used = new HashSet<string>(StringComparer.Ordinal);
        var steps = new List<ChosenActionReplacementStep>();
        long raw = requested ?? 0; long? delta = normalized;
        while (true)
        {
            var eligible = candidates.Where(c => !used.Contains(c.Id)
                && (c.Kind == EmpoweredTargetReplacementRules.TargetRule
                    || kind == EmpoweredTargetReplacementRules.NegativeMight && delta is < 0))
                .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            if (eligible.Length == 0) throw new InvalidOperationException("An applicable mandatory target replacement was lost.");
            var selected = eligible.Length == 1 ? eligible[0].Id : frame.Choose(affectedController,
                "选择对该单位本次行动先生效的替换效果。",
                eligible.Select(c => new RuleChoiceOption(c.Id, c.Kind == EmpoweredTargetReplacementRules.TargetRule
                    ? "改为本回合战力增加" : "额外降低战力", [c.Rule.Source.ObjectId])).ToArray(),
                kind: "TARGETED_ACTION_REPLACEMENT", preview: state);
            var candidate = eligible.Single(c => c.Id == selected);
            used.Add(candidate.Id);
            if (candidate.Kind == EmpoweredTargetReplacementRules.TargetRule)
            {
                steps.Add(new(candidate.Kind, candidate.Rule, delta, candidate.Bonus));
                // Like caster-side Might provenance, this belongs to the original
                // instruction's tick. Later synchronous answers must replay the
                // same intermediate witnesses and request hashes.
                var context = new ChosenActionReplacementContext(kind, state.Tick + 1, item.StackItemId,
                    occurrence, item.ControllerId, item.SourceObjectId, item.CardNo, chosen.Target,
                    requested, normalized, minimum, snapshot, steps.ToArray());
                return ApplyChosenActionReplacement(target, context, candidate.Bonus);
            }
            raw -= candidate.Bonus;
            var after = EmpoweredTargetReplacementRules.Normalize(raw, minimum, snapshot);
            steps.Add(new(candidate.Kind, candidate.Rule, delta, after)); delta = after;
        }
    }

    private static EmpoweredRuleContributor CaptureTargetedContributor(CardObjectState source,
        RuleTextInstance instance, string controller)
        => new(new(source.ObjectId, source.ObjectGeneration), instance.CardNo, instance.Id,
            controller, source.CopyFaceTransitions?.Count ?? 0, source.IsEmpowered, instance.Origin);

    // This positive action belongs to the replacement rule, whose controller
    // can differ from the original spell controller. The distinct private
    // witness keeps both origins; no synthetic spell changes that attribution.
    private static TargetedActionResult ApplyChosenActionReplacement(CardObjectState target,
        ChosenActionReplacementContext context, int bonus)
    {
        var rule = context.Steps[^1].Rule;
        var power = TargetedActionInt((long)target.Power + bonus, "战力增加结果");
        var cumulative = TargetedActionInt((long)target.UntilEndOfTurnPowerModifier + bonus, "临时战力合计");
        var basePower = TargetedActionInt((long)target.Power - target.UntilEndOfTurnPowerModifier, "战力基础值");
        var previousOrder = target.UntilEndOfTurnPowerModifiers.Select((m, index) => m.AppliedOrder ?? index + 1).DefaultIfEmpty(0).Max();
        var order = TargetedActionInt((long)previousOrder + 1, "战力记录顺序");
        var ledger = new PowerModifierLedgerEntry(EmpoweredTargetReplacementRules.EffectId(context, order),
            EmpoweredTargetReplacementRules.Effect, "UNTIL_END_OF_TURN", target.ObjectId,
            rule.Source.ObjectId, rule.CardNo, bonus, basePower, power, EmpoweredTargetReplacementRules.SourcePath,
            bonus, 0, power, order) { ChosenActionReplacement = context };
        var next = target with { Power = power, UntilEndOfTurnPowerModifier = cumulative,
            UntilEndOfTurnPowerModifiers = [..target.UntilEndOfTurnPowerModifiers, ledger] };
        return new(next, new GameEvent("POWER_MODIFIED_UNTIL_END_OF_TURN", "目标的替换技能令其本回合战力增加",
            new Dictionary<string, object?> { ["sourceObjectId"] = rule.Source.ObjectId, ["targetObjectId"] = target.ObjectId,
                ["playerId"] = rule.ControllerId, ["controllerId"] = rule.ControllerId,
                ["effectKind"] = EmpoweredTargetReplacementRules.Effect, ["powerDelta"] = bonus,
                ["appliedPowerDelta"] = bonus, ["minimumPower"] = 0, ["resultingPower"] = power,
                ["replacementKind"] = context.OriginalKind }));
    }

    private static int TargetedActionInt(long value, string operation)
        => value < int.MinValue || value > int.MaxValue
            ? throw new TargetedActionNumericLimitException(operation) : (int)value;

    // CN359.3.e.14.b: Reconsider's second instruction refers to the unit's
    // owner, not to a completed return event. An invalid ignored target never
    // reaches this branch; replacing a legal return preserves this instruction.
    private static void ResolveIndependentReturnOwnerRuneInstruction(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardBehaviorDefinition behavior, StackItemState item,
        string? originalOwner, List<GameEvent> events)
    {
        if (behavior.RuneCallCountAfterTargetReturn <= 0) return;
        if (string.IsNullOrWhiteSpace(originalOwner) || !zones.ContainsKey(originalOwner))
            throw new InvalidOperationException("The independent rune instruction requires the selected unit's original owner.");
        var runes = CallRunes(zones, cards, originalOwner, behavior.RuneCallCountAfterTargetReturn);
        events.Add(new("RUNES_CALLED", $"{originalOwner} 召出 {runes.CalledRuneObjectIds.Count} 张符文",
            new Dictionary<string, object?> { ["playerId"] = originalOwner, ["sourceObjectId"] = item.SourceObjectId,
                ["count"] = runes.CalledRuneObjectIds.Count, ["runeObjectIds"] = runes.CalledRuneObjectIds.ToArray() }));
    }
}
