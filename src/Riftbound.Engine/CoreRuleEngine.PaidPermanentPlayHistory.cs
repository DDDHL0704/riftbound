namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal const string PaidPermanentPlayCaptured = "PAID_PERMANENT_PLAY_SKILL_CAPTURED";
    internal const string PaidPermanentPlayRetired = "PAID_PERMANENT_PLAY_SKILL_RETIRED";
    internal const string PaidPermanentPlaySkillKey = "paidPlaySkillHash";
    internal const string PaidPermanentPlayChoicesKey = "paidPlayChoicesHash";

    private static bool IsPaidPermanentPlaySkill(StackItemState item)
        => item.SourceConfirmed && !string.IsNullOrWhiteSpace(item.EffectKind)
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && behavior.CardNo == item.CardNo
            && (behavior.PlaysSourceToBaseAsUnit || behavior.PlaysSourceToBaseAsEquipment)
            && HasTargetEffectAdditionalCost(behavior);

    internal static bool ValidPaidPermanentPlayChoices(StackItemState item)
        => item.OptionalCosts is not null && IsPaidPermanentPlaySkill(item)
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && TargetEffectAdditionalCostPaid(behavior, item.OptionalCosts)
            && TryBuildConfirmedPermanentTargetAdditionalCost(item, behavior, out _, out _);

    internal static IEnumerable<StackItemState> PaidPermanentPlaySkills(MatchState state)
    {
        foreach (var frame in new[] { state, state.PendingRuleChoice?.Origin }.OfType<MatchState>())
            foreach (var item in (frame.StackItems ?? []).OfType<StackItemState>().Concat(
                frame.PendingEffectPlay?.Parent is { } parent ? [parent] : []))
                if (IsPaidPermanentPlaySkill(item)) yield return item;
    }

    internal static string PaidPermanentPlayChoicesHash(StackItemState item)
        => MatchStateHasher.HashValue(new { item.StackItemId, item.SourceObjectId, item.ControllerId,
            item.CardNo, item.EffectKind, item.OptionalCosts, item.PermanentPlaySource });

    // Run once after every command's complete continuation. Selection of a
    // target or payment of Ward does not change the original play declaration.
    // A skill in a suspended parent/origin remains alive throughout that pause.
    private static ResolutionResult RecordPaidPermanentPlayHistory(MatchState before, ResolutionResult result)
    {
        var prior = PaidPermanentPlaySkills(before).DistinctBy(item => item.StackItemId)
            .ToDictionary(item => item.StackItemId, StringComparer.Ordinal);
        var current = PaidPermanentPlaySkills(result.State).DistinctBy(item => item.StackItemId)
            .ToDictionary(item => item.StackItemId, StringComparer.Ordinal);
        if (prior.Count == 0 && current.Count == 0) return result;
        var events = result.Events.ToList();
        void Record(string kind, StackItemState item)
        {
            var key = MatchStateHasher.HashValue(item.StackItemId);
            // A resumed command can already contain a nested completion's
            // events. Preserve that original receipt instead of duplicating it.
            if (events.Any(ev => ev.Kind == kind && Equals(ev.Payload.GetValueOrDefault(PaidPermanentPlaySkillKey), key))) return;
            var payload = new Dictionary<string, object?> { [PaidPermanentPlaySkillKey] = key };
            if (kind == PaidPermanentPlayCaptured) payload[PaidPermanentPlayChoicesKey] = PaidPermanentPlayChoicesHash(item);
            events.Add(new(kind, kind == PaidPermanentPlayCaptured ? "出场技能触发" : "出场技能结束", payload));
        }
        foreach (var item in current.Values.Where(item => !prior.ContainsKey(item.StackItemId)))
            Record(PaidPermanentPlayCaptured, item);
        foreach (var item in prior.Values.Where(item => !current.ContainsKey(item.StackItemId)))
            Record(PaidPermanentPlayRetired, item);
        return events.Count == result.Events.Count ? result : result with { Events = events };
    }
}
