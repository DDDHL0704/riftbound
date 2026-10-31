using Riftbound.Contracts;

namespace Riftbound.Engine;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record SimpleActivationContext(string AbilityId, ObjectBinding Source, long AcquiredTick,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? TargetTokenFactories = null);

public sealed partial class CoreRuleEngine
{
    private static string SimpleActivationUse(CardObjectState source, string ability)
        => $"PRINTED_ACTIVATION_USED:{source.ObjectId}:{source.ObjectGeneration}:{ability}";
    private static string EnemyUnitTargetMarker(string actor) => "TARGETED_ENEMY_UNIT_THIS_TURN:" + actor;

    // Track confirmed targets for every skill/spell through the shared stack;
    // paying a non-target cost never counts as choosing an enemy unit.
    private static ResolutionResult RecordEnemyUnitTargets(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var markers = result.State.UntilEndOfTurnEffects.ToHashSet(StringComparer.Ordinal);
        foreach (var item in result.State.StackItems) {
            if (item.TargetGenerations is null || item.EmpowerActivation is not null) continue;
            var prior = before.StackItems.FirstOrDefault(i => i.StackItemId == item.StackItemId);
            if (prior?.TargetGenerations is not null && prior.TargetObjectIds.SequenceEqual(item.TargetObjectIds)) continue;
            if (item.TargetObjectIds.Any(id => before.CardObjects.TryGetValue(id, out var target)
                && IsObjectOnField(before.PlayerZones, id) && !target.IsFaceDown
                && target.Tags.Contains(CardObjectTags.UnitCard) && target.ControllerId != item.ControllerId))
                markers.Add(EnemyUnitTargetMarker(item.ControllerId));
        }
        if (markers.Count == result.State.UntilEndOfTurnEffects.Count) return result;
        var next = result.State with { UntilEndOfTurnEffects = markers.Order(StringComparer.Ordinal).ToArray() };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    internal static string[] SimpleActivationDiscardChoices(MatchState state, string actor)
        => state.PlayerZones[actor].Hand.Where(id => state.CardObjects.TryGetValue(id, out var card)
            && card.Tags.Contains(CardObjectTags.EquipmentCard)).Select(id => "DISCARD_HAND_CARD:" + id).ToArray();

    internal static bool IsSimpleAbilitySource(MatchState state, string actor, string id, P4ActivatedAbilityDefinition ability)
        => state.CardObjects.TryGetValue(id, out var source) && source.ObjectId == id && source.ControllerId == actor
            && !source.IsFaceDown && (!ability.ExhaustsSourceAsCost || !source.IsExhausted)
            && source.Tags.Contains(ability.SourceRequiredTag)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, source.CardNo)
            && FindFieldObjectLocation(state.PlayerZones, id) is { } at && at.PlayerId == actor
            && state.ObjectLocations.TryGetValue(id, out var precise) && precise.PlayerId == actor && precise.Zone == at.Zone
            && (ability.SimpleEffect?.RemovesSourceEmpowermentAsCost != true || source.IsEmpowered)
            && (ability.SimpleEffect?.OncePerTurn != true || !state.UntilEndOfTurnEffects.Contains(SimpleActivationUse(source, ability.AbilityId)))
            && (ability.SimpleEffect?.RequiresEnemyUnitTargetThisTurn != true || state.UntilEndOfTurnEffects.Contains(EnemyUnitTargetMarker(actor)))
            && (ability.SimpleEffect?.DiscardsEquipmentAsCost != true || SimpleActivationDiscardChoices(state, actor).Length > 0);

