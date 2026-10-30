using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExhaustReadyActivationContext(ObjectBinding Source, string CardNo, int AbilityIndex,
    ExhaustedUnitFact CostUnit, ObjectBinding Target, int TurnNumber, long AcquiredTick, long FaceRevision,
    string InstanceId, string? SourceTokenFactoryCardNo, RuleTextInstanceBinding? Origin = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExhaustReadyUse(string Controller, ExhaustReadyActivationContext Context);

internal sealed record ExhaustReadyAbility(string Id, RuleTextInstance Instance, int Index, ActivatedAbilitySpec Spec)
{
    internal string? InstanceId => Instance.Origin is null ? null : $"{Instance.Id}:{Id}";
}

internal static class ExhaustReadyRules
{
    internal const string Effect = "EXHAUST_FRIENDLY_UNIT_READY_UNIT";
    internal const string UsedPrefix = "EXHAUST_READY_USED|";
    private static readonly Lazy<IReadOnlyDictionary<string, ActivatedAbilitySpec[]>> SpecsByCard = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo,
            c => ActivatedAbilityParser.Parse(c.CardEffect).Where(a => a.Kind == ActivatedAbilityKinds.ExhaustFriendlyUnitReadyUnit
                && a.OncePerTurn == true && a.TargetScope == CardTargetScopes.AnyUnit && a.RequiredTargetCount == 1).ToArray()));
    internal static IReadOnlyList<ActivatedAbilitySpec> Specs(string no) => SpecsByCard.Value.GetValueOrDefault(no) ?? [];
    internal static IEnumerable<ExhaustReadyAbility> Abilities(MatchState state, CardObjectState source)
        => AttachedRuleTextRules.Instances(state, source).SelectMany(instance => Specs(instance.CardNo)
            .Select((spec, index) => new ExhaustReadyAbility($"EXHAUST-READY:{index}", instance, index, spec)))
            .Where(ability => !Used(state, source, ability));
    internal static bool CanActivate(MatchState state, string actor, CardObjectState source)
        => SelfMightRules.CanActivate(state, actor, source);
    internal static string Marker(ExhaustReadyUse use)
        => UsedPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(use)));
    internal static ExhaustReadyUse? Read(string marker)
    {
        if (!marker.StartsWith(UsedPrefix, StringComparison.Ordinal)) return null;
        try {
            var use = JsonSerializer.Deserialize<ExhaustReadyUse>(Encoding.UTF8.GetString(Convert.FromBase64String(marker[UsedPrefix.Length..])));
            return use is { Context: not null, Controller.Length: > 0 } && marker == Marker(use) ? use : null;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException) { return null; }
    }
    private static bool Used(MatchState state, CardObjectState source, ExhaustReadyAbility ability)
        => state.UntilEndOfTurnEffects.Select(Read).OfType<ExhaustReadyUse>().Any(use =>
            use.Context.Source == new ObjectBinding(source.ObjectId, source.ObjectGeneration)
            && use.Context.CardNo == ability.Instance.CardNo && use.Context.AbilityIndex == ability.Index
            && use.Context.InstanceId == ability.Instance.Id
            && use.Context.FaceRevision == (ability.Instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0));
}

public sealed partial class CoreRuleEngine
{
    internal static string[] ExhaustReadyCostChoices(MatchState state, string actor)
        => state.CardObjects.Values.Where(unit => IsFaceUpNonStandbyUnit(unit) && !unit.IsExhausted
                && IsObjectOnField(state.PlayerZones, unit.ObjectId)
                && EffectiveFieldControllerId(state.PlayerZones, unit.ObjectId, unit) == actor)
            .Select(unit => unit.ObjectId).Order(StringComparer.Ordinal).ToArray();

    private static bool ValidExhaustReadyContext(MatchState state, string controller, ExhaustReadyActivationContext c)
    {
        if (c.Source is not { Generation: >= 0 } || c.CostUnit is not { Unit.Generation: >= 0, FaceRevision: >= 0 }
            || c.Target is not { Generation: >= 0 } || string.IsNullOrWhiteSpace(c.Target.ObjectId)
            || c.CardNo is null || c.AbilityIndex < 0 || c.AbilityIndex >= ExhaustReadyRules.Specs(c.CardNo).Count
            || c.TurnNumber <= 0 || c.TurnNumber != state.TurnNumber || c.AcquiredTick <= 0 || c.AcquiredTick > state.Tick || c.FaceRevision < 0
            || !state.Seats.ContainsKey(controller)
            || c.SourceTokenFactoryCardNo is not null && (!P6TokenFactoryCatalog.TryGetByCardNo(c.SourceTokenFactoryCardNo, out var factory)
                || !factory.Tags.Contains(CardObjectTags.UnitCard))
            || !PredictBindingMatches(state, c.CostUnit.Unit, c.CostUnit.CardNo, c.CostUnit.FaceRevision,
                c.AcquiredTick, c.CostUnit.TokenFactoryCardNo)
            || state.CardObjects.TryGetValue(c.Target.ObjectId, out var target) && target.ObjectGeneration < c.Target.Generation)
            return false;
        return c.Origin is { } origin
            ? c.FaceRevision == origin.FaceRevision
                && c.InstanceId == $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
                && AttachedRuleTextRules.ValidCaptured(state, origin, c.Source.ObjectId, c.Source.Generation, c.CardNo)
            : c.InstanceId == $"PRINTED:{c.Source.ObjectId}:{c.Source.Generation}"
                && PredictBindingMatches(state, c.Source, c.CardNo, c.FaceRevision, c.AcquiredTick, c.SourceTokenFactoryCardNo);
    }

