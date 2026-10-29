using Riftbound.Contracts;

namespace Riftbound.Engine;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record SimpleActivationContext(string AbilityId, ObjectBinding Source, long AcquiredTick,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? TargetTokenFactories = null);

public sealed partial class CoreRuleEngine
{
    internal static bool IsSimpleAbilitySource(MatchState state, string actor, string id, P4ActivatedAbilityDefinition ability)
        => state.CardObjects.TryGetValue(id, out var source) && source.ObjectId == id && source.ControllerId == actor
            && !source.IsFaceDown && (!ability.ExhaustsSourceAsCost || !source.IsExhausted)
            && source.Tags.Contains(ability.SourceRequiredTag)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, source.CardNo)
            && FindFieldObjectLocation(state.PlayerZones, id) is { } at && at.PlayerId == actor
            && state.ObjectLocations.TryGetValue(id, out var precise) && precise.PlayerId == actor && precise.Zone == at.Zone
            && (ability.SimpleEffect?.RemovesSourceEmpowermentAsCost != true || source.IsEmpowered);

    internal static bool IsSimpleAbilityTarget(MatchState state, string actor, string sourceId, string id, P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect?.EmpowersOtherEquipment == true && id != sourceId
            && state.CardObjects.TryGetValue(id, out var target) && !target.IsFaceDown
            && target.Tags.Contains(CardObjectTags.EquipmentCard) && IsObjectOnField(state.PlayerZones, id)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id);

    private static ResolutionResult ResolveSimpleActivation(MatchState state, PlayerIntent intent,
        ActivateAbilityCommand command, P4ActivatedAbilityDefinition ability)
    {
        if (!CanActivateOrdinaryPowerAbility(state, intent.PlayerId))
            return RejectWithCorePrompts(state, "此技能只能在你的开放主阶段打出。", ErrorCodes.PhaseNotAllowed);
        var targets = NormalizeTargetObjectIds(command.TargetObjectIds);
        if (targets.Count != command.TargetObjectIds.Count || targets.Count != ability.RequiredTargetCount
            || !IsSimpleAbilitySource(state, intent.PlayerId, command.SourceObjectId, ability)
            || targets.Any(id => !IsSimpleAbilityTarget(state, intent.PlayerId, command.SourceObjectId, id, ability)))
            return RejectWithCorePrompts(state, "技能来源、横置或解除强化费用、目标不合法。", ErrorCodes.InvalidTarget);
        if (!TryExtractRecycleRunePaymentResourceActions(state, intent.PlayerId, NormalizeOptionalCosts(command.OptionalCosts),
                out var extras, out var resources, out var recycled) || extras.Count != 0)
            return RejectWithCorePrompts(state, "技能支付选择不合法。", ErrorCodes.InvalidTarget);
        var tax = ability.AppliesSpellshieldTargetTax ? ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out _) : 0;
        var typed = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        const string window = "ACTIVATE_ABILITY";
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, window, intent.PlayerId, command.SourceObjectId, ability.AbilityId);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, window, intent.PlayerId,
            baseManaCost: ability.ManaCost, totalManaCost: ability.ManaCost,
            genericPowerCost: ability.PowerCost + tax, totalPowerCost: ability.PowerCost + tax + typed.Values.Sum(),
            powerCostByTrait: typed, paymentResourceActionIds: resources, reason: ability.EffectKind,
            sourceObjectId: command.SourceObjectId, abilityId: ability.AbilityId,
            powerPurpose: PowerPurposeForActivatedAbility(ability), manaPurpose: ManaPurposeForActivatedAbility(state, command.SourceObjectId),
            manaSourceSelection: command.ServerManaSourceSelection);
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>();
        var pools = ApplyRecycleRunePaymentResourceActions(state.RunePools, zones, cards, locations,
            intent.PlayerId, recycled, events, window, paymentId);
        var paid = PaymentCostRules.TryCommitPayment(plan, pools, state.PlayerExperience, state: state);
        if (!paid.Accepted) return RejectWithCorePrompts(state, "资源不足，未横置或解除来源的强化。", ErrorCodes.InsufficientCost);
        var source = cards[command.SourceObjectId];
        if (ability.SimpleEffect!.RemovesSourceEmpowermentAsCost)
        {
            var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards }, new(source.ObjectId, source.ObjectGeneration), false);
            source = changed.CardObjects[source.ObjectId];
            events.AddRange(changed.Events);
        }
        cards[source.ObjectId] = source with { IsExhausted = source.IsExhausted || ability.ExhaustsSourceAsCost };
        var tokenTargets = targets.Where(id => TokenObjectRules.IsToken(cards[id]))
            .ToDictionary(id => id, id => cards[id].TokenFactoryCardNo ?? cards[id].CardNo!, StringComparer.Ordinal);
        var context = new SimpleActivationContext(ability.AbilityId, new(source.ObjectId, source.ObjectGeneration), state.Tick + 1,
            tokenTargets.Count == 0 ? null : tokenTargets);
        var item = SimpleActivationStackItem(context, intent.PlayerId, source.CardNo!, ability, targets);
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
            ["abilityId"] = ability.AbilityId, ["effectKind"] = ability.EffectKind, ["targetObjectIds"] = targets.ToArray() };
        events.Add(new("ABILITY_ACTIVATED", $"打出{ability.DisplayName}的技能", payload));
        events.Add(new("EQUIPMENT_EXHAUSTED", "横置来源以支付技能费用", payload));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, paid.RunePools, paid.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = locations, RunePools = paid.RunePools,
            PlayerExperience = paid.PlayerExperience, StackItems = [..state.StackItems, item] };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackItemState SimpleActivationStackItem(SimpleActivationContext context, string actor, string cardNo,
        P4ActivatedAbilityDefinition ability, IReadOnlyList<string> targets)
        => new($"STACK-{context.AcquiredTick}-{context.Source.ObjectId}-PRINTED", actor, context.Source.ObjectId,
            ability.EffectKind, cardNo, targets, 0, 1, []) { SimpleActivation = context };

    internal static bool ValidSimpleActivation(MatchState state, StackItemState item)
    {
        var recognized = P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability) && ability.SimpleEffect is not null;
        if (item.SimpleActivation is not { } context) return !recognized;
        if (!recognized || context.AbilityId != ability.AbilityId || context.Source is not { Generation: >= 0 }
            || context.AcquiredTick <= 0 || context.AcquiredTick > state.Tick || !state.Seats.ContainsKey(item.ControllerId)
            || !P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, item.CardNo)
            || !state.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
            || source.ObjectGeneration < context.Source.Generation
            || source.ObjectGeneration == context.Source.Generation
                && (!P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, source.CardNo) || !source.Tags.Contains(ability.SourceRequiredTag))
            || item.TargetObjectIds.Count != ability.RequiredTargetCount || item.TargetObjectIds.Distinct().Count() != ability.RequiredTargetCount
            || (item.TargetGenerations?.Count ?? 0) != ability.RequiredTargetCount
            || context.TargetTokenFactories is not null && context.TargetTokenFactories.Any(p => !item.TargetObjectIds.Contains(p.Key)
                || !P6TokenFactoryCatalog.TryGetByCardNo(p.Value, out var factory) || !factory.Tags.Contains(CardObjectTags.EquipmentCard))
            || item.TargetObjectIds.Any(id => item.TargetGenerations!.GetValueOrDefault(id, -1) < 0
                || (state.CardObjects.TryGetValue(id, out var target)
                    ? item.TargetGenerations![id] > target.ObjectGeneration
                        || context.TargetTokenFactories?.GetValueOrDefault(id) != target.TokenFactoryCardNo
                    : context.TargetTokenFactories?.ContainsKey(id) != true))) return false;
        var expected = SimpleActivationStackItem(context, item.ControllerId, item.CardNo!, ability, item.TargetObjectIds)
            with { TargetGenerations = item.TargetGenerations, TokenEntryPlan = item.TokenEntryPlan };
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(expected)
            && (item.TokenEntryPlan is null || ability.SimpleEffect!.CreatedRobotCount > 0 && ValidTokenEntryPlan(state, item));
    }

    private static CardBehaviorDefinition SimpleActivationTokenBehavior(P4ActivatedAbilityDefinition ability)
        => new(ability.SourceCardNo, ability.DisplayName, 0, ability.EffectKind, 0, 0,
            CreatedBaseUnitTokenCount: ability.SimpleEffect!.CreatedRobotCount,
            CreatedBaseUnitTokenName: "机器人", CreatedBaseUnitTokenPower: 3, CreatedBaseUnitTokenTags: "机械");

    private static StackResolutionResult ResolveSimpleActivationEffect(MatchState state, StackItemState item, P4ActivatedAbilityDefinition ability)
    {
        if (!ValidSimpleActivation(state, item)) throw new InvalidOperationException("Invalid captured printed activation.");
        var spec = ability.SimpleEffect!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", $"{ability.DisplayName}技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = ability.AbilityId }) };
        var result = NoopStackResolutionResult(state);
        if (spec.DrawCount > 0)
        {
            var drawn = ApplyDrawToPlayer(state, zones, state.PlayerScores, item.ControllerId, spec.DrawCount, state.RngCursor, events, deferNormalVictory: true);
            result = result with { PlayerScores = drawn.PlayerScores, WinnerPlayerId = drawn.WinnerPlayerId, RngCursor = drawn.RngCursor };
        }
        if (spec.CreatedRobotCount > 0)
            CreateBaseUnitTokens(state, zones, cards, pools, SimpleActivationTokenBehavior(ability), item, events, locations, state.UntilEndOfTurnEffects);
        if (spec.EmpowersOtherEquipment)
        {
            var target = item.TargetObjectIds.Single();
            if (IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, target, ability)
                && item.TargetGenerations![target] == cards[target].ObjectGeneration)
            {
                var changed = ApplyEmpowerStatusAction(state, new(target, cards[target].ObjectGeneration), true);
                cards[target] = changed.CardObjects[target]; events.AddRange(changed.Events);
            }
        }
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
