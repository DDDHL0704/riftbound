using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NamedUnitSwapContext(ObjectBinding Source, string CardNo, int AbilityIndex,
    long AcquiredTick, long FaceRevision, string InstanceId, RuleTextInstanceBinding? Origin = null);

internal sealed record NamedUnitSwapAbility(string Id, RuleTextInstance Instance, int Index, ActivatedAbilitySpec Spec)
{
    internal string? InstanceId => Instance.Origin is null ? null : $"{Instance.Id}:{Id}";
}

internal static class NamedUnitSwapRules
{
    internal const string Effect = "SWIFT_NAMED_UNIT_SWAP";
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo));
    private static readonly Lazy<IReadOnlyDictionary<string, ActivatedAbilitySpec[]>> SpecsByCard = new(() =>
        Cards.Value.ToDictionary(c => c.Key, c => ActivatedAbilityParser.Parse(c.Value.CardEffect)
            .Where(a => a.Kind == ActivatedAbilityKinds.SwiftSwapSelfWithNamedControlledUnit).ToArray()));
    internal static IReadOnlyList<ActivatedAbilitySpec> Specs(string no) => SpecsByCard.Value.GetValueOrDefault(no) ?? [];
    internal static IEnumerable<NamedUnitSwapAbility> Abilities(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).SelectMany(instance => Specs(instance.CardNo)
            .Select((spec, index) => new NamedUnitSwapAbility($"NAMED-UNIT-SWAP:{index}", instance, index, spec)));
    internal static bool IsSource(MatchState state, string actor, CardObjectState source)
        => source.ControllerId == actor && !source.IsFaceDown && source.Tags.Contains(CardObjectTags.UnitCard)
            && AttachedRuleTextRules.Field(state, source.ObjectId);
    internal static bool CanActivate(MatchState state, string actor, CardObjectState source)
        => CoreRuleEngine.CanActivateSwiftAbility(state, actor) && IsSource(state, actor, source);
    internal static bool IsTarget(MatchState state, string actor, string source, string target, ActivatedAbilitySpec spec)
        => target != source && state.CardObjects.TryGetValue(target, out var unit) && IsSource(state, actor, unit)
            && Name(unit) == spec.TargetUnitName && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, target);
    private static string? Name(CardObjectState unit)
        => unit.CardNo is { } no && Cards.Value.TryGetValue(no, out var card) ? card.CardName
            : P6TokenFactoryCatalog.TryGetByCardNo(unit.CardNo ?? "", out var token) ? token.TokenFamilyName : null;

    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (item.NamedUnitSwap is not { } c) return item.EffectKind != Effect;
        var specs = Specs(c.CardNo);
        if (item.EffectKind != Effect || item.CardNo != c.CardNo || c.AbilityIndex < 0 || c.AbilityIndex >= specs.Count
            || c.Source is not { Generation: >= 0 } || c.Source.ObjectId != item.SourceObjectId
            || c.AcquiredTick <= 0 || c.AcquiredTick > state.Tick || c.FaceRevision < 0
            || item.StackItemId != $"STACK-{c.AcquiredTick}-{c.Source.ObjectId}-NAMED-UNIT-SWAP"
            || item.TargetObjectIds.Count != 1 || item.TargetObjectIds[0] == item.SourceObjectId
            || item.TargetGenerations is not { Count: 1 } generations
            || !generations.TryGetValue(item.TargetObjectIds[0], out var generation) || generation < 0
            || item.OptionalCosts.Count != 0 || item.EffectRepeatCount != 1 || item.SourceConfirmed || item.DamageAmount != 0
            || !state.Seats.ContainsKey(item.ControllerId)
            || state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) && source.ObjectGeneration < c.Source.Generation
            || state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && target.ObjectGeneration < generation
            || !AttachedRuleTextRules.ValidCaptured(state, c.Origin, item.SourceObjectId, c.Source.Generation, c.CardNo)
            || item.EmpowerActivation is not null || item.TargetedPowerAbility is not null || item.SpellContext is not null
            || item.FieldContext is not null || item.PlayCost is not null || item.AzirMovement is not null) return false;
        return c.Origin is { } origin
            ? c.InstanceId == $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : c.InstanceId == $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}"
                && (!state.CardObjects.TryGetValue(c.Source.ObjectId, out var live) || live.ObjectGeneration != c.Source.Generation
                    || CopyFaceTimeline.FaceAt(live, c.FaceRevision, c.AcquiredTick) == c.CardNo);
    }
}