    internal static bool ValidExhaustReadyUses(MatchState state)
    {
        var uses = state.UntilEndOfTurnEffects.Where(e => e.StartsWith(ExhaustReadyRules.UsedPrefix, StringComparison.Ordinal))
            .Select(ExhaustReadyRules.Read).ToArray();
        return uses.All(use => use is not null && ValidExhaustReadyContext(state, use.Controller, use.Context))
            && uses.Select(use => (use!.Context.Source, use.Context.CardNo, use.Context.InstanceId,
                use.Context.FaceRevision, use.Context.AbilityIndex)).Distinct().Count() == uses.Length;
    }

    internal static bool ValidExhaustReadyActivation(MatchState state, StackItemState item)
    {
        if (item.ExhaustReadyActivation is not { } c) return item.EffectKind != ExhaustReadyRules.Effect;
        return ValidExhaustReadyContext(state, item.ControllerId, c)
            && item.EffectKind == ExhaustReadyRules.Effect && item.CardNo == c.CardNo && item.SourceObjectId == c.Source.ObjectId
            && item.StackItemId == $"STACK-{c.AcquiredTick}-{c.Source.ObjectId}-EXHAUST-READY"
            && item.TargetObjectIds.Count == 1 && item.TargetObjectIds[0] == c.Target.ObjectId
            && item.TargetGenerations is { Count: 1 } && item.TargetGenerations.GetValueOrDefault(c.Target.ObjectId, -1) == c.Target.Generation
            && item.OptionalCosts.Count == 0 && !item.SourceConfirmed && item.EffectRepeatCount == 1 && item.DamageAmount == 0
            && item.Destination.Length == 0 && item.FieldContext is null && item.SpellContext is null && item.PlayCost is null
            && item.SelfMightActivation is null && item.RecycleMightActivation is null && item.TargetedPowerAbility is null
            && item.TemporaryActivation is null && item.EmpowerActivation is null
            && state.UntilEndOfTurnEffects.Contains(ExhaustReadyRules.Marker(new(item.ControllerId, c)), StringComparer.Ordinal);
    }