    internal static bool IsSimpleAbilityTarget(MatchState state, string actor, string sourceId, string id, P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect?.TransfersSourceToChosenPlayer == true ? state.Seats.ContainsKey(id)
            : state.CardObjects.TryGetValue(id, out var target) && !target.IsFaceDown
            && IsObjectOnField(state.PlayerZones, id)
            && (ability.SimpleEffect?.EmpowersOtherEquipment == true && id != sourceId && target.Tags.Contains(CardObjectTags.EquipmentCard)
                || ability.SimpleEffect?.BattlefieldUnitDamage > 0 && target.Tags.Contains(CardObjectTags.UnitCard)
                    && state.ObjectLocations.GetValueOrDefault(id)?.Zone == "BATTLEFIELD")
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
                out var extras, out var resources, out var recycled)
            || (ability.SimpleEffect!.DiscardsEquipmentAsCost ? extras.Count != 1
                || !SimpleActivationDiscardChoices(state, intent.PlayerId).Contains(extras[0]) : extras.Count != 0))
            return RejectWithCorePrompts(state, "技能支付选择不合法。", ErrorCodes.InvalidTarget);
        var tax = ability.AppliesSpellshieldTargetTax ? ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out _) : 0;
        var typed = P4ActivatedAbilityCatalog.PowerCostByTraitForAbility(ability);
        const string window = "ACTIVATE_ABILITY";
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, window, intent.PlayerId, command.SourceObjectId, ability.AbilityId);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, window, intent.PlayerId,
            baseManaCost: ability.ManaCost, totalManaCost: ability.ManaCost,
            genericPowerCost: ability.PowerCost + tax, totalPowerCost: ability.PowerCost + tax + typed.Values.Sum(),
            powerCostByTrait: typed, optionalCostIds: extras, paymentResourceActionIds: resources, reason: ability.EffectKind,
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
        if (ability.SimpleEffect.DiscardsEquipmentAsCost) {
            var discarded = extras.Single()["DISCARD_HAND_CARD:".Length..];
            if (!TryDiscardCardFromHand(zones, cards, intent.PlayerId, discarded))
                return RejectWithCorePrompts(state, "无法弃置所选装备。", ErrorCodes.InvalidTarget);
            events.Add(new("CARD_DISCARDED", "弃置装备以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = discarded,
                ["reason"] = "ABILITY_COST", ["destinationZone"] = "GRAVEYARD" }));
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, intent.PlayerId, "CARD_DISCARDED", source.ObjectId, [discarded], events);
        }
        if (ability.SimpleEffect!.RemovesSourceEmpowermentAsCost)
        {
            var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards }, new(source.ObjectId, source.ObjectGeneration), false, intent.PlayerId);
            source = changed.CardObjects[source.ObjectId];
            events.AddRange(changed.Events);
        }
        cards[source.ObjectId] = ability.ExhaustsSourceAsCost ? ExhaustForInstruction(zones, cards, source) : source;
        var tokenTargets = targets.Where(id => cards.TryGetValue(id, out var target) && TokenObjectRules.IsToken(target))
            .ToDictionary(id => id, id => cards[id].TokenFactoryCardNo ?? cards[id].CardNo!, StringComparer.Ordinal);
        var context = new SimpleActivationContext(ability.AbilityId, new(source.ObjectId, source.ObjectGeneration), state.Tick + 1,
            tokenTargets.Count == 0 ? null : tokenTargets);
        var item = SimpleActivationStackItem(context, intent.PlayerId, source.CardNo!, ability, targets);
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
            ["abilityId"] = ability.AbilityId, ["effectKind"] = ability.EffectKind, ["targetObjectIds"] = targets.ToArray() };
        events.Add(new("ABILITY_ACTIVATED", $"打出{ability.DisplayName}的技能", payload));
        if (ability.ExhaustsSourceAsCost)
            events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED", "横置来源以支付技能费用", payload));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, paid.RunePools, paid.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var effects = ability.SimpleEffect.OncePerTurn
            ? state.UntilEndOfTurnEffects.Append(SimpleActivationUse(source, ability.AbilityId)).ToArray()
            : state.UntilEndOfTurnEffects;
        if (ability.SimpleEffect.DiscardsEquipmentAsCost)
            effects = MarkPlayerDiscardedHandCardsThisTurn(effects, intent.PlayerId, [extras.Single()["DISCARD_HAND_CARD:".Length..]], zones);
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = paid.RunePools,
            PlayerExperience = paid.PlayerExperience, StackItems = [..state.StackItems, item],
            UntilEndOfTurnEffects = effects };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackItemState SimpleActivationStackItem(SimpleActivationContext context, string actor, string cardNo,
        P4ActivatedAbilityDefinition ability, IReadOnlyList<string> targets)
        => new($"STACK-{context.AcquiredTick}-{context.Source.ObjectId}-PRINTED", actor, context.Source.ObjectId,
            ability.EffectKind, cardNo, targets, 0, 1, []) { SimpleActivation = context };

    internal static bool SimpleActivationTargetsPlayer(MatchState state, StackItemState item)
        => P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && ability.SimpleEffect?.TransfersSourceToChosenPlayer == true && ValidSimpleActivation(state, item);

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
            || (item.TargetGenerations?.Count ?? 0) != (ability.SimpleEffect!.TransfersSourceToChosenPlayer ? 0 : ability.RequiredTargetCount)
            || context.TargetTokenFactories is not null && context.TargetTokenFactories.Any(p => !item.TargetObjectIds.Contains(p.Key)
                || !P6TokenFactoryCatalog.TryGetByCardNo(p.Value, out var factory)
                || !factory.Tags.Contains(ability.SimpleEffect!.EmpowersOtherEquipment ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard))
            || (ability.SimpleEffect!.TransfersSourceToChosenPlayer ? item.TargetObjectIds.Any(id => !state.Seats.ContainsKey(id))
                : item.TargetObjectIds.Any(id => item.TargetGenerations!.GetValueOrDefault(id, -1) < 0
                || (state.CardObjects.TryGetValue(id, out var target)
                    ? item.TargetGenerations![id] > target.ObjectGeneration
                        || context.TargetTokenFactories?.GetValueOrDefault(id) != target.TokenFactoryCardNo
                    : context.TargetTokenFactories?.ContainsKey(id) != true)))) return false;
        var expected = SimpleActivationStackItem(context, item.ControllerId, item.CardNo!, ability, item.TargetObjectIds)
            with { TargetGenerations = item.TargetGenerations, TokenEntryPlan = item.TokenEntryPlan,
                DepartedTokenTargets = item.DepartedTokenTargets };
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
        if (spec.TransfersSourceToChosenPlayer && cards.TryGetValue(item.SourceObjectId, out var transferred)
            && transferred.ObjectGeneration == item.SimpleActivation!.Source.Generation && IsObjectOnField(zones, transferred.ObjectId)) {
            var controller = item.TargetObjectIds.Single();
            var previous = EffectiveFieldControllerId(zones, transferred.ObjectId, transferred);
            cards[transferred.ObjectId] = transferred with { ControllerId = controller, AttachedToObjectId = null, AttachedRuleText = null };
            RecallAttachedGroupToBase(zones, cards, transferred.ObjectId, controller);
            events.Add(new("CONTROL_CHANGED", "所选玩家获得来源物体控制权并将其召回", new Dictionary<string, object?> {
                ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = item.SourceObjectId,
                ["previousControllerId"] = previous, ["controllerId"] = controller, ["destinationZone"] = "BASE" }));
        }
        if (spec.ReadiesSource && cards.TryGetValue(item.SourceObjectId, out var source)
            && source.ObjectGeneration == item.SimpleActivation!.Source.Generation && IsObjectOnField(zones, source.ObjectId)) {
            var behavior = new CardBehaviorDefinition(item.CardNo!, ability.DisplayName, 0, ability.EffectKind, 0, 0, PowerModifierAmount: spec.SourcePowerBonus);
            source = ApplyReadyState(source, behavior, item, source.ObjectId, out var ready, zones, cards);
            if (ready is not null) events.Add(ready);
            source = ApplyPowerModifier(state, source, behavior, item, source.ObjectId, spec.SourcePowerBonus, out var power);
            events.Add(power); cards[source.ObjectId] = source;
        }
        if (spec.BattlefieldUnitDamage > 0) {
            var target = item.TargetObjectIds.Single();
            if (IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, target, ability)
                && item.TargetGenerations![target] == cards[target].ObjectGeneration) {
                var destroyed = new HashSet<string>(StringComparer.Ordinal);
                var amount = ApplySpellSkillDamageBonuses(state, item.ControllerId, target, spec.BattlefieldUnitDamage);
                var damage = ApplyDamageToCardObject(state, cards, item.ControllerId, target, amount, destroyed,
                    state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new("DAMAGE_APPLIED", "主动技能造成伤害", BuildDamagePayload(item.SourceObjectId, target, damage)));
                var cleanup = RunStateBasedCleanupLoop(state, zones, cards, item, pools, damageTriggeredDestroyTargetObjectIds: destroyed,
                    objectLocations: locations, destroyedUnitOwnerIdsAlreadyThisTurn: state.DestroyedUnitOwnerIdsThisTurn);
                events.AddRange(cleanup.Events); pools = cleanup.RunePools.ToDictionary(p => p.Key, p => p.Value);
                result = result with { TriggerQueue = cleanup.TriggerQueue, DestroyedUnitOwnerIds = cleanup.DestroyedUnitOwnerIds };
            }
        }
        if (spec.DrawCount > 0)
        {
            var drawn = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId, spec.DrawCount, state.RngCursor, events, deferNormalVictory: true);
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
                var changed = ApplyEmpowerStatusAction(state, new(target, cards[target].ObjectGeneration), true, item.ControllerId);
                cards[target] = changed.CardObjects[target]; events.AddRange(changed.Events);
            }
        }
        return result with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
            ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
    }
}
