using Riftbound.Contracts;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record CeasedDisarmSource(ObjectBinding Source, string FactoryCardNo, long Tick,
    string CardNo, long FaceRevision, long CapturedTick);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record DisarmKeywordContribution(ObjectBinding Source, string CardNo, int Amount,
    RuleTextInstanceBinding? Origin = null, string? AuraKey = null)
{
    public long FaceRevision { get; init; } = -1;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceTokenFactoryCardNo { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CeasedDisarmSource? CeasedSource { get; init; }
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record DisarmTriggerContext(IReadOnlyList<DisarmKeywordContribution> Contributions, long CapturedTick = -1);

public sealed partial class CoreRuleEngine
{
    private const string DisarmKind = "DISARM";
    private static string DisarmAuraKey(StaticAuraSpec aura)
        => string.Join('|', aura.Kind, aura.TargetScope, aura.TargetFilter, aura.GrantedKeyword);
    private static string DisarmSourceGrantKey(DisarmFriendlyGrantSource grant)
        => "CN-SOURCE:" + grant.RequiredUnitTag + ":" + grant.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static int PrintedDisarmAmount(string cardNo)
        => DisarmSourceSpecifications.TryGet(cardNo, out var spec) ? spec.PrintedValue
            : CombatKeywordAmount(AttachedRuleTextRules.PrintedKeywords(cardNo), CardCombatKeywordNames.Disarm);
    private static string? DisarmTokenFactory(CardObjectState source)
        => IsTokenObject(source) ? source.TokenFactoryCardNo ?? source.CardNo : null;
    private static bool KnownDisarmTokenFactory(string factory)
        => P6TokenFactoryCatalog.TryGetByCardNo(factory, out var token) && token.Tags.Contains(CardObjectTags.UnitCard);
    private static DisarmKeywordContribution CaptureDisarmContribution(CardObjectState source, RuleTextInstance instance,
        int amount, string? auraKey = null)
        => new(new(source.ObjectId, source.ObjectGeneration), instance.CardNo, amount, instance.Origin, auraKey) {
            FaceRevision = instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0,
            SourceTokenFactoryCardNo = DisarmTokenFactory(source) };

    private static DisarmKeywordContribution[] DisarmContributions(MatchState state, CardObjectState unit)
    {
        var contributions = new List<DisarmKeywordContribution>();
        foreach (var instance in AttachedRuleTextRules.Instances(state, unit))
        {
            var amount = PrintedDisarmAmount(instance.CardNo);
            if (amount > 0) contributions.Add(CaptureDisarmContribution(unit, instance, amount));
        }
        // CN 831.2 adds each independent grant, including grants from two
        // different copies of the same card. Do not take a maximum or dedupe.
        foreach (var grant in CurrentPublicKeywordGrants(state, unit, CardCombatKeywordNames.Disarm))
        {
            if (DisarmSourceSpecifications.TryGet(grant.Instance.CardNo, out var spec) && spec.FriendlyGrants.Length > 0) continue;
            var source = state.CardObjects[grant.SourceId];
            contributions.Add(CaptureDisarmContribution(source, grant.Instance,
                GrantedCombatKeywordAmount(grant.Aura, CardCombatKeywordNames.Disarm), DisarmAuraKey(grant.Aura)));
        }
        foreach (var source in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal)
            .Where(c => IsFaceUpNonStandbyUnit(c) && IsObjectOnField(state.PlayerZones, c.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, c.ObjectId, c) == EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit)))
            foreach (var instance in AttachedRuleTextRules.Instances(state, source))
                if (DisarmSourceSpecifications.TryGet(instance.CardNo, out var spec))
                    foreach (var grant in spec.FriendlyGrants.Where(g => unit.Tags.Contains(g.RequiredUnitTag, StringComparer.Ordinal)))
                        contributions.Add(CaptureDisarmContribution(source, instance, grant.Value, DisarmSourceGrantKey(grant)));
        return contributions.ToArray();
    }

    private static TriggerQueueItemState[] CaptureDisarmTriggers(MatchState state, IEnumerable<string> attackers,
        string battlefield, string timing, string? returnFocusPlayer = null)
    {
        var queue = new List<TriggerQueueItemState>();
        foreach (var id in attackers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var unit) || !IsFaceUpNonStandbyUnit(unit)) continue;
            var contributions = DisarmContributions(state, unit);
            if (contributions.Length == 0) continue;
            var context = new FieldTriggerContext(AttachedRuleTextRules.Face(unit)!, DisarmKind,
                unit.ObjectGeneration, battlefield, returnFocusPlayer) {
                SourceLocation = new(unit.ControllerId!, "BATTLEFIELD", battlefield),
                SourceTokenFactoryCardNo = IsTokenObject(unit) ? unit.TokenFactoryCardNo ?? unit.CardNo : null,
                Disarm = new(contributions, RuleChoices.Value?.CompletionTick ?? state.Tick + 1) };
            queue.Add(new($"disarm-{state.Tick + 1}-{id}", unit.ControllerId!, id, FieldTriggerEffect,
                "BATTLE_ROLES_ESTABLISHED", timing) { FieldContext = context });
        }
        return queue.ToArray();
    }

    private static bool ValidDisarmShape(FieldTriggerContext context, string effect, string? cardNo)
    {
        if (effect != FieldTriggerEffect || context.SourceGeneration < 0 || cardNo is not null && cardNo != context.CardNo
            || string.IsNullOrWhiteSpace(context.CardNo) || string.IsNullOrWhiteSpace(context.BattlefieldId)
            || context.SourceLocation is null || context.Origin is not null || context.RecycledBoon is not null
            || context.EmpoweredTransition is not null || context.AffectedUnit is not null || context.AffectedUnitCeasedToExist
            || context.Disarm is not { CapturedTick: >= 0, Contributions.Count: > 0 } disarm
            || context.SourceCeasedToExist && context.SourceTokenFactoryCardNo is null
            || context.SourceTokenFactoryCardNo is { } factory && !KnownDisarmTokenFactory(factory)) return false;
        var contributions = disarm.Contributions;
        if (contributions.Any(c => c is null || c.Source is not { Generation: >= 0 }
            || string.IsNullOrWhiteSpace(c.Source.ObjectId) || string.IsNullOrWhiteSpace(c.CardNo) || c.Amount <= 0
            || c.FaceRevision < 0 || c.SourceTokenFactoryCardNo is { } tokenFactory && !KnownDisarmTokenFactory(tokenFactory)
            || c.Origin is { } origin && (origin.FaceRevision != c.FaceRevision
                || origin.CapturedTick is not >= 0 || origin.CapturedTick > disarm.CapturedTick)
            || c.CeasedSource is { } ceased && (ceased.Source != c.Source || ceased.FactoryCardNo != c.SourceTokenFactoryCardNo
                || ceased.CardNo != c.CardNo || ceased.FaceRevision != c.FaceRevision
                || ceased.CapturedTick != (c.Origin?.CapturedTick ?? disarm.CapturedTick)
                || ceased.Tick < disarm.CapturedTick || c.SourceTokenFactoryCardNo is null))
            || contributions.Sum(c => (long)c.Amount) > int.MaxValue) return false;
        return contributions.Select(c => (c.Source.ObjectId, c.Source.Generation, c.CardNo, c.AuraKey,
            c.Origin?.Equipment.ObjectId, c.Origin?.Equipment.Generation, c.Origin?.Revision)).Distinct().Count() == contributions.Count;
    }

    private static bool ValidDisarmContributions(MatchState state, FieldTriggerContext context, string unitId)
    {
        if (context.Disarm!.CapturedTick > state.Tick) return false;
        foreach (var contribution in context.Disarm!.Contributions)
        {
            if (contribution.SourceTokenFactoryCardNo is not null
                && !CopyCharacteristics.HasFace(contribution.CardNo)) return false;
            var binding = contribution.Source;
            state.CardObjects.TryGetValue(binding.ObjectId, out var source);
            if (source is null)
            {
                if (contribution.CeasedSource is not { } ceased || ceased.Tick > state.Tick
                    || contribution.SourceTokenFactoryCardNo is null || !KnownDisarmTokenFactory(contribution.SourceTokenFactoryCardNo)
                    || binding.ObjectId == unitId && (!context.SourceCeasedToExist
                        || binding.Generation != context.SourceGeneration
                        || contribution.SourceTokenFactoryCardNo != context.SourceTokenFactoryCardNo)) return false;
            }
            else if (contribution.CeasedSource is not null || source.ObjectGeneration < binding.Generation
                || DisarmTokenFactory(source) != contribution.SourceTokenFactoryCardNo
                || source.ObjectGeneration == binding.Generation && (!CopyFaceTimeline.Valid(state, source)
                    || CopyFaceTimeline.FaceAt(source, contribution.FaceRevision,
                        contribution.Origin?.CapturedTick ?? context.Disarm.CapturedTick) != contribution.CardNo)) return false;
            if (!AttachedRuleTextRules.ValidCaptured(state, contribution.Origin, binding.ObjectId, binding.Generation, contribution.CardNo)) return false;
            if (contribution.AuraKey is { } key)
            {
                var retainedSourceGrant = DisarmSourceSpecifications.TryGet(contribution.CardNo, out var spec)
                    && spec.FriendlyGrants.Any(g => DisarmSourceGrantKey(g) == key && g.Value == contribution.Amount);
                if (!retainedSourceGrant && !StaticAuraSpecRules.GetStaticAuras(contribution.CardNo).Any(a => DisarmAuraKey(a) == key
                    && GrantedCombatKeywordAmount(a, CardCombatKeywordNames.Disarm) == contribution.Amount)) return false;
            }
            else
            {
                if (binding.ObjectId != unitId || binding.Generation != context.SourceGeneration) return false;
                if (PrintedDisarmAmount(contribution.CardNo) != contribution.Amount) return false;
            }
        }
        return true;
    }

    private static FieldTriggerContext? FinalizeDisarmContributionDepartures(MatchState before, MatchState after,
        IReadOnlySet<string> departed, FieldTriggerContext? context)
    {
        if (context is not { Kind: DisarmKind, Disarm: { } disarm } || disarm.Contributions is null) return context;
        var contributions = disarm.Contributions.Select(contribution =>
        {
            if (contribution?.Source is not { } binding || !departed.Contains(binding.ObjectId)
                || !before.CardObjects.TryGetValue(binding.ObjectId, out var source)
                || source.ObjectGeneration != binding.Generation
                || contribution.SourceTokenFactoryCardNo is not { } factory || factory != DisarmTokenFactory(source)) return contribution!;
            var capturedTick = contribution.Origin?.CapturedTick ?? disarm.CapturedTick;
            var face = CopyFaceTimeline.FaceAt(source, contribution.FaceRevision, capturedTick);
            if (face != contribution.CardNo) return contribution;
            return contribution with { CeasedSource = new(binding, factory, after.Tick, face,
                contribution.FaceRevision, capturedTick) };
        }).ToArray();
        return context with { Disarm = disarm with { Contributions = contributions } };
    }

    private static StackResolutionResult ResolveDisarm(MatchState state, StackItemState item)
    {
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        var events = new List<GameEvent>();
        var target = item.TargetObjectIds.SingleOrDefault();
        if (target is not null && item.TargetGenerations?.GetValueOrDefault(target, -1) == cards.GetValueOrDefault(target)?.ObjectGeneration
            && FieldTriggerTargets(state, item).Contains(target, StringComparer.Ordinal))
        {
            var amount = checked(item.FieldContext!.Disarm!.Contributions.Sum(c => c.Amount));
            var behavior = new CardBehaviorDefinition(item.CardNo, "缴械", 0, item.EffectKind, 0, 1,
                TargetScope: CardTargetScopes.EnemyUnit, PowerModifierAmount: -amount);
            cards[target] = ApplyPowerModifier(state, cards[target], behavior, item, target, -amount, out var modified);
            events.Add(modified);
        }
        events.Add(new("TRIGGER_RESOLVED", "缴械触发结算完成", new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId }));
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
