using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static bool CanActivateOrdinaryPowerAbility(MatchState state, string player)
        => state.Phase == MatchPhases.Main && state.TimingState == TimingStates.NeutralOpen
            && state.ActivePlayerId == player && state.StackItems.Count == 0
            && state.PendingPayment is null && state.PendingHandChoice is null
            && state.PendingCardChoice is null && state.PendingRuleChoice is null
            && state.PendingEffectPlay is null && !state.PendingTaskQueue.IsBlocking;

    internal static bool IsPowerAbilitySource(MatchState state, string player, string id, P4ActivatedAbilityDefinition ability)
        => IsObjectOnField(state.PlayerZones, id) && state.CardObjects.TryGetValue(id, out var card)
            && card.ControllerId == player && !card.IsExhausted && !card.IsFaceDown
            && card.Tags.Contains(ability.SourceRequiredTag, StringComparer.Ordinal)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, card.CardNo);

    internal static bool IsPowerAbilityTarget(MatchState state, string player, string id)
        => IsFieldObject(state.PlayerZones, id) && state.CardObjects.TryGetValue(id, out var card)
            && !string.IsNullOrWhiteSpace(card.CardNo) && !card.IsFaceDown
            && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id);

    private static ResolutionResult ResolveTargetedPowerModifierAbility(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        if (!CanActivateOrdinaryPowerAbility(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "此技能只能在你的开放主阶段激活。", ErrorCodes.PhaseNotAllowed);
        var targets = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targets.Count != 1 || command.TargetObjectIds.Count != 1
            || !IsPowerAbilitySource(state, intent.PlayerId, command.SourceObjectId, ability)
            || !IsPowerAbilityTarget(state, intent.PlayerId, targets[0]))
            return RejectWithCorePrompts(state, "请选择有效的活跃来源和一名公开单位。", ErrorCodes.InvalidTarget);
        if (!TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, NormalizeOptionalCosts(command.OptionalCosts),
                out var otherCosts, out var resources, out var recycledRunes) || otherCosts.Count != 0)
            return RejectWithCorePrompts(state, "此技能没有所选额外费用。", ErrorCodes.InvalidTarget);

        var tax = ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out var taxedTargets);
        const string window = "ACTIVATE_ABILITY";
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, window, intent.PlayerId, command.SourceObjectId, command.AbilityId);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, window, intent.PlayerId, 0, 0, tax, tax,
            paymentResourceActionIds: resources, reason: ability.EffectKind, sourceObjectId: command.SourceObjectId,
            abilityId: ability.AbilityId, powerPurpose: PowerPurposeForActivatedAbility(ability),
            auditMetadata: new Dictionary<string, object?> {
                ["spellshieldTaxPower"] = tax, ["spellshieldTaxTargetObjectIds"] = taxedTargets.ToArray(),
                ["recycledRuneObjectIds"] = recycledRunes.ToArray(), ["exhaustsSource"] = true,
                ["targetObjectIds"] = targets.ToArray() });
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>();
        var pools = ApplyRecycleRunePaymentResourceActions(state.RunePools, zones, cards, locations,
            intent.PlayerId, recycledRunes, events, window, paymentId);
        var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!payment.Accepted)
            return RejectWithCorePrompts(state, "资源不足，无法支付目标的法盾。", ErrorCodes.InsufficientCost);

        var source = cards[command.SourceObjectId];
        cards[command.SourceObjectId] = source with { IsExhausted = true };
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-POWER", intent.PlayerId,
            source.ObjectId, ability.EffectKind, source.CardNo, targets, 0, 1, []) {
                TargetedPowerAbility = new(ability.AbilityId, source.ObjectId, source.ObjectGeneration) };
        var payload = new Dictionary<string, object?> {
            ["playerId"] = intent.PlayerId, ["controllerId"] = intent.PlayerId,
            ["sourceObjectId"] = source.ObjectId, ["cardNo"] = source.CardNo,
            ["abilityId"] = ability.AbilityId, ["effectKind"] = ability.EffectKind,
            ["targetObjectIds"] = targets.ToArray(), ["exhaustsSource"] = true };
        events.Add(new("ABILITY_ACTIVATED", $"激活{ability.DisplayName}的技能", payload));
        events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard, StringComparer.Ordinal) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED",
            "横置来源以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId,
                ["abilityId"] = ability.AbilityId, ["wasExhausted"] = false, ["isExhausted"] = true, ["exhaustsSource"] = true }));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools,
            payment.PlayerExperience, new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId })));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            CardObjects = cards, PlayerZones = zones, ObjectLocations = locations, RunePools = payment.RunePools,
            PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item],
            UntilEndOfTurnEffects = MarkEzrealEnemyTargetsThisTurnForUnitAbility(
                state, state.UntilEndOfTurnEffects, intent.PlayerId, targets) };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveTargetedPowerModifierAbilityStackItem(MatchState state,
        StackItemState item, P4ActivatedAbilityDefinition ability)
    {
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", $"{ability.DisplayName}技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId,
                ["abilityId"] = ability.AbilityId, ["stackItemId"] = item.StackItemId }) };
        if (item.TargetObjectIds.Count == 1 && IsPowerAbilityTarget(state, item.ControllerId, item.TargetObjectIds[0])) {
            var id = item.TargetObjectIds[0];
            var behavior = new CardBehaviorDefinition(ability.SourceCardNo, ability.DisplayName, 0, ability.EffectKind, 0, 1,
                TargetScope: CardTargetScopes.AnyUnit, PowerModifierAmount: ability.PowerModifierAmount,
                MinimumPowerAfterModifier: ability.MinimumPowerAfterModifier);
            cards[id] = ApplyPowerModifier(state, cards[id], behavior, item, id, ability.PowerModifierAmount, out var modified);
            events.Add(modified);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
