using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string DisarmKind = "ATTACK_DISARM";

    private static List<DisarmContribution> DisarmContributions(MatchState state, CardObjectState unit, string controller)
    {
        var contributions = new List<DisarmContribution>();
        foreach (var instance in AttachedRuleTextRules.Instances(state, unit))
            if (DisarmSourceSpecifications.TryGet(instance.CardNo, out var spec) && spec.PrintedValue > 0)
                contributions.Add(new(new(unit.ObjectId, unit.ObjectGeneration), instance.CardNo,
                    instance.Origin?.FaceRevision ?? unit.CopyFaceTransitions?.Count ?? 0,
                    unit.TokenFactoryCardNo, instance.Origin, false, spec.PrintedValue));
        foreach (var giver in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (!IsFaceUpNonStandbyUnit(giver) || !IsObjectOnField(state.PlayerZones, giver.ObjectId)
                || EffectiveFieldControllerId(state.PlayerZones, giver.ObjectId, giver) != controller) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, giver))
                if (DisarmSourceSpecifications.TryGet(instance.CardNo, out var spec))
                    foreach (var grant in spec.FriendlyGrants.Where(g => unit.Tags.Contains(g.RequiredUnitTag)))
                        contributions.Add(new(new(giver.ObjectId, giver.ObjectGeneration), instance.CardNo,
                            instance.Origin?.FaceRevision ?? giver.CopyFaceTransitions?.Count ?? 0,
                            giver.TokenFactoryCardNo, instance.Origin, true, grant.Value));
        }
        return contributions;
    }

    private static bool IsDisarmTag(string tag)
        => tag == "缴械" || tag.StartsWith("缴械", StringComparison.Ordinal) && int.TryParse(tag[2..], out _);

    private static MatchState RecomputeDisarmKeywords(MatchState state)
    {
        // The characteristic is inspectable even before its attack trigger exists (CN 831.3).
        if (!state.CardObjects.Values.Any(c => DisarmSourceSpecifications.TryGet(AttachedRuleTextRules.Face(c) ?? "", out _)
            || c.AttachedRuleText is { } copy && DisarmSourceSpecifications.TryGet(copy.CardNo, out _)
            || c.RuleDerivedTags?.Any(IsDisarmTag) == true)) return state;
        Dictionary<string, CardObjectState>? cards = null;
        foreach (var unit in state.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.UnitCard)))
        {
            var amount = !unit.IsFaceDown && IsObjectOnField(state.PlayerZones, unit.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) is { } controller
                ? DisarmContributions(state, unit, controller).Sum(c => c.Value) : 0;
            var printed = AttachedRuleTextRules.PrintedKeywords(AttachedRuleTextRules.Face(unit) ?? "").Where(IsDisarmTag);
            var tag = amount == 1 ? "缴械" : "缴械" + amount;
            var derived = (unit.RuleDerivedTags ?? []).Where(t => !IsDisarmTag(t))
                .Concat(amount > 0 ? [tag] : []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var tags = unit.Tags.Where(t => !IsDisarmTag(t))
                .Concat(amount > 0 ? [tag] : printed).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (tags.SequenceEqual(unit.Tags) && derived.SequenceEqual(unit.RuleDerivedTags ?? [])) continue;
            cards ??= state.CardObjects.ToDictionary();
            cards[unit.ObjectId] = unit with { Tags = tags, RuleDerivedTags = derived.Length == 0 ? null : derived };
        }
        return cards is null ? state : state with { CardObjects = cards };
    }

    private static TriggerQueueItemState[] CaptureDisarmTriggers(MatchState state, IEnumerable<string> attackers,
        string battlefield, string timing = TimingStates.NeutralClosed, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var unit) || !IsFaceUpNonStandbyUnit(unit)
                || EffectiveFieldControllerId(state.PlayerZones, id, unit) is not { } controller
                || AttachedRuleTextRules.Face(unit) is not { } face
                || !IsDisarmUnitFace(face)) continue;
            var contributions = DisarmContributions(state, unit, controller);
            if (contributions.Count == 0) continue;
            var origin = new DisarmOrigin(state.Tick, unit.CopyFaceTransitions?.Count ?? 0, contributions);
            var context = new FieldTriggerContext(face, DisarmKind, unit.ObjectGeneration, battlefield, returnFocusPlayer)
            {
                SourceLocation = new(controller, "BATTLEFIELD", battlefield), Disarm = origin,
                SourceTokenFactoryCardNo = unit.TokenFactoryCardNo
            };
            queue.Add(new($"disarm-{state.Tick + 1}-{id}", controller, id, FieldTriggerEffect, "BATTLE_DECLARED", timing)
                { FieldContext = context, SourceCardNo = face });
        }
        return queue.ToArray();
    }

    private static bool ValidDisarmShape(FieldTriggerContext c, string effect, string? cardNo)
        => effect == FieldTriggerEffect && c.Kind == DisarmKind && c.SourceGeneration >= 0
            && (cardNo is null || cardNo == c.CardNo) && IsDisarmUnitFace(c.CardNo)
            && c.Disarm is { CapturedTick: >= 0, FaceRevision: >= 0, Contributions.Count: > 0 } d
            && d.Contributions.All(g => g is { Source.Generation: >= 0, FaceRevision: >= 0, Value: > 0 }
                && !string.IsNullOrWhiteSpace(g.Source.ObjectId)
                && CopyCharacteristics.HasFace(g.CardNo)
                && (g.TokenFactoryCardNo is null || P6TokenFactoryCatalog.IsTokenFactory(g.TokenFactoryCardNo))
                && (g.CeasedSource is null || g.CeasedSource is { } ceased
                    && g.TokenFactoryCardNo is not null && ceased.FactoryCardNo == g.TokenFactoryCardNo
                    && ceased.Source == g.Source && ceased.CardNo == g.CardNo && ceased.FaceRevision == g.FaceRevision
                    && ceased.CapturedTick == (g.Origin?.CapturedTick ?? d.CapturedTick)
                    && ceased.Tick >= d.CapturedTick)
                && (g.Origin is null || g.Origin.CardNo == g.CardNo && g.Origin.Unit == g.Source
                    && g.Origin.FaceRevision == g.FaceRevision && g.Origin.CapturedTick <= d.CapturedTick)
                && DisarmSourceSpecifications.TryGet(g.CardNo, out var spec)
                && (g.FriendlyGrant ? TryPredictEnteredFace(c.CardNo, out var entered)
                    && spec.FriendlyGrants.Any(grant => grant.Value == g.Value && entered.Tags.Contains(grant.RequiredUnitTag))
                    : spec.PrintedValue == g.Value))
            && d.Contributions.Sum(g => (long)g.Value) <= int.MaxValue
            && d.Contributions.Select(MatchStateHasher.HashValue).Distinct(StringComparer.Ordinal).Count() == d.Contributions.Count
            && c.SourceLocation is not null && !string.IsNullOrWhiteSpace(c.BattlefieldId)
            && c.Origin is null && c.PaidUnitTrigger is null && c.StunObserver is null && c.SelfAction is null
            && c.AttackMightDamage is null && c.RecycledBoon is null && c.EmpoweredTransition is null
            && c.AttackConditionFrame is null && c.AffectedUnit is null && !c.AffectedUnitCeasedToExist
            && (c.SourceTokenFactoryCardNo is null ? !c.SourceCeasedToExist
                : P6TokenFactoryCatalog.IsTokenFactory(c.SourceTokenFactoryCardNo));

    private static bool ValidDisarmSource(MatchState state, FieldTriggerContext c, string source, string controller)
    {
        var d = c.Disarm!;
        if (!state.Seats.ContainsKey(controller) || d.CapturedTick > state.Tick
            || c.ReturnFocusPlayerId is not null && !state.Seats.ContainsKey(c.ReturnFocusPlayerId)
            || BattlefieldLocalRules.Battlefield(state, c.BattlefieldId) is null
            || c.SourceLocation is not { } location || !state.Seats.ContainsKey(location.PlayerId)
            || !(location.Zone == "BASE" && location.BattlefieldObjectId is null
                || location.Zone == "BATTLEFIELD" && BattlefieldLocalRules.Battlefield(state, location.BattlefieldObjectId) is not null)
            || !ValidCapturedFieldSource(state, c, source)
            || !PredictBindingMatches(state, new(source, c.SourceGeneration), c.CardNo, d.FaceRevision, d.CapturedTick, c.SourceTokenFactoryCardNo)
            || CombatMightSourceLocation(state, source, c.SourceGeneration) is { } current && current != location)
            return false;
        foreach (var g in d.Contributions)
        {
            if (!g.FriendlyGrant && (g.Source.ObjectId != source || g.Source.Generation != c.SourceGeneration)) return false;
            if (state.CardObjects.ContainsKey(g.Source.ObjectId)
                ? g.CeasedSource is not null
                : g.CeasedSource is not { } ceased || ceased.Tick > state.Tick) return false;
            if (g.Origin is { } copy
                ? !AttachedRuleTextRules.ValidCaptured(state, copy, g.Source.ObjectId, g.Source.Generation, g.CardNo)
                : !PredictBindingMatches(state, g.Source, g.CardNo, g.FaceRevision, d.CapturedTick, g.TokenFactoryCardNo)) return false;
        }
        return true;
    }

    private static bool IsDisarmUnitFace(string face)
        => TryPredictEnteredFace(face, out var card) && card.Tags.Contains(CardObjectTags.UnitCard);

    private static FieldTriggerContext? FinalizeDisarmContributionDepartures(MatchState before, MatchState after,
        IReadOnlySet<string> departed, FieldTriggerContext? context)
    {
        if (context is not { Kind: DisarmKind, Disarm: { } origin }) return context;
        var contributions = origin.Contributions.Select(contribution =>
        {
            var binding = contribution.Source;
            if (!departed.Contains(binding.ObjectId)
                || !before.CardObjects.TryGetValue(binding.ObjectId, out var card)
                || card.ObjectGeneration != binding.Generation
                || contribution.TokenFactoryCardNo is not { } factory || factory != card.TokenFactoryCardNo)
                return contribution;
            var tick = contribution.Origin?.CapturedTick ?? origin.CapturedTick;
            if (CopyFaceTimeline.FaceAt(card, contribution.FaceRevision, tick) != contribution.CardNo) return contribution;
            return contribution with { CeasedSource = new(binding, factory, after.Tick, contribution.CardNo,
                contribution.FaceRevision, tick) };
        }).ToArray();
        return context with { Disarm = origin with { Contributions = contributions } };
    }

    internal static bool ValidDisarmTrigger(TriggerQueueItemState t)
        => t.FieldContext?.Kind != DisarmKind || t.SourceCardNo == t.FieldContext.CardNo
            && t.TriggeredByEventKind is "BATTLE_DECLARED" or "BATTLE_ROLES_ESTABLISHED" or "BATTLE_ROLE_ACQUIRED";

    internal static bool ValidDisarmStack(StackItemState item)
        => item.FieldContext?.Kind != DisarmKind || item.OptionalCosts.Count == 0 && item.DamageAmount == 0
            && item.EffectRepeatCount == 1 && item.Destination.Length == 0
            && (item.TargetGenerations is not null || item.TargetObjectIds.Count == 0);

    private static StackResolutionResult ResolveDisarm(MatchState state, StackItemState item)
    {
        var context = item.FieldContext!;
        if (!ValidFieldContextState(state, context, item.SourceObjectId, item.ControllerId, item.EffectKind, item.CardNo))
            throw new InvalidOperationException("Invalid captured Disarm skill.");
        var result = NoopStackResolutionResult(state);
        result = result with { Events = [new("TRIGGER_RESOLVED", "缴械触发结算完成",
            new Dictionary<string, object?> { ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, "__spectator__") })] };
        if (item.TargetObjectIds.SingleOrDefault() is not { } target
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects.GetValueOrDefault(target)?.ObjectGeneration
            || !FieldTriggerTargets(state, item).Contains(target)) return result;
        var amount = checked(-context.Disarm!.Contributions.Sum(g => g.Value));
        var behavior = new CardBehaviorDefinition(item.CardNo, "缴械", 0, item.EffectKind, 0, 1,
            TargetScope: CardTargetScopes.EnemyUnit, PowerModifierAmount: amount);
        var cards = state.CardObjects.ToDictionary();
        cards[target] = ApplyPowerModifier(state, cards[target], behavior, item, target, amount, out var ev);
        return result with { CardObjects = cards, Events = result.Events.Prepend(ev).ToArray() };
    }
}
