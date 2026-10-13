using System.Text.Json.Serialization;

namespace Riftbound.Engine;

public sealed record EmpoweredRuleContributor(ObjectBinding Source, string CardNo, string InstanceId,
    string ControllerId, long FaceRevision, bool WasEmpowered,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RuleTextInstanceBinding? Origin = null);
public sealed record ChosenNegativeMightContext(int OriginalDelta, long AppliedTick, string ControllerId,
    string StackItemId, ObjectBinding Target, IReadOnlyList<EmpoweredRuleContributor> Contributors);

internal static class EmpoweredSpellRules
{
    internal static IEnumerable<(CardObjectState Source, RuleTextInstance Instance)> Sources(MatchState state, string actor)
        => state.CardObjects.Values.Where(c => c.IsEmpowered && c.ControllerId == actor && !c.IsFaceDown
            && !c.Tags.Contains(CardObjectTags.Standby)
            && (AttachedRuleTextRules.Field(state, c.ObjectId) || state.PlayerZones.Values.Any(z => z.LegendZone.Contains(c.ObjectId))))
            .OrderBy(c => c.ObjectId, StringComparer.Ordinal).SelectMany(c => AttachedRuleTextRules.Instances(state, c).Select(i => (c, i)));

    internal static bool PreventsCounter(MatchState state, string actor)
        => Sources(state, actor).Any(i => EmpowerRules.SpellEffects(i.Instance.CardNo).Any(p => p.PreventsCounter));

    internal static ChosenNegativeMightContext? Replace(MatchState? state, StackItemState item, CardObjectState target, int delta)
    {
        if (state is null || delta >= 0 || !target.Tags.Contains(CardObjectTags.UnitCard)
            || !StackObjectRules.ReferencesCurrentObject(state, item, target.ObjectId)
            || !StackObjectRules.IsSpell(item) && !StackObjectRules.IsAbility(item)) return null;
        var sources = Sources(state, item.ControllerId).Where(i => Bonus(i.Instance.CardNo) > 0).ToArray();
        if (sources.Length == 0) return null;
        return new(delta, state.Tick + 1, item.ControllerId, item.StackItemId, new(target.ObjectId, target.ObjectGeneration),
            sources.Select(i => new EmpoweredRuleContributor(new(i.Source.ObjectId, i.Source.ObjectGeneration), i.Instance.CardNo,
                i.Instance.Id, i.Source.ControllerId!, i.Source.CopyFaceTransitions?.Count ?? 0, i.Source.IsEmpowered, i.Instance.Origin)).ToArray());
    }
    internal static int Bonus(string cardNo) => EmpowerRules.SpellEffects(cardNo).Sum(p => p.AdditionalChosenNegativeMight);
    internal static int Delta(ChosenNegativeMightContext context) => context.OriginalDelta - context.Contributors.Sum(c => Bonus(c.CardNo));

    internal static bool Valid(MatchState state, CardObjectState target, PowerModifierLedgerEntry modifier)
    {
        if (modifier?.ChosenNegativeMight is not { } c) return true;
        if (c.OriginalDelta >= 0 || modifier.RequestedPowerDelta >= 0 || c.AppliedTick < 0 || c.AppliedTick > state.Tick
            || string.IsNullOrWhiteSpace(c.ControllerId) || !state.Seats.ContainsKey(c.ControllerId)
            || string.IsNullOrWhiteSpace(c.StackItemId) || c.Target is null || c.Target.ObjectId != target.ObjectId
            || c.Target.Generation != target.ObjectGeneration || c.Contributors is not { Count: > 0 }
            || c.Contributors.Any(p => p is null || p.Source is null || string.IsNullOrWhiteSpace(p.Source.ObjectId)
                || string.IsNullOrWhiteSpace(p.CardNo) || string.IsNullOrWhiteSpace(p.InstanceId))
            || c.Contributors.Select(p => p.InstanceId).Distinct().Count() != c.Contributors.Count
            || modifier.RequestedPowerDelta != Delta(c)) return false;
        foreach (var source in c.Contributors) {
            if (source.Source is null || source.Source.Generation < 0 || !source.WasEmpowered || source.ControllerId != c.ControllerId
                || source.FaceRevision < 0 || Bonus(source.CardNo) <= 0
                || source.Origin is null && source.InstanceId != $"PRINTED:{source.Source.ObjectId}:{source.Source.Generation}"
                || source.Origin is not null && (source.Origin.Equipment is null
                    || source.InstanceId != $"ATTACHED:{source.Origin.Equipment.ObjectId}:{source.Origin.Equipment.Generation}:{source.Origin.Revision}")
                || !AttachedRuleTextRules.ValidCaptured(state, source.Origin, source.Source.ObjectId, source.Source.Generation, source.CardNo)) return false;
            if (state.CardObjects.TryGetValue(source.Source.ObjectId, out var live)
                && (source.Source.Generation > live.ObjectGeneration || source.Origin is null && source.Source.Generation == live.ObjectGeneration
                    && CopyFaceTimeline.FaceAt(live, source.FaceRevision, c.AppliedTick) != source.CardNo)) return false;
        }
        return true;
    }
}
