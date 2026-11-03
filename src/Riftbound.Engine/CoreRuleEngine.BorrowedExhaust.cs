using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool CanActivateBorrowedExhaust(MatchState state, string player, BorrowedExhaustSpec spec)
        => spec.Reaction ? CanActivateReactionResourceSkill(state, player)
            : spec.Swift ? CanActivateSwiftAbility(state, player) : CanActivateOrdinaryPowerAbility(state, player);

    private static bool IsBorrowedReactionResource(MatchState state, string player, ActivateAbilityCommand command)
        => state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            && BorrowedExhaustRules.Abilities(state, player, source).Any(a => a.Id == command.AbilityId && a.Spec.Kind == "RESOURCE");

    internal static PowerPaymentPurpose BorrowedPowerPurpose(CardObjectState source)
        => source.Tags.Contains(CardObjectTags.EquipmentCard) ? PowerPaymentPurpose.Equipment : PowerPaymentPurpose.Other;

    internal static bool IsBorrowedExhaustTarget(MatchState state, string player, string id, BorrowedExhaustSpec spec)
        => spec.Kind == "READY_EQUIPMENT" ? IsLegendEquipmentReadyTarget(state, player, id)
            : state.CardObjects.TryGetValue(id, out var card) && IsFaceUpNonStandbyUnit(card)
                && IsObjectOnField(state.PlayerZones, id)
                && (spec.TargetScope != "BATTLEFIELD_UNIT" || state.PlayerZones.Values.Any(z => z.Battlefields.Contains(id)))
                && (spec.TargetScope is not ("FRIENDLY_UNIT" or "EXHAUSTED_FRIENDLY_UNIT")
                    || EffectiveFieldControllerId(state.PlayerZones, id, card) == player)
                && (spec.TargetScope != "EXHAUSTED_FRIENDLY_UNIT" || card.IsExhausted)
                && (spec.TargetScope != "ENEMY_UNIT" || EffectiveFieldControllerId(state.PlayerZones, id, card) != player)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id);

    private static ResolutionResult ResolveBorrowedExhaust(MatchState state, PlayerIntent intent, ActivateAbilityCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.AbilityInstanceId)
            || !state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            || BorrowedExhaustRules.Abilities(state, intent.PlayerId, source).FirstOrDefault(a => a.Id == command.AbilityId) is not { } ability)
            return RejectWithCorePrompts(state, "来源当前不具有所选技能。", ErrorCodes.InvalidTarget);
        if (!CanActivateBorrowedExhaust(state, intent.PlayerId, ability.Spec))
            return RejectWithCorePrompts(state, "当前时机不能激活此技能。", ErrorCodes.PhaseNotAllowed);
        var targets = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targets.Count != ability.Spec.Targets || command.TargetObjectIds.Count != targets.Count
            || targets.Distinct(StringComparer.Ordinal).Count() != targets.Count
            || targets.Any(id => !IsBorrowedExhaustTarget(state, intent.PlayerId, id, ability.Spec)))
            return RejectWithCorePrompts(state, "请选择技能要求的不同合法目标。", ErrorCodes.InvalidTarget);
        var requested = NormalizeOptionalCosts(command.OptionalCosts);
        if (!TryExtractRenektonPlayPayment(requested, out var withoutRenekton, out var renekton)
            || !TryExtractAbilityRunePaymentResourceActions(state, intent.PlayerId, withoutRenekton,
                out var extras, out var resources, out _) || extras.Count != (ability.Spec.Conversion.Length > 0 ? 1 : 0)
                    + (ability.Spec.DiscardsEquipmentAsCost ? 1 : 0))
            return RejectWithCorePrompts(state, "所选支付资源行动无效。", ErrorCodes.InvalidTarget);
        var discard = extras.Where(id => id.StartsWith("DISCARD_HAND_CARD:", StringComparison.Ordinal)).ToArray();
        if (ability.Spec.DiscardsEquipmentAsCost
            && (discard.Length != 1 || !SimpleActivationDiscardChoices(state, intent.PlayerId).Contains(discard[0])))
            return RejectWithCorePrompts(state, "请选择自己手中的装备牌支付弃置费用。", ErrorCodes.InvalidTarget);
        var ordered = renekton is null ? resources : OrderedRenektonPaymentResources(requested, resources, renekton);
        var tax = ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out _);
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY", intent.PlayerId, source.ObjectId, ability.Id);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, "ACTIVATE_ABILITY", intent.PlayerId,
            ability.Spec.Mana, ability.Spec.Mana, ability.Spec.Power + tax,
            ability.Spec.Power + tax + ability.Spec.TypedPower.Values.Sum(), ability.Spec.TypedPower,
            experienceCost: ability.Spec.Experience, optionalCostIds: extras, paymentResourceActionIds: ordered,
            sourceObjectId: source.ObjectId, abilityId: ability.Id, reason: BorrowedExhaustRules.Effect,
            manaPurpose: ManaPurposeForActivatedAbility(state, source.ObjectId), powerPurpose: BorrowedPowerPurpose(source),
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
        if (ability.Spec.DiscardsEquipmentAsCost)
        {
            if (!SimpleActivationDiscardChoices(paidState, intent.PlayerId).Contains(discard[0]))
                return RejectWithCorePrompts(state, "所选装备已无法支付弃置费用。", ErrorCodes.InvalidTarget);
            var discarded = discard[0]["DISCARD_HAND_CARD:".Length..];
            if (!TryDiscardCardFromHand(zones, cards, intent.PlayerId, discarded))
                return RejectWithCorePrompts(state, "无法弃置所选装备手牌。", ErrorCodes.InvalidTarget);
            events.Add(new("CARD_DISCARDED", "弃置装备以支付获得的技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = discarded,
                ["reason"] = "ABILITY_COST", ["destinationZone"] = "GRAVEYARD" }));
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, intent.PlayerId, "CARD_DISCARDED", source.ObjectId, [discarded], events);
        }
        if (ability.Spec.Kind == "RESOURCE")
            return ResolveBorrowedResource(state, intent, ability, plan, payment, zones, cards, locations, events, extras);
        source = cards[source.ObjectId];
        var factories = targets.Where(id => TokenObjectRules.IsToken(cards[id]))
            .ToDictionary(id => id, id => cards[id].TokenFactoryCardNo ?? cards[id].CardNo!, StringComparer.Ordinal);
        var context = new BorrowedExhaustContext(BorrowedExhaustRules.Capture(source, ability.BorrowerText),
            BorrowedExhaustRules.Capture(cards[ability.Donor.ObjectId], ability.DonorText), ability.Spec.Index,
            state.Tick + 1, source.IsEmpowered, factories.Count == 0 ? null : factories,
            zones.Values.Any(z => z.Battlefields.Contains(source.ObjectId)));
        if (ability.Spec.RemovesSourceEmpowerment)
        {
            var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards },
                new(source.ObjectId, source.ObjectGeneration), false, intent.PlayerId);
            source = changed.CardObjects[source.ObjectId];
            events.AddRange(changed.Events);
        }
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
        events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED", "横置来源以支付技能费用", new Dictionary<string, object?>(payload) {
            ["targetObjectId"] = source.ObjectId, ["wasExhausted"] = false, ["isExhausted"] = true }));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能加入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = state.SpellDuelState.IsActive ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed,
            PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = payment.RunePools, PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item],
            UntilEndOfTurnEffects = source.Tags.Contains(CardObjectTags.UnitCard)
                ? MarkEzrealEnemyTargetsThisTurnForUnitAbility(state, state.UntilEndOfTurnEffects, intent.PlayerId, targets)
                : state.UntilEndOfTurnEffects };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveBorrowedExhaustStack(MatchState state, StackItemState item)
    {
        if (!BorrowedExhaustRules.Valid(state, item)) throw new InvalidOperationException("Invalid captured borrowed skill.");
        var c = item.BorrowedExhaust!;
        var spec = BorrowedExhaustRules.Spec(c.Donor.CardNo, c.AbilityIndex)!;
        var cards = state.CardObjects.ToDictionary();
        var zones = NormalizeZonesForSeats(state);
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
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
            else if (spec.Kind == "READY_UNIT")
            {
                var behavior = new CardBehaviorDefinition(c.Donor.CardNo, "获得的活跃技能", 0, item.EffectKind, 0, 1);
                cards[id] = ApplyReadyState(target, behavior, item, id, out var readied, zones, cards);
                if (readied is not null) events.Add(readied);
            }
            else if (spec.Kind == "BOON_UNIT")
            {
                var behavior = new CardBehaviorDefinition(c.Donor.CardNo, "获得的增益技能", 0, item.EffectKind, 0, 1,
                    TargetScope: CardTargetScopes.AnyUnit, GrantsBoon: true);
                cards[id] = ApplyBoon(target, behavior, item, id, out var applied, zones, cards);
                events.AddRange(applied);
            }
            else if (spec.Kind == "KEYWORD_UNIT")
            {
                var definition = TemporaryCombatKeywordRules.Prefix + spec.Keyword;
                var effect = TemporaryCombatKeywordRules.AppliedEffectId(target, definition);
                cards[id] = target with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(target.UntilEndOfTurnEffects, effect) };
                events.Add(new("STATUS_EFFECT_APPLIED", "获得的技能给予临时关键词", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = id, ["effectId"] = definition }));
            }
            else if (spec.Kind == "DAMAGE_UNIT")
            {
                var amount = ApplySpellSkillDamageBonuses(current, item.ControllerId, id, spec.Damage, item);
                var damage = ApplyDamageToCardObject(current, cards, item.ControllerId, id, amount, destroyed,
                    state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new("DAMAGE_APPLIED", "获得的技能造成伤害", BuildDamagePayload(item.SourceObjectId, id, damage)));
            }
            else
            {
                var behavior = new CardBehaviorDefinition(c.Donor.CardNo, "获得的战力技能", 0, item.EffectKind, 0, 1,
                    PowerModifierAmount: spec.Might, MinimumPowerAfterModifier: spec.MinimumMight);
                cards[id] = ApplyPowerModifier(current, target, behavior, item, id, spec.Might, out var modified);
                events.Add(modified);
            }
        }
        if (spec.Kind != "DAMAGE_UNIT")
            return NoopStackResolutionResult(state) with { CardObjects = cards, PlayerZones = zones, Events = events };
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, state.RunePools,
            damageTriggeredDestroyTargetObjectIds: destroyed, objectLocations: locations,
            destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
        events.AddRange(cleanup.Events);
        return NoopStackResolutionResult(state) with { CardObjects = cards, PlayerZones = zones, Events = events,
            RunePools = cleanup.RunePools, TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds,
            ObjectLocations = ReconcileObjectLocations(locations, zones) };
    }
}