public sealed partial class CoreRuleEngine
{
    private static ResolutionResult ResolveNamedUnitSwapActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || !NamedUnitSwapRules.CanActivate(state, intent.PlayerId, source))
            return RejectWithCorePrompts(state, "此迅捷技能需要受控的公开单位与行动权。", ErrorCodes.InvalidTarget);
        var ability = NamedUnitSwapRules.Abilities(state, source).FirstOrDefault(a => a.Id == command.AbilityId
            && (a.InstanceId is null ? string.IsNullOrEmpty(command.AbilityInstanceId) : a.InstanceId == command.AbilityInstanceId));
        var costs = NormalizeOptionalCosts(command.OptionalCosts);
        if (ability is null || command.TargetObjectIds.Count != 1
            || !NamedUnitSwapRules.IsTarget(state, intent.PlayerId, source.ObjectId, command.TargetObjectIds[0], ability.Spec)
            || costs.Count != (command.OptionalCosts?.Count ?? 0)
            || !TryExtractRenektonPlayPayment(costs, out var withoutLegend, out var legendAction)
            || !TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, withoutLegend, out var extra, out var resources, out var recycled)
            || extra.Count != 0)
            return RejectWithCorePrompts(state, "请选择当前存在的技能、受控同名单位与支付资源。", ErrorCodes.InvalidTarget);
        var ordered = legendAction is null ? resources : OrderedRenektonPaymentResources(costs, resources, legendAction);
        var spec = ability.Spec;
        var typed = new Dictionary<string, int> { [spec.PowerCostTrait!] = spec.PowerCost!.Value };
        var plan = new PaymentCostRules.PaymentPlan(PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY",
            intent.PlayerId, source.ObjectId, ability.Id), "ACTIVATE_ABILITY", intent.PlayerId,
            spec.ManaCost!.Value, spec.ManaCost.Value, 0, spec.PowerCost.Value, powerCostByTrait: typed,
            paymentResourceActionIds: ordered, reason: NamedUnitSwapRules.Effect, sourceObjectId: source.ObjectId, abilityId: ability.Id,
            manaPurpose: ManaPurposeForActivatedAbility(state, source.ObjectId), manaSourceSelection: command.ServerManaSourceSelection);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones); var events = new List<GameEvent>();
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (legendAction is not null) {
            if (!TryApplyOrderedPaymentResources(state, intent.PlayerId, ordered, zones, ref cards, locations, ref pools,
                    events, "ACTIVATE_ABILITY", plan.PaymentId))
                return RejectWithCorePrompts(state, "支付资源行动顺序或来源无效。", ErrorCodes.InvalidTarget);
        } else pools = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, locations, intent.PlayerId,
            recycled, events, "ACTIVATE_ABILITY", plan.PaymentId);
        var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!payment.Accepted) return RejectWithCorePrompts(state, "资源不足，无法支付迅捷移动技能。", ErrorCodes.InsufficientCost);
        var context = new NamedUnitSwapContext(new(source.ObjectId, source.ObjectGeneration), ability.Instance.CardNo,
            ability.Index, state.Tick + 1, source.CopyFaceTransitions?.Count ?? 0, ability.Instance.Id, ability.Instance.Origin);
        var target = command.TargetObjectIds[0];
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-NAMED-UNIT-SWAP", intent.PlayerId,
            source.ObjectId, NamedUnitSwapRules.Effect, ability.Instance.CardNo, [target], 0, 1, []) {
            NamedUnitSwap = context, TargetGenerations = new Dictionary<string, long> { [target] = state.CardObjects[target].ObjectGeneration } };
        events.Add(new("COST_PAID", "支付迅捷移动技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools,
            payment.PlayerExperience, new Dictionary<string, object?> { ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id })));
        events.Add(new("ABILITY_ACTIVATED", "激活迅捷移动技能", new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id, ["abilityInstanceId"] = command.AbilityInstanceId,
            ["effectKind"] = NamedUnitSwapRules.Effect, ["targetObjectIds"] = new[] { target }, ["swift"] = true }));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = source.ObjectId, ["effectKind"] = item.EffectKind }));
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = payment.RunePools,
            PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item],
            TimingState = state.TimingState == TimingStates.SpellDuelOpen ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed,
            ActivePlayerId = intent.PlayerId, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveNamedUnitSwap(MatchState state, StackItemState item)
    {
        if (!NamedUnitSwapRules.Valid(state, item)) throw new InvalidOperationException("Invalid named unit swap activation.");
        var c = item.NamedUnitSwap!; var target = item.TargetObjectIds[0];
        if (!state.CardObjects.TryGetValue(c.Source.ObjectId, out var source) || source.ObjectGeneration != c.Source.Generation
            || !NamedUnitSwapRules.IsSource(state, item.ControllerId, source)
            || !state.CardObjects.TryGetValue(target, out var unit) || unit.ObjectGeneration != item.TargetGenerations![target]
            || !NamedUnitSwapRules.IsTarget(state, item.ControllerId, source.ObjectId, target, NamedUnitSwapRules.Specs(c.CardNo)[c.AbilityIndex])
            || !TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, source.ObjectId, out var sourceOrigin)
            || !TryGetPreciseFieldLocation(state.PlayerZones, state.ObjectLocations, target, out var targetOrigin))
            return NoopStackResolutionResult(state);
        var zones = NormalizeZonesForSeats(state); var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones); var events = new List<GameEvent>();
        TryMoveFieldUnitByEffect(state, zones, cards, locations, source.ObjectId, targetOrigin, item.ControllerId, item, events);
        TryMoveFieldUnitByEffect(state, zones, cards, locations, target, sourceOrigin, item.ControllerId, item, events);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, Events = events };
    }
}
