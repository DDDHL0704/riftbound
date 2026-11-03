using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool IsBorrowedExhaustTarget(MatchState state, string player, string id, BorrowedExhaustSpec spec)
        => spec.Kind == "READY_EQUIPMENT" ? IsLegendEquipmentReadyTarget(state, player, id)
            : state.CardObjects.TryGetValue(id, out var card) && IsFaceUpNonStandbyUnit(card)
                && IsObjectOnField(state.PlayerZones, id)
                && (spec.Kind != "STUN_ENEMY" || EffectiveFieldControllerId(state.PlayerZones, id, card) != player)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id);

    private static ResolutionResult ResolveBorrowedExhaust(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!CanActivateOrdinaryPowerAbility(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "此技能只能在你的开放主阶段激活。", ErrorCodes.PhaseNotAllowed);
        if (!string.IsNullOrWhiteSpace(command.AbilityInstanceId)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || BorrowedExhaustRules.Abilities(state, intent.PlayerId, source).FirstOrDefault(a => a.Id == command.AbilityId) is not { } ability)
            return RejectWithCorePrompts(state, "来源当前不具有所选技能。", ErrorCodes.InvalidTarget);
        var targets = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targets.Count != ability.Spec.Targets || command.TargetObjectIds.Count != targets.Count
            || targets.Distinct(StringComparer.Ordinal).Count() != targets.Count
            || targets.Any(id => !IsBorrowedExhaustTarget(state, intent.PlayerId, id, ability.Spec)))
            return RejectWithCorePrompts(state, "请选择技能要求的不同合法目标。", ErrorCodes.InvalidTarget);
        var requested = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRenektonPlayPayment(requested, out var withoutRenekton, out var renekton)
            || !TryExtractAbilityRunePaymentResourceActions(state, intent.PlayerId, withoutRenekton,
                out var extras, out var resources, out _) || extras.Count != 0)
            return RejectWithCorePrompts(state, "所选支付资源行动无效。", ErrorCodes.InvalidTarget);
        var ordered = renekton is null ? resources : OrderedRenektonPaymentResources(requested, resources, renekton);
        var tax = ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out _);
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY", intent.PlayerId, source.ObjectId, ability.Id);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, "ACTIVATE_ABILITY", intent.PlayerId,
            ability.Spec.Mana, ability.Spec.Mana, tax, tax, paymentResourceActionIds: ordered,
            sourceObjectId: source.ObjectId, abilityId: ability.Id, reason: BorrowedExhaustRules.Effect,
            manaPurpose: ManaPurposeForActivatedAbility(state, source.ObjectId), powerPurpose: PowerPaymentPurpose.Other,
            manaSourceSelection: command.ServerManaSourceSelection);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary();
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var pools = state.RunePools.ToDictionary();
        var events = new List<GameEvent>();
        if (!TryApplyOrderedPaymentResources(state, intent.PlayerId, ordered, zones, ref cards, locations,
            ref pools, events, "ACTIVATE_ABILITY", paymentId))
            return RejectWithCorePrompts(state, "支付资源行动顺序或来源无效。", ErrorCodes.InvalidTarget);
        var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!payment.Accepted) return RejectWithCorePrompts(state, "资源不足，未横置来源。", ErrorCodes.InsufficientCost);
        var paidState = state with { PlayerZones = zones, CardObjects = cards };
        if (!BorrowedExhaustRules.Abilities(paidState, intent.PlayerId, cards[source.ObjectId]).Any(a => a.Id == ability.Id))
            return RejectWithCorePrompts(state, "支付资源行动后来源已无法使用该技能。", ErrorCodes.InvalidTarget);
        source = cards[source.ObjectId];
        var factories = targets.Where(id => TokenObjectRules.IsToken(cards[id]))
            .ToDictionary(id => id, id => cards[id].TokenFactoryCardNo ?? cards[id].CardNo!, StringComparer.Ordinal);
        var context = new BorrowedExhaustContext(BorrowedExhaustRules.Capture(source, ability.BorrowerText),
            BorrowedExhaustRules.Capture(cards[ability.Donor.ObjectId], ability.DonorText), ability.Spec.Index,
            state.Tick + 1, source.IsEmpowered, factories.Count == 0 ? null : factories);
        cards[source.ObjectId] = ExhaustForInstruction(zones, cards, source);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-BORROWED", intent.PlayerId,
            source.ObjectId, BorrowedExhaustRules.Effect, AttachedRuleTextRules.Face(source), targets,
            timingContext: StackTimingContextForNewStackItem(state)) {
            BorrowedExhaust = context,
            TargetGenerations = targets.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal) };
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["controllerId"] = intent.PlayerId,
            ["sourceObjectId"] = source.ObjectId, ["cardNo"] = source.CardNo, ["abilityId"] = ability.Id,
            ["effectKind"] = item.EffectKind, ["targetObjectIds"] = targets.ToArray() };
        events.Add(new("ABILITY_ACTIVATED", "激活获得的技能", payload));
        events.Add(new("UNIT_EXHAUSTED", "横置来源以支付技能费用", new Dictionary<string, object?>(payload) {
            ["targetObjectId"] = source.ObjectId, ["wasExhausted"] = false, ["isExhausted"] = true }));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能加入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = payment.RunePools, PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveBorrowedExhaustStack(MatchState state, StackItemState item)
    {
        if (!BorrowedExhaustRules.Valid(state, item)) throw new InvalidOperationException("Invalid captured borrowed skill.");
        var c = item.BorrowedExhaust!;
        var spec = BorrowedExhaustRules.Spec(c.Donor.CardNo, c.AbilityIndex)!;
        var cards = state.CardObjects.ToDictionary();
        var zones = NormalizeZonesForSeats(state);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "获得的技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId }) };
        using (new MightInstructionScope(state, zones, cards, events))
        foreach (var id in item.TargetObjectIds)
        {
            var current = state with { CardObjects = cards };
            if (!StackObjectRules.ReferencesCurrentObject(current, item, id)
                || !IsBorrowedExhaustTarget(current, item.ControllerId, id, spec)) continue;
            var target = cards[id];
            if (spec.Kind == "READY_EQUIPMENT")
            {
                if (AttachedRuleTextRules.Instances(current, target).Any(i => CardStaticAbilitySpecRules.CardCannotBecomeActive(i.CardNo))) continue;
                cards[id] = target with { IsExhausted = false };
                events.Add(new("EQUIPMENT_READIED", "装备变为活跃状态", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id,
                    ["wasExhausted"] = target.IsExhausted, ["isExhausted"] = false }));
            }
            else if (spec.Kind == "STUN_ENEMY")
            {
                if (!CanExecuteStun(target) || TryReplaceEmpoweredStun(current, item, cards, id, events)) continue;
                cards[id] = target with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(target.UntilEndOfTurnEffects, "STUNNED") };
                events.Add(CaptureStunObservers(new("STATUS_EFFECT_APPLIED", "眩晕目标单位", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["effectId"] = "STUNNED" }), zones, cards, item, id));
            }
            else
            {
                var behavior = new CardBehaviorDefinition(c.Donor.CardNo, "获得的战力技能", 0, item.EffectKind, 0, 1,
                    PowerModifierAmount: spec.Might, MinimumPowerAfterModifier: spec.MinimumMight);
                cards[id] = ApplyPowerModifier(current, target, behavior, item, id, spec.Might, out var modified);
                events.Add(modified);
            }
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, PlayerZones = zones, Events = events };
    }
}
