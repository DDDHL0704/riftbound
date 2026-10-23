using System.Text.Json.Serialization;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChosenActionReplacementStep(string Kind, EmpoweredRuleContributor Rule,
    long? BeforeDelta, long AfterDelta);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChosenActionReplacementContext(string OriginalKind, long AppliedTick,
    string StackItemId, int Occurrence, string EffectControllerId, string SourceObjectId,
    string SourceCardNo, ObjectBinding Target, int? RequestedDelta, int? NormalizedDelta,
    int MinimumMight, int? SnapshotMight, IReadOnlyList<ChosenActionReplacementStep> Steps);

internal static class EmpoweredTargetReplacementRules
{
    internal const string Stun = "STUN", NegativeMight = "NEGATIVE_MIGHT", ReturnToHand = "RETURN_TO_HAND";
    internal const string TargetRule = "TARGET_ACTION_TO_MIGHT", CasterRule = "CASTER_ADDITIONAL_NEGATIVE_MIGHT";
    internal const string Effect = "CHOSEN_ACTION_REPLACEMENT_MIGHT";
    internal const string SourcePath = "CoreRuleEngine.ApplyChosenActionReplacement";
    private static readonly string[] Kinds = [Stun, NegativeMight, ReturnToHand];
    private static readonly Lazy<IReadOnlyDictionary<string, EmpoweredTargetReplacementSpec?>> Profiles = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo,
            c => EmpowerParser.TargetReplacements(c.CardEffect).SingleOrDefault(), StringComparer.Ordinal));

    internal static EmpoweredTargetReplacementSpec? Profile(string cardNo)
        => Profiles.Value.GetValueOrDefault(cardNo);

    internal static IEnumerable<(RuleTextInstance Instance, EmpoweredTargetReplacementSpec Spec)> Instances(
        MatchState state, CardObjectState target, string kind)
        => !target.IsEmpowered || target.IsFaceDown || !target.Tags.Contains(CardObjectTags.UnitCard)
            || !AttachedRuleTextRules.Field(state, target.ObjectId) ? []
            : AttachedRuleTextRules.Instances(state, target).Select(i => (Instance: i, Spec: Profile(i.CardNo)))
                .Where(p => p.Spec is not null && p.Spec.ActionKinds.Contains(kind, StringComparer.Ordinal))
                .Select(p => (p.Instance, p.Spec!));

    internal static string EffectId(ChosenActionReplacementContext context, int order)
        => $"TARGETED-ACTION:{context.AppliedTick}:{context.Occurrence}:{context.Target.ObjectId}:{context.Target.Generation}:{order}";

    internal static string InstanceKey(ChosenActionReplacementStep step) => step.Kind + ":" + step.Rule.InstanceId;

    // The action floor belongs to the original instruction. Its raw requested
    // amount can change under a caster replacement; normalize each virtual step.
    internal static long Normalize(long requested, int minimum, int? snapshot)
        => minimum > 0 ? Math.Max(requested, -Math.Max(0L, (long)snapshot!.Value - minimum)) : requested;

    internal static bool Valid(MatchState state, CardObjectState target, PowerModifierLedgerEntry modifier)
    {
        if (modifier is null) return false;
        if (modifier.ChosenActionReplacement is not { } context) return modifier.EffectKind != Effect;
        if (modifier.EffectKind != Effect || modifier.SourcePath != SourcePath || modifier.ChosenNegativeMight is not null
            || modifier.Duration != "UNTIL_END_OF_TURN" || modifier.MinimumPower != 0 || modifier.SnapshotMight is not null
            || modifier.AppliedOrder is not > 0 || context.Occurrence < 0 || context.AppliedTick < 1 || context.AppliedTick > state.Tick
            || string.IsNullOrWhiteSpace(context.StackItemId) || string.IsNullOrWhiteSpace(context.SourceObjectId)
            || string.IsNullOrWhiteSpace(context.SourceCardNo) || string.IsNullOrWhiteSpace(context.EffectControllerId)
            || !state.Seats.ContainsKey(context.EffectControllerId)
            || context.Target is null || context.Target.ObjectId != target.ObjectId || context.Target.Generation != target.ObjectGeneration
            || !Kinds.Contains(context.OriginalKind, StringComparer.Ordinal) || context.MinimumMight < 0
            || context.Steps is not { Count: > 0 } || context.Steps.Any(s => s is null || s.Rule is null)
            || context.Steps.Select(InstanceKey).Distinct(StringComparer.Ordinal).Count() != context.Steps.Count
            || modifier.EffectId != EffectId(context, modifier.AppliedOrder.Value)) return false;
        long? current = null; long raw = 0;
        if (context.OriginalKind == NegativeMight)
        {
            if (context.RequestedDelta is not < 0 || context.NormalizedDelta is not < 0
                || context.MinimumMight > 0 && context.SnapshotMight is null
                || context.MinimumMight == 0 && context.SnapshotMight is not null) return false;
            raw = context.RequestedDelta.Value;
            current = Normalize(raw, context.MinimumMight, context.SnapshotMight);
            if (current != context.NormalizedDelta) return false;
        }
        else if (context.RequestedDelta is not null || context.NormalizedDelta is not null
            || context.MinimumMight != 0 || context.SnapshotMight is not null) return false;

        for (var index = 0; index < context.Steps.Count; index++)
        {
            var step = context.Steps[index]; var rule = step.Rule;
            if (step.BeforeDelta != current || !ValidContributor(state, rule, context.AppliedTick)) return false;
            if (step.Kind == CasterRule)
            {
                var bonus = EmpoweredSpellRules.Bonus(rule.CardNo);
                if (context.OriginalKind != NegativeMight || current is not < 0 || bonus <= 0
                    || rule.ControllerId != context.EffectControllerId || index == context.Steps.Count - 1) return false;
                raw -= bonus;
                current = Normalize(raw, context.MinimumMight, context.SnapshotMight);
                if (step.AfterDelta != current) return false;
            }
            else if (step.Kind == TargetRule)
            {
                var profile = Profile(rule.CardNo);
                if (index != context.Steps.Count - 1 || rule.Source != context.Target || profile is null
                    || profile.MightBonus != 3 || profile.ActionKinds.Count != 3
                    || !Kinds.ToHashSet(StringComparer.Ordinal).SetEquals(profile.ActionKinds)
                    || !profile.ActionKinds.Contains(context.OriginalKind, StringComparer.Ordinal)
                    || modifier.SourceObjectId != rule.Source.ObjectId || modifier.SourceCardNo != rule.CardNo
                    || step.AfterDelta != profile.MightBonus || modifier.PowerDelta != profile.MightBonus
                    || modifier.RequestedPowerDelta != profile.MightBonus) return false;
                current = step.AfterDelta;
            }
            else return false;
        }
        return context.Steps[^1].Kind == TargetRule && current == modifier.PowerDelta;
    }

    private static bool ValidContributor(MatchState state, EmpoweredRuleContributor source, long tick)
    {
        if (source.Source is null || source.Source.Generation < 0 || string.IsNullOrWhiteSpace(source.Source.ObjectId)
            || !source.WasEmpowered || string.IsNullOrWhiteSpace(source.ControllerId)
            || !state.Seats.ContainsKey(source.ControllerId) || source.FaceRevision < 0
            || string.IsNullOrWhiteSpace(source.CardNo) || string.IsNullOrWhiteSpace(source.InstanceId)
            || source.Origin is null && source.InstanceId != $"PRINTED:{source.Source.ObjectId}:{source.Source.Generation}"
            || source.Origin is not null && (source.Origin.Equipment is null
                || source.InstanceId != $"ATTACHED:{source.Origin.Equipment.ObjectId}:{source.Origin.Equipment.Generation}:{source.Origin.Revision}")
            || !AttachedRuleTextRules.ValidCaptured(state, source.Origin, source.Source.ObjectId, source.Source.Generation, source.CardNo)) return false;
        return !state.CardObjects.TryGetValue(source.Source.ObjectId, out var live)
            || source.Source.Generation <= live.ObjectGeneration
                && (source.Source.Generation != live.ObjectGeneration || source.Origin is not null
                    || CopyFaceTimeline.FaceAt(live, source.FaceRevision, tick) == source.CardNo);
    }
}

internal sealed class TargetedActionNumericLimitException(string operation) : Exception(operation);