    private static ResolutionResult ResolveExhaustReadyActivation(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || !ExhaustReadyRules.CanActivate(state, intent.PlayerId, source))
            return RejectWithCorePrompts(state, "请在你的开放主阶段选择受控的公开单位。", ErrorCodes.InvalidTarget);
        var ability = ExhaustReadyRules.Abilities(state, source).FirstOrDefault(a => a.Id == command.AbilityId
            && (a.InstanceId is null ? string.IsNullOrEmpty(command.AbilityInstanceId) : a.InstanceId == command.AbilityInstanceId));
        var costs = NormalizeOptionalCosts(command.OptionalCosts);
        if (ability is null || command.TargetObjectIds.Count != 1 || !IsPowerAbilityTarget(state, intent.PlayerId, command.TargetObjectIds[0])
            || costs.Count != (command.OptionalCosts?.Count ?? 0)
            || !TryExtractRenektonPlayPayment(costs, out var withoutLegend, out var legendAction)
            || !TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, withoutLegend, out var extra, out var resources, out var recycled)
            || extra.Count != 1 || !TryParseExhaustFriendlyUnitOptionalCost(extra[0], out var costId)
            || extra[0] != ExhaustFriendlyUnitOptionalCostPrefix + costId
            || !ExhaustReadyCostChoices(state, intent.PlayerId).Contains(costId, StringComparer.Ordinal))
            return RejectWithCorePrompts(state, "请选择一名单位，并让恰好一名活跃友方单位休眠以支付费用；此技能每回合限一次。", ErrorCodes.InvalidTarget);
        var targets = command.TargetObjectIds.ToArray();
        var tax = ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out var taxed);
        var ordered = legendAction is null ? resources : OrderedRenektonPaymentResources(costs, resources, legendAction);
        var plan = new PaymentCostRules.PaymentPlan(PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY",
            intent.PlayerId, source.ObjectId, ability.Id), "ACTIVATE_ABILITY", intent.PlayerId, 0, 0, tax, tax,
            paymentResourceActionIds: ordered, reason: ExhaustReadyRules.Effect, sourceObjectId: source.ObjectId,
            abilityId: ability.Id, powerPurpose: PowerPaymentPurpose.Other,
            manaPurpose: ManaPurposeForActivatedAbility(state, source.ObjectId), manaSourceSelection: command.ServerManaSourceSelection,
            auditMetadata: new Dictionary<string, object?> { ["spellshieldTaxPower"] = tax,
                ["spellshieldTaxTargetObjectIds"] = taxed.ToArray(), ["targetObjectIds"] = targets });
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
        if (!payment.Accepted) return RejectWithCorePrompts(state, "符能不足，无法支付目标法盾。", ErrorCodes.InsufficientCost);
        if (!cards.TryGetValue(costId, out var paid) || paid.IsExhausted || !IsFaceUpNonStandbyUnit(paid)
            || paid.ObjectGeneration != state.CardObjects[costId].ObjectGeneration || !IsObjectOnField(zones, costId)
            || EffectiveFieldControllerId(zones, costId, paid) != intent.PlayerId)
            return RejectWithCorePrompts(state, "所选单位无法继续支付休眠费用。", ErrorCodes.InvalidTarget);
        var context = new ExhaustReadyActivationContext(new(source.ObjectId, source.ObjectGeneration), ability.Instance.CardNo, ability.Index,
            new(new(paid.ObjectId, paid.ObjectGeneration), AttachedRuleTextRules.Face(paid)!, paid.CopyFaceTransitions?.Count ?? 0, paid.TokenFactoryCardNo),
            new(targets[0], state.CardObjects[targets[0]].ObjectGeneration), state.TurnNumber, state.Tick + 1,
            ability.Instance.Origin?.FaceRevision ?? source.CopyFaceTransitions?.Count ?? 0, ability.Instance.Id, source.TokenFactoryCardNo, ability.Instance.Origin);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-EXHAUST-READY", intent.PlayerId,
            source.ObjectId, ExhaustReadyRules.Effect, ability.Instance.CardNo, targets, 0, 1, []) {
                ExhaustReadyActivation = context, TargetGenerations = new Dictionary<string, long> { [targets[0]] = context.Target.Generation } };
        CaptureExhaustionInstruction(zones, cards, intent.PlayerId, [costId], "ACTIVATE_ABILITY:" + item.StackItemId, events);
        events.Add(CaptureSourceExhaustionTokens(new("UNIT_EXHAUSTED", "让友方单位休眠以支付技能费用", new Dictionary<string, object?> {
            ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = costId,
            ["wasExhausted"] = false, ["isExhausted"] = true, ["abilityId"] = ability.Id }), zones, cards, [paid]));
        cards[costId] = paid with { IsExhausted = true };
        events.Add(new("COST_PAID", "支付休眠技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools,
            payment.PlayerExperience, new Dictionary<string, object?> { ["sourceObjectId"] = source.ObjectId, ["exhaustedAdditionalCostTargetObjectIds"] = new[] { costId } })));
        events.Add(new("ABILITY_ACTIVATED", "激活单位活跃技能", new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["abilityId"] = ability.Id, ["abilityInstanceId"] = command.AbilityInstanceId,
            ["effectKind"] = item.EffectKind, ["targetObjectIds"] = targets }));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = source.ObjectId, ["effectKind"] = item.EffectKind }));
        var next = state with { Tick = state.Tick + 1, PlayerZones = zones, CardObjects = cards,
            ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = payment.RunePools,
            PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item],
            UntilEndOfTurnEffects = [..MarkEzrealEnemyTargetsThisTurnForUnitAbility(state, state.UntilEndOfTurnEffects, intent.PlayerId, targets),
                ExhaustReadyRules.Marker(new(intent.PlayerId, context))],
            TimingState = TimingStates.NeutralClosed, ActivePlayerId = intent.PlayerId,
            PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveExhaustReadyStack(MatchState state, StackItemState item)
    {
        if (!ValidExhaustReadyActivation(state, item)) throw new InvalidOperationException("Invalid exhaust-ready activation.");
        var target = item.ExhaustReadyActivation!.Target;
        if (!state.CardObjects.TryGetValue(target.ObjectId, out var unit) || unit.ObjectGeneration != target.Generation
            || !IsPowerAbilityTarget(state, item.ControllerId, target.ObjectId)) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var behavior = new CardBehaviorDefinition(item.CardNo!, "单位活跃技能", 0, item.EffectKind, 0, 1);
        cards[unit.ObjectId] = ApplyReadyState(unit, behavior, item, unit.ObjectId, out var ready, state.PlayerZones, cards);
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = ready is null ? [] : [ready] };
    }
}
