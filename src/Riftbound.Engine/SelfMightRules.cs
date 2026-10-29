using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SelfMightActivationContext(ObjectBinding Source, string CardNo, int AbilityIndex,
    long AcquiredTick, long FaceRevision, string InstanceId, RuleTextInstanceBinding? Origin = null);

internal sealed record SelfMightAbility(string Id, RuleTextInstance Instance, int Index, ActivatedAbilitySpec Spec)
{
    internal string? InstanceId => Instance.Origin is null ? null : $"{Instance.Id}:{Id}";
}

internal static class SelfMightRules
{
    internal const string Effect = "MANA_GRANT_SELF_MIGHT";
    private static readonly Lazy<IReadOnlyDictionary<string, ActivatedAbilitySpec[]>> SpecsByCard = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo,
            c => ActivatedAbilityParser.Parse(c.CardEffect).Where(a => a.Kind == ActivatedAbilityKinds.ManaGrantSelfMight).ToArray()));
    internal static IReadOnlyList<ActivatedAbilitySpec> Specs(string no) => SpecsByCard.Value.GetValueOrDefault(no) ?? [];
    internal static IEnumerable<SelfMightAbility> Abilities(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).SelectMany(instance => Specs(instance.CardNo)
            .Select((spec, index) => new SelfMightAbility($"SELF-MIGHT:{index}", instance, index, spec)));
    internal static bool CanActivate(MatchState state, string actor, CardObjectState source)
        => CoreRuleEngine.CanActivateOrdinaryPowerAbility(state, actor) && source.ControllerId == actor
            && !source.IsFaceDown && source.Tags.Contains(CardObjectTags.UnitCard)
            && state.PlayerZones.TryGetValue(actor, out var zones)
            && (zones.Base.Contains(source.ObjectId) || zones.Battlefields.Contains(source.ObjectId));

    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (item.SelfMightActivation is not { } c) return item.EffectKind != Effect;
        var specs = Specs(c.CardNo);
        if (item.EffectKind != Effect || item.CardNo != c.CardNo || c.AbilityIndex < 0 || c.AbilityIndex >= specs.Count
            || c.Source is not { Generation: >= 0 } || c.Source.ObjectId != item.SourceObjectId
            || c.AcquiredTick <= 0 || c.AcquiredTick > state.Tick || c.FaceRevision < 0
            || item.StackItemId != $"STACK-{c.AcquiredTick}-{c.Source.ObjectId}-SELF-MIGHT"
            || item.TargetObjectIds.Count != 0 || item.TargetGenerations is not null || item.OptionalCosts.Count != 0
            || item.EffectRepeatCount != 1 || item.SourceConfirmed || item.DamageAmount != 0
            || !state.Seats.ContainsKey(item.ControllerId)
            || state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) && source.ObjectGeneration < c.Source.Generation
            || !AttachedRuleTextRules.ValidCaptured(state, c.Origin, item.SourceObjectId, c.Source.Generation, c.CardNo)
            || item.EmpowerActivation is not null || item.TargetedPowerAbility is not null || item.SpellContext is not null
            || item.FieldContext is not null || item.PlayCost is not null) return false;
        return c.Origin is { } origin
            ? c.InstanceId == $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : c.InstanceId == $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}"
                && (!state.CardObjects.TryGetValue(c.Source.ObjectId, out var live) || live.ObjectGeneration != c.Source.Generation
                    || CopyFaceTimeline.FaceAt(live, c.FaceRevision, c.AcquiredTick) == c.CardNo);
    }
}

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult ResolveSelfMightActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || !SelfMightRules.CanActivate(state, intent.PlayerId, source))
            return RejectWithCorePrompts(state, "请在你的开放主阶段选择受控的公开单位。", ErrorCodes.InvalidTarget);
        var ability = SelfMightRules.Abilities(state, source).FirstOrDefault(a => a.Id == command.AbilityId
            && (a.InstanceId is null ? string.IsNullOrEmpty(command.AbilityInstanceId) : a.InstanceId == command.AbilityInstanceId));
        var costs = NormalizeOptionalCosts(command.OptionalCosts);
        if (ability is null || command.TargetObjectIds.Count != 0 || costs.Count != (command.OptionalCosts?.Count ?? 0)
            || !TryExtractRenektonPlayPayment(costs, out var withoutLegend, out var legendAction)
            || !TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, withoutLegend, out var extra, out var resources, out var recycled)
            || extra.Count != 0)
            return RejectWithCorePrompts(state, "请选择当前存在的技能与支付资源；此技能没有目标。", ErrorCodes.InvalidTarget);
        var ordered = legendAction is null ? resources : OrderedRenektonPaymentResources(costs, resources, legendAction);
        var mana = ability.Spec.ManaCost!.Value;
        var plan = new PaymentCostRules.PaymentPlan(PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY",
            intent.PlayerId, source.ObjectId, ability.Id), "ACTIVATE_ABILITY", intent.PlayerId, mana, mana, 0, 0,
            paymentResourceActionIds: ordered, reason: SelfMightRules.Effect, sourceObjectId: source.ObjectId, abilityId: ability.Id,
            manaPurpose: ManaPurposeForActivatedAbility(state, source.ObjectId), manaSourceSelection: command.ServerManaSourceSelection);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones); var events = new List<GameEvent>();
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (legendAction is not null) {
            if (!TryApplyOrderedRenektonResources(state, intent.PlayerId, ordered, zones, ref cards, locations, ref pools,
                    events, "ACTIVATE_ABILITY", plan.PaymentId))
                return RejectWithCorePrompts(state, "支付资源行动顺序或来源无效。", ErrorCodes.InvalidTarget);
        } else pools = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, locations, intent.PlayerId,
            recycled, events, "ACTIVATE_ABILITY", plan.PaymentId);
        var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!payment.Accepted) return RejectWithCorePrompts(state, "资源不足，无法支付技能费用。", ErrorCodes.InsufficientCost);
        var context = new SelfMightActivationContext(new(source.ObjectId, source.ObjectGeneration), ability.Instance.CardNo,
            ability.Index, state.Tick + 1, source.CopyFaceTransitions?.Count ?? 0, ability.Instance.Id, ability.Instance.Origin);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-SELF-MIGHT", intent.PlayerId,
            source.ObjectId, SelfMightRules.Effect, ability.Instance.CardNo, [], 0, 1, []) { SelfMightActivation = context };
        events.Add(new("COST_PAID", "支付战力技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools,
            payment.PlayerExperience, new Dictionary<string, object?> { ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id })));
        events.Add(new("ABILITY_ACTIVATED", "激活本回合战力技能", new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id, ["abilityInstanceId"] = command.AbilityInstanceId,
            ["effectKind"] = SelfMightRules.Effect, ["targetObjectIds"] = Array.Empty<string>() }));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = source.ObjectId, ["effectKind"] = item.EffectKind }));
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = payment.RunePools,
            PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item],
            TimingState = TimingStates.NeutralClosed, ActivePlayerId = intent.PlayerId,
            PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveSelfMightStack(MatchState state, StackItemState item)
    {
        if (!SelfMightRules.Valid(state, item)) throw new InvalidOperationException("Invalid self Might activation.");
        var context = item.SelfMightActivation!;
        if (!state.CardObjects.TryGetValue(context.Source.ObjectId, out var source) || source.ObjectGeneration != context.Source.Generation
            || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state);
        var amount = SelfMightRules.Specs(context.CardNo)[context.AbilityIndex].PowerModifierAmount!.Value;
        var behavior = new CardBehaviorDefinition(context.CardNo, "本回合战力技能", 0, item.EffectKind, 0, 0, PowerModifierAmount: amount);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        cards[source.ObjectId] = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, amount, out var modified);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [modified] };
    }
}
