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
    {
        if (!state.CardObjects.TryGetValue(id, out var card) || card.IsExhausted || card.IsFaceDown
            || !SourceObjectControlledByPlayerOrLegacyOwned(card, player)) return false;
        if (ability.SourceScope == ActivatedAbilitySourceScopes.FriendlyLegend)
        {
            return state.PlayerZones.TryGetValue(player, out var zones)
                && zones.LegendZone.Contains(id, StringComparer.Ordinal)
                && HasControllerCardOnField(state, player, ability.RequiredControllerCardNoOnField);
        }

        return IsObjectOnField(state.PlayerZones, id)
            && card.ControllerId == player
            && card.Tags.Contains(ability.SourceRequiredTag, StringComparer.Ordinal)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, card.CardNo);
    }

    internal static bool HasControllerCardOnField(MatchState state, string player, string cardNo)
        => string.IsNullOrWhiteSpace(cardNo)
            || state.CardObjects.Values.Any(candidate => candidate.ControllerId == player
                && candidate.CardNo == cardNo
                && IsObjectOnField(state.PlayerZones, candidate.ObjectId));

    internal static bool IsPowerAbilityTarget(MatchState state, string player, string id, P4ActivatedAbilityDefinition? ability = null)
        => IsFieldObject(state.PlayerZones, id) && state.CardObjects.TryGetValue(id, out var card)
            && !string.IsNullOrWhiteSpace(card.CardNo) && !card.IsFaceDown
            && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
            && (ability?.TargetScope != CardTargetScopes.EnemyUnit
                || EffectiveFieldControllerId(state.PlayerZones, id, card) != player)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id);

    private static ResolutionResult ResolveTargetedPowerModifierAbility(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        if (!CanActivateOrdinaryPowerAbility(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "此技能只能在你的开放主阶段激活。", ErrorCodes.PhaseNotAllowed);
        var targets = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targets.Count != 1 || command.TargetObjectIds.Count != 1
            || !IsPowerAbilitySource(state, intent.PlayerId, command.SourceObjectId, ability)
            || !IsPowerAbilityTarget(state, intent.PlayerId, targets[0], ability))
            return RejectWithCorePrompts(state, "请选择有效的活跃来源和一名公开单位。", ErrorCodes.InvalidTarget);
        if (!TryExtractAbilityRunePaymentResourceActions(state, intent.PlayerId, NormalizeOptionalCosts(command.OptionalCosts),
                out var otherCosts, out var resources, out var recycledRunes) || otherCosts.Count != 0)
            return RejectWithCorePrompts(state, "此技能没有所选额外费用。", ErrorCodes.InvalidTarget);

        var tax = ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out var taxedTargets);
        const string window = "ACTIVATE_ABILITY";
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, window, intent.PlayerId, command.SourceObjectId, command.AbilityId);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, window, intent.PlayerId, 0, 0, tax, tax,
            paymentResourceActionIds: resources, reason: ability.EffectKind, sourceObjectId: command.SourceObjectId,
            abilityId: ability.AbilityId, powerPurpose: PowerPurposeForActivatedAbility(ability),
            manaPurpose: ManaPurposeForActivatedAbility(state, command.SourceObjectId),
            manaSourceSelection: command.ServerManaSourceSelection,
            auditMetadata: new Dictionary<string, object?> {
                ["spellshieldTaxPower"] = tax, ["spellshieldTaxTargetObjectIds"] = taxedTargets.ToArray(),
                ["recycledRuneObjectIds"] = recycledRunes.ToArray(), ["exhaustsSource"] = true,
                ["targetObjectIds"] = targets.ToArray() });
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>();
        var pools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (!TryApplyOrderedPaymentResources(state, intent.PlayerId, resources, zones, ref cards,
                locations, ref pools, events, window, paymentId))
            return RejectWithCorePrompts(state, "支付资源动作顺序或来源无效。", ErrorCodes.InvalidTarget);
        var payment = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!payment.Accepted)
            return RejectWithCorePrompts(state, "资源不足，无法支付目标的法盾。", ErrorCodes.InsufficientCost);

        var source = cards[command.SourceObjectId];
        cards[command.SourceObjectId] = ExhaustForInstruction(zones, cards, source);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-POWER", intent.PlayerId,
            source.ObjectId, ability.EffectKind, source.CardNo, targets, 0, 1, []) {
                TargetedPowerAbility = new(ability.AbilityId, source.ObjectId, source.ObjectGeneration,
                    ability.PowerModifierAmountIfSourceEmpowered is not null ? source.IsEmpowered : null) };
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
        if (item.TargetObjectIds.Count == 1 && IsPowerAbilityTarget(state, item.ControllerId, item.TargetObjectIds[0], ability)) {
            var id = item.TargetObjectIds[0];
            var empowered = state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                && source.ObjectGeneration == item.TargetedPowerAbility!.SourceGeneration && IsObjectOnField(state.PlayerZones, source.ObjectId)
                    ? source.IsEmpowered : item.TargetedPowerAbility!.LastKnownSourceEmpowered;
            var amount = ability.PowerModifierAmountIfSourceEmpowered is { } upgraded && empowered == true
                ? upgraded : ability.PowerModifierAmount;
            if (ability.Kind == ActivatedAbilityKinds.StunEnemyUnit)
            {
                if (CanExecuteStun(cards[id]))
                {
                    cards[id] = cards[id] with { UntilEndOfTurnEffects = cards[id].UntilEndOfTurnEffects
                        .Append("STUNNED").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                    var applied = new GameEvent("STATUS_EFFECT_APPLIED", "技能眩晕敌方单位",
                        new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId,
                            ["targetObjectId"] = id, ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED" });
                    events.Add(CaptureStunObservers(applied, state.PlayerZones, cards, item, id));
                }
                return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
            }
            var behavior = new CardBehaviorDefinition(ability.SourceCardNo, ability.DisplayName, 0, ability.EffectKind, 0, 1,
                TargetScope: CardTargetScopes.AnyUnit, PowerModifierAmount: amount,
                MinimumPowerAfterModifier: ability.MinimumPowerAfterModifier);
            if (ability.Kind == ActivatedAbilityKinds.GrantUnitBoonOrTemporaryPower
                && !cards[id].Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal))
            {
                cards[id] = ApplyBoon(cards[id], behavior, item, id, out var boonEvents, state.PlayerZones, cards);
                events.AddRange(boonEvents);
            }
            else
            {
                cards[id] = ApplyPowerModifier(state, cards[id], behavior, item, id, amount, out var modified);
                events.Add(modified);
            }
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }

    private static ResolutionResult RecordTargetedPowerSourceStatus(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList();
        var changed = false;
        var stack = result.State.StackItems.Select(item => {
            if (item.TargetedPowerAbility is not { } context
                || !P4ActivatedAbilityCatalog.TryGetByAbilityId(context.AbilityId, out var ability)
                || ability.PowerModifierAmountIfSourceEmpowered is null) return item;
            bool? empowered = context.LastKnownSourceEmpowered;
            if (result.State.CardObjects.TryGetValue(context.SourceObjectId, out var current)
                && current.ObjectGeneration == context.SourceGeneration && IsObjectOnField(result.State.PlayerZones, current.ObjectId))
                empowered = current.IsEmpowered;
            else if (before.CardObjects.TryGetValue(context.SourceObjectId, out var previous)
                && previous.ObjectGeneration == context.SourceGeneration && IsObjectOnField(before.PlayerZones, previous.ObjectId)) {
                empowered = previous.IsEmpowered;
                foreach (var ev in result.Events)
                    if (ev.Kind is "OBJECT_EMPOWERED" or "OBJECT_EMPOWER_REMOVED"
                        && EventPayloadString(ev, "sourceObjectId") == context.SourceObjectId
                        && ev.Payload.TryGetValue("objectGeneration", out var generation)
                        && Convert.ToInt64(generation, System.Globalization.CultureInfo.InvariantCulture) == context.SourceGeneration)
                        empowered = ev.Kind == "OBJECT_EMPOWERED";
            }
            if (empowered == context.LastKnownSourceEmpowered) return item;
            changed = true;
            events.Add(new("ABILITY_SOURCE_STATUS_CAPTURED", "保留技能来源的最后强化状态", new Dictionary<string, object?> {
                ["stackItemId"] = item.StackItemId, ["sourceObjectId"] = context.SourceObjectId,
                ["sourceGeneration"] = context.SourceGeneration, ["isEmpowered"] = empowered }));
            return item with { TargetedPowerAbility = context with { LastKnownSourceEmpowered = empowered } };
        }).ToArray();
        if (!changed) return result;
        var state = result.State with { StackItems = stack };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
