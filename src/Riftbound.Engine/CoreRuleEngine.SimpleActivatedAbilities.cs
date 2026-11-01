using Riftbound.CardCatalog;
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
    private static bool IsRad158BlackMarket(P4ActivatedAbilityDefinition ability)
        => ability.AbilityId == P4ActivatedAbilityCatalog.BlackMarketAbilityId;
    private static string Rad158BlackMarketUse(int turn, string playerId)
        => $"RAD158_BLACK_MARKET_USED:{turn}:{playerId}";
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
        => state.CardObjects.TryGetValue(id, out var source) && source.ObjectId == id
            && (IsRad158BlackMarket(ability) || source.ControllerId == actor)
            && !source.IsFaceDown && (!ability.ExhaustsSourceAsCost || !source.IsExhausted)
            && source.Tags.Contains(ability.SourceRequiredTag)
            && P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(ability, source.CardNo)
            && FindFieldObjectLocation(state.PlayerZones, id) is { } at
            && (IsRad158BlackMarket(ability) || at.PlayerId == actor)
            && state.ObjectLocations.TryGetValue(id, out var precise) && (IsRad158BlackMarket(ability) || precise.PlayerId == actor) && precise.Zone == at.Zone
            && (!IsRad158BlackMarket(ability) || precise.Zone == "BATTLEFIELD")
            && (!ability.RequiresBaseEquipmentSource || precise.Zone == "BASE")
            && (!ability.RequiresBattlefieldSource || precise.Zone == "BATTLEFIELD")
            && (ability.SimpleEffect?.RemovesSourceEmpowermentAsCost != true || source.IsEmpowered)
            && (ability.SimpleEffect?.OncePerTurn != true
                || (IsRad158BlackMarket(ability)
                    ? !state.UntilEndOfTurnEffects.Contains(Rad158BlackMarketUse(state.TurnNumber, actor), StringComparer.Ordinal)
                    : !state.UntilEndOfTurnEffects.Contains(SimpleActivationUse(source, ability.AbilityId))))
            && (ability.SimpleEffect?.RequiresEnemyUnitTargetThisTurn != true || state.UntilEndOfTurnEffects.Contains(EnemyUnitTargetMarker(actor)))
            && (ability.SimpleEffect?.DiscardsEquipmentAsCost != true || SimpleActivationDiscardChoices(state, actor).Length > 0);

    internal static bool IsSimpleAbilityTarget(MatchState state, string actor, string sourceId, string id, P4ActivatedAbilityDefinition ability)
        => ability.SimpleEffect?.TransfersSourceToChosenPlayer == true ? state.Seats.ContainsKey(id)
            : ability.SimpleEffect?.CommandsOpponentUnitToSourceBattlefield == true ? state.Seats.ContainsKey(id) && id != actor
            : ability.SimpleEffect?.ScrutinizesOpponentGuess == true ? state.Seats.ContainsKey(id) && id != actor
            : ability.SimpleEffect?.ReturnsFriendlyUnitThenFreePlayUpToPower == true
                ? state.CardObjects.TryGetValue(id, out var returned)
                    && !returned.IsFaceDown && returned.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && SourceObjectControlledByPlayerOrLegacyOwned(returned, actor)
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.GrantsBoonOrPower == true
                ? state.CardObjects.TryGetValue(id, out var boonTarget)
                    && !boonTarget.IsFaceDown && boonTarget.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : ability.SimpleEffect?.StunsTarget == true
                ? state.CardObjects.TryGetValue(id, out var stunTarget)
                    && !stunTarget.IsFaceDown && stunTarget.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && IsObjectOnField(state.PlayerZones, id)
                    && EffectiveFieldControllerId(state.PlayerZones, id, stunTarget) != actor
                    && TargetProtectionRules.IsLegalActivatedSkillTarget(state, actor, id)
            : state.CardObjects.TryGetValue(id, out var target) && !target.IsFaceDown
            && IsObjectOnField(state.PlayerZones, id)
            && (ability.SimpleEffect?.EmpowersOtherEquipment == true && id != sourceId && target.Tags.Contains(CardObjectTags.EquipmentCard)
                || ability.SimpleEffect?.DestroysEquipmentAsCost == true && id != sourceId
                    && target.Tags.Contains(CardObjectTags.EquipmentCard)
                    && EffectiveFieldControllerId(state.PlayerZones, id, target) == actor
                || ability.SimpleEffect?.DestroysUnitAsCost == true && id != sourceId
                    && target.Tags.Contains(CardObjectTags.UnitCard)
                    && EffectiveFieldControllerId(state.PlayerZones, id, target) == actor
                || ability.SimpleEffect?.ExhaustsTargetAsCost == true && id != sourceId
                    && target.Tags.Contains(CardObjectTags.UnitCard)
                    && EffectiveFieldControllerId(state.PlayerZones, id, target) == actor
                    && ResolveCurrentFieldUnitPower(state, target) >= 5
                    && !target.IsExhausted
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
        if (!TryExtractAbilityRunePaymentResourceActions(state, intent.PlayerId, NormalizeOptionalCosts(command.OptionalCosts),
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
        var pools = state.RunePools.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (!TryApplyOrderedPaymentResources(state, intent.PlayerId, resources, zones, ref cards,
                locations, ref pools, events, window, paymentId))
            return RejectWithCorePrompts(state, "支付资源动作顺序或来源无效。", ErrorCodes.InvalidTarget);
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
        var targetGenerations = targets
            .Where(id => cards.ContainsKey(id))
            .ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal);
        var item = SimpleActivationStackItem(context, intent.PlayerId, source.CardNo!, ability, targets)
            with { TargetGenerations = targetGenerations.Count == 0 ? null : targetGenerations };
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
            ["abilityId"] = ability.AbilityId, ["effectKind"] = ability.EffectKind, ["targetObjectIds"] = targets.ToArray() };
        events.Add(new("ABILITY_ACTIVATED", $"打出{ability.DisplayName}的技能", payload));
        if (ability.ExhaustsSourceAsCost)
            events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED", "横置来源以支付技能费用", payload));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, paid.RunePools, paid.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能进入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var effects = ability.SimpleEffect.OncePerTurn
            ? state.UntilEndOfTurnEffects.Append(IsRad158BlackMarket(ability)
                ? Rad158BlackMarketUse(state.TurnNumber, intent.PlayerId)
                : SimpleActivationUse(source, ability.AbilityId)).ToArray()
            : state.UntilEndOfTurnEffects;
        if (ability.SimpleEffect.DiscardsEquipmentAsCost)
            effects = MarkPlayerDiscardedHandCardsThisTurn(effects, intent.PlayerId, [extras.Single()["DISCARD_HAND_CARD:".Length..]], zones);
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = TimingStates.NeutralClosed, PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = paid.RunePools,
            PlayerExperience = paid.PlayerExperience, StackItems = [..state.StackItems, item],
            UntilEndOfTurnEffects = effects };
        var destructionCostTargets = new HashSet<string>(StringComparer.Ordinal);
        if (ability.SimpleEffect.DestroysSourceAsCost)
            destructionCostTargets.Add(source.ObjectId);
        if (ability.SimpleEffect.DestroysEquipmentAsCost || ability.SimpleEffect.DestroysUnitAsCost)
            destructionCostTargets.Add(targets.Single());
        if (destructionCostTargets.Count > 0)
        {
            foreach (var target in destructionCostTargets)
            {
                var removal = ResolveFieldDestructions(next with { PlayerZones = zones, CardObjects = cards,
                        ObjectLocations = locations, RunePools = next.RunePools }, zones, cards, item,
                    new HashSet<string>(StringComparer.Ordinal), next.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal),
                    next.RunePools, objectLocations: locations, explicitDestroyObjectIds: new HashSet<string> { target }, explicitDestroyReason: "ABILITY_COST");
                if (removal.Events.Count == 0)
                    return RejectWithCorePrompts(state, "无法支付摧毁费用。", ErrorCodes.InvalidTarget);
                next = next with { PlayerZones = zones, CardObjects = cards,
                    ObjectLocations = ReconcileObjectLocations(locations, zones), RunePools = removal.RunePools,
                    TriggerQueue = next.TriggerQueue.Concat(removal.TriggerQueue).ToArray(),
                    DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(next.DestroyedUnitOwnerIdsThisTurn, removal.DestroyedUnitOwnerIds) };
                events.InsertRange(0, removal.Events);
            }
        }
        if (ability.SimpleEffect.ExhaustsTargetAsCost)
        {
            var target = targets.Single();
            if (!cards.TryGetValue(target, out var targetState) || targetState.IsExhausted)
                return RejectWithCorePrompts(state, "无法横置所选强力单位。", ErrorCodes.InvalidTarget);
            cards[target] = ExhaustForInstruction(zones, cards, targetState);
            events.Add(new("UNIT_EXHAUSTED", "横置友方强力单位以支付技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId,
                ["targetObjectId"] = target, ["reason"] = "ABILITY_COST" }));
        }
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackItemState SimpleActivationStackItem(SimpleActivationContext context, string actor, string cardNo,
        P4ActivatedAbilityDefinition ability, IReadOnlyList<string> targets)
        => new($"STACK-{context.AcquiredTick}-{context.Source.ObjectId}-PRINTED", actor, context.Source.ObjectId,
            ability.EffectKind, cardNo, targets, 0, 1, []) { SimpleActivation = context };

    internal static bool SimpleActivationTargetsPlayer(MatchState state, StackItemState item)
        => P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var ability)
            && (ability.SimpleEffect?.TransfersSourceToChosenPlayer == true
                || ability.SimpleEffect?.CommandsOpponentUnitToSourceBattlefield == true
                || ability.SimpleEffect?.ScrutinizesOpponentGuess == true)
            && ValidSimpleActivation(state, item);

    internal static bool ValidSimpleActivation(MatchState state, StackItemState item, bool allowMissingReturnTarget = false)
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
            || (item.TargetGenerations?.Count ?? 0) != (ability.SimpleEffect!.TransfersSourceToChosenPlayer
                || ability.SimpleEffect.CommandsOpponentUnitToSourceBattlefield
                || ability.SimpleEffect.ScrutinizesOpponentGuess ? 0 : ability.RequiredTargetCount)
            || context.TargetTokenFactories is not null && context.TargetTokenFactories.Any(p => !item.TargetObjectIds.Contains(p.Key)
                || !P6TokenFactoryCatalog.TryGetByCardNo(p.Value, out var factory)
                || !factory.Tags.Contains(ability.SimpleEffect!.EmpowersOtherEquipment ? CardObjectTags.EquipmentCard : CardObjectTags.UnitCard))
            || (ability.SimpleEffect!.TransfersSourceToChosenPlayer || ability.SimpleEffect.CommandsOpponentUnitToSourceBattlefield
                || ability.SimpleEffect.ScrutinizesOpponentGuess
                ? item.TargetObjectIds.Any(id => !state.Seats.ContainsKey(id)
                    || (ability.SimpleEffect.CommandsOpponentUnitToSourceBattlefield || ability.SimpleEffect.ScrutinizesOpponentGuess) && id == item.ControllerId)
                : item.TargetObjectIds.Any(id => item.TargetGenerations!.GetValueOrDefault(id, -1) < 0
                || (state.CardObjects.TryGetValue(id, out var target)
                    ? item.TargetGenerations![id] > target.ObjectGeneration
                        || context.TargetTokenFactories?.GetValueOrDefault(id) != target.TokenFactoryCardNo
                    : !allowMissingReturnTarget && context.TargetTokenFactories?.ContainsKey(id) != true)))) return false;
        var expected = SimpleActivationStackItem(context, item.ControllerId, item.CardNo!, ability, item.TargetObjectIds)
            with { TargetGenerations = item.TargetGenerations, TokenEntryPlan = item.TokenEntryPlan,
                DepartedTokenTargets = item.DepartedTokenTargets,
                EffectPlayCompleted = allowMissingReturnTarget ? item.EffectPlayCompleted : false };
        return MatchStateHasher.HashValue(item) == MatchStateHasher.HashValue(expected)
            && (item.TokenEntryPlan is null || ability.SimpleEffect!.CreatedRobotCount > 0 && ValidTokenEntryPlan(state, item));
    }

    internal static bool ValidTranspositionMageEffectPlay(MatchState state, PendingEffectPlayState pending)
    {
        if (!P4ActivatedAbilityCatalog.TryGetByEffectKind(pending.Parent.EffectKind, out var ability)
            || ability.SimpleEffect?.ReturnsFriendlyUnitThenFreePlayUpToPower != true
            || pending.RevealedHand is not null || pending.Parent.EffectPlayCompleted
            || pending.SourceZone != "HAND" || pending.PlayerId != pending.Parent.ControllerId
            || !pending.Optional || !pending.IgnoreAllCosts || !pending.IgnoreBaseMana || !pending.IgnoreBasePower
            || pending.ManaReduction != 0 || pending.MaximumUnitPower is not >= 0
            || !ValidSimpleActivation(state, pending.Parent, allowMissingReturnTarget: true)
            || !state.PlayerZones.TryGetValue(pending.PlayerId, out var zones)) return false;

        var targetId = pending.Parent.TargetObjectIds.SingleOrDefault();
        if (targetId is null || !state.PlayerZones.Values.Any(player => player.Hand.Contains(targetId, StringComparer.Ordinal))) return false;

        var eligible = zones.Hand
            .Where(id => state.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior)
                && behavior.PlaysSourceToBaseAsUnit
                && ResolveCurrentFieldUnitPower(state, card) <= pending.MaximumUnitPower.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var valid = pending.Sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(eligible.Order(StringComparer.Ordinal))
            && pending.Sources.All(entry => zones.Hand.Contains(entry.Key, StringComparer.Ordinal)
                && state.CardObjects.TryGetValue(entry.Key, out var card)
                && card.ObjectGeneration == entry.Value);
        return valid;
    }

    private static CardBehaviorDefinition SimpleActivationTokenBehavior(P4ActivatedAbilityDefinition ability)
        => new(ability.SourceCardNo, ability.DisplayName, 0, ability.EffectKind, 0, 0,
            CreatedBaseUnitTokenCount: ability.SimpleEffect!.CreatedRobotCount,
            CreatedBaseUnitTokenName: "机器人", CreatedBaseUnitTokenPower: 3, CreatedBaseUnitTokenTags: "机械");

    private static StackResolutionResult ResolveSimpleActivationEffect(MatchState state, StackItemState item, P4ActivatedAbilityDefinition ability)
    {
        var spec = ability.SimpleEffect!;
        if (!ValidSimpleActivation(state, item,
                allowMissingReturnTarget: spec.ReturnsFriendlyUnitThenFreePlayUpToPower && item.EffectPlayCompleted))
            throw new InvalidOperationException("Invalid captured printed activation.");
        if (spec.ReturnsFriendlyUnitThenFreePlayUpToPower && item.EffectPlayCompleted
            && !state.PlayerZones.Values.Any(player => player.Hand.Contains(item.TargetObjectIds.SingleOrDefault() ?? string.Empty, StringComparer.Ordinal)))
            throw new InvalidOperationException("Invalid completed transposition target.");
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", $"{ability.DisplayName}技能结算",
            new Dictionary<string, object?> { ["sourceObjectId"] = item.SourceObjectId, ["abilityId"] = ability.AbilityId }) };
        var result = NoopStackResolutionResult(state);
        if (spec.ScrutinizesOpponentGuess)
        {
            var opponent = item.TargetObjectIds.Single();
            var frame = RuleChoices.Value ?? throw new InvalidOperationException("严审透镜需要对手猜测卡牌类型。");
            var guess = frame.Choose(opponent, "严审透镜：猜测牌库顶牌的类型。",
                [new("UNIT", "单位"), new("EQUIPMENT", "装备"), new("SPELL", "法术")],
                kind: "RAD064_GUESS", preview: state);
            var top = zones[item.ControllerId].MainDeck.FirstOrDefault();
            if (top is null || !cards.TryGetValue(top, out var card))
                return result with { PlayerZones = zones, CardObjects = cards, Events = events };
            events.Add(new("CARD_REVEALED", "严审透镜展示牌库顶牌", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["targetObjectId"] = top, ["cardNo"] = card.CardNo, ["zone"] = TriggerZones.MainDeck,
                ["guess"] = guess, ["reason"] = "RAD064_SCRUTINY_LENS" }));
            var actual = card.Tags.Contains(CardObjectTags.UnitCard) ? "UNIT"
                : card.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT"
                : card.Tags.Contains(CardObjectTags.SpellCard) ? "SPELL" : "";
            if (actual == guess)
            {
                var recycled = RecycleLookedMainDeckCards(state, zones, cards, item.ControllerId,
                    item.SourceObjectId, [top], result.RngCursor);
                events.AddRange(recycled.Events); result = result with { RngCursor = recycled.RngCursor };
            }
            else
            {
                var drawState = state with { PlayerZones = zones, CardObjects = cards,
                    ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
                var drawn = ApplyDrawToPlayer(drawState, zones, cards, state.PlayerScores,
                    item.ControllerId, 1, result.RngCursor, events, deferNormalVictory: true);
                result = result with { PlayerScores = drawn.PlayerScores, WinnerPlayerId = drawn.WinnerPlayerId,
                    RngCursor = drawn.RngCursor };
            }
            return result with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones), Events = events };
        }
        if (spec.CommandsOpponentUnitToSourceBattlefield
            && cards.TryGetValue(item.SourceObjectId, out var commandedSource)
            && commandedSource.ObjectGeneration == item.SimpleActivation!.Source.Generation
            && TryGetPreciseFieldLocation(zones, locations, commandedSource.ObjectId, out var sourceLocation)
            && sourceLocation.Zone == "BATTLEFIELD"
            && sourceLocation.BattlefieldObjectId is { } battlefieldId
            && BattlefieldLocalRules.Battlefield(state, battlefieldId) is not null)
        {
            var opponent = item.TargetObjectIds.Single();
            var current = state with {
                PlayerZones = zones,
                CardObjects = cards,
                ObjectLocations = locations,
                RunePools = pools
            };
            var legal = cards.Values
                .Where(card => IsFaceUpNonStandbyUnit(card)
                    && FindFieldObjectLocation(zones, card.ObjectId) is { } location
                    && location.PlayerId == opponent
                    && IsUnitMovementDestination(current, card.ObjectId, "BATTLEFIELD:" + battlefieldId)
                    && MovementRestrictionRules.CanMove(card, opponent))
                .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
                .ToArray();
            if (legal.Length > 0)
            {
                var frame = RuleChoices.Value ?? throw new InvalidOperationException("提灯亚龙需要对手选择移动单位。");
                var selected = frame.Choose(opponent, "提灯亚龙：选择你控制的一名单位，移动到提灯亚龙所在战场。",
                    legal.Select(card => new RuleChoiceOption(
                        "MOVE:" + card.ObjectId,
                        OfficialCardSourceIdentityGroups.DisplayName(card.CardNo ?? "") + "（" + card.ObjectId + "）",
                        [card.ObjectId])).ToArray(),
                    kind: "MOVEMENT_SELECTION", preview: current);
                TryMoveFieldUnitByEffect(current, zones, cards, locations, selected[5..],
                    new(item.ControllerId, "BATTLEFIELD", battlefieldId), opponent, item, events);
            }
            else
            {
                events.Add(new("ABILITY_NO_EFFECT", "提灯亚龙结算时对手没有可移动的单位",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["opponentId"] = opponent,
                        ["abilityId"] = ability.AbilityId, ["stackItemId"] = item.StackItemId,
                        ["reason"] = "NO_LEGAL_OPPONENT_UNIT"
                    }));
            }
        }
        if (spec.ReturnsFriendlyUnitThenFreePlayUpToPower && item.EffectPlayCompleted)
            return result;
        if (spec.ReturnsFriendlyUnitThenFreePlayUpToPower && !item.EffectPlayCompleted)
        {
            var targetId = item.TargetObjectIds.SingleOrDefault();
            var targetGeneration = targetId is null ? -1 : item.TargetGenerations?.GetValueOrDefault(targetId, -1) ?? -1;
            if (targetId is null
                || targetGeneration < 0
                || !cards.TryGetValue(targetId, out var target)
                || target.ObjectGeneration != targetGeneration
                || !IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, targetId, ability))
            {
                events.Add(new("ABILITY_NO_EFFECT", "易位法师结算时目标已不再合法",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["abilityId"] = ability.AbilityId, ["stackItemId"] = item.StackItemId,
                        ["reason"] = "TARGET_NO_LONGER_LEGAL"
                    }));
                return result with { Events = events };
            }

            var capturedPower = ResolveCurrentFieldUnitPower(state, target);
            if (!TryReturnTargetToHand(zones, cards, targetId, out var ownerPlayerId, out var wasEquipment))
            {
                events.Add(new("ABILITY_NO_EFFECT", "易位法师结算时无法返回目标",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["abilityId"] = ability.AbilityId, ["stackItemId"] = item.StackItemId,
                        ["reason"] = "TARGET_RETURN_FAILED"
                    }));
                return result with { Events = events };
            }
            events.Add(BuildReturnedToHandEvent(ability.DisplayName, item, targetId, ownerPlayerId, wasEquipment));

            var eligible = zones[item.ControllerId].Hand
                .Where(id => cards.TryGetValue(id, out var candidate)
                    && candidate.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                    && CardBehaviorRegistry.TryGetByCardNo(candidate.CardNo ?? "", out var behavior)
                    && behavior.PlaysSourceToBaseAsUnit
                    && ResolveCurrentFieldUnitPower(state with { PlayerZones = zones, CardObjects = cards }, candidate) <= capturedPower)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (eligible.Length == 0)
            {
                events.Add(new("ABILITY_NO_EFFECT", "易位法师没有符合战力限制的手牌单位",
                    new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["abilityId"] = ability.AbilityId, ["capturedPower"] = capturedPower,
                        ["reason"] = "NO_ELIGIBLE_HAND_UNIT"
                    }));
                return result with { PlayerZones = zones, CardObjects = cards,
                    ObjectLocations = ReconcileObjectLocations(locations, zones), Events = events };
            }

            var pending = new PendingEffectPlayState(
                $"EFFECT-PLAY:{state.Tick + 1}:{item.StackItemId}", item.ControllerId, item, "HAND",
                eligible.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal),
                IgnoreBaseMana: true, IgnoreBasePower: true, ManaReduction: 0,
                DestinationPolicy: "", Optional: true, IgnoreAllCosts: true,
                MaximumUnitPower: capturedPower);
            return result with { PlayerZones = zones, CardObjects = cards,
                ObjectLocations = ReconcileObjectLocations(locations, zones), PendingEffectPlay = pending,
                Events = events };
        }
        if (spec.GrantsBoonOrPower)
        {
            var targetId = item.TargetObjectIds.SingleOrDefault();
            if (targetId is not null && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == cards.GetValueOrDefault(targetId)?.ObjectGeneration
                && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, targetId, ability))
            {
                var target = cards[targetId];
                if (target.Tags.Contains(CardObjectTags.Boon, StringComparer.Ordinal))
                {
                    var next = ApplyDirectUntilEndPowerModifier(
                        target, targetId, item.SourceObjectId, item.CardNo, item.EffectKind,
                        "CoreRuleEngine.ResolveSimpleActivationEffect.PrismwingFlock", spec.ExistingBoonPowerBonus,
                        target.Power + spec.ExistingBoonPowerBonus);
                    cards[targetId] = next;
                    events.Add(new("POWER_MODIFIED_UNTIL_END_OF_TURN", "已有增益的单位本回合战力提高",
                        new Dictionary<string, object?> {
                            ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                            ["powerDelta"] = spec.ExistingBoonPowerBonus,
                            ["appliedPowerDelta"] = next.Power - target.Power,
                            ["minimumPower"] = 0, ["resultingPower"] = next.Power
                        }));
                }
                else
                    GrantLegendBoon(cards, targetId, item.ControllerId, item.SourceObjectId, item.EffectKind, events);
            }
            else
                events.Add(new("ABILITY_NO_EFFECT", "幻彩飞禽结算时目标已不再合法", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                    ["abilityId"] = ability.AbilityId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
        }
        if (spec.StunsTarget)
        {
            var targetId = item.TargetObjectIds.SingleOrDefault();
            if (targetId is not null && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == cards.GetValueOrDefault(targetId)?.ObjectGeneration
                && IsSimpleAbilityTarget(state, item.ControllerId, item.SourceObjectId, targetId, ability))
            {
                var target = cards[targetId];
                if (CanExecuteStun(target))
                {
                    cards[targetId] = target with {
                        UntilEndOfTurnEffects = target.UntilEndOfTurnEffects
                            .Concat(["STUNNED"])
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
                    var applied = new GameEvent("STATUS_EFFECT_APPLIED", "眩晕目标单位", new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["effectId"] = "STUNNED", ["statusEffectId"] = "STUNNED", ["duration"] = "UNTIL_END_OF_TURN" });
                    events.Add(CaptureStunObservers(applied, zones, cards, item, targetId));
                }
                else
                    events.Add(new("ABILITY_NO_EFFECT", "无情潜伏者结算时目标已经眩晕", new Dictionary<string, object?> {
                        ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                        ["abilityId"] = ability.AbilityId, ["reason"] = "ALREADY_STUNNED" }));
            }
            else
                events.Add(new("ABILITY_NO_EFFECT", "无情潜伏者结算时目标已不再合法", new Dictionary<string, object?> {
                    ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = targetId,
                    ["abilityId"] = ability.AbilityId, ["reason"] = "TARGET_NO_LONGER_LEGAL" }));
        }
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
        if (spec.CreatedRobotCount > 0 || spec.CreatedMinionCount > 0)
        {
            var tokenBehavior = spec.CreatedMinionCount > 0
                ? new CardBehaviorDefinition(item.CardNo!, ability.DisplayName, 0, ability.EffectKind, 0, 0,
                    CreatedBaseUnitTokenCount: spec.CreatedMinionCount, CreatedBaseUnitTokenName: "随从",
                    CreatedBaseUnitTokenPower: 1, CreatedBaseUnitTokenTags: "随从")
                : SimpleActivationTokenBehavior(ability);
            CreateBaseUnitTokens(state, zones, cards, pools, tokenBehavior, item, events, locations, state.UntilEndOfTurnEffects);
        }
        if (spec.CreatedGoldCount > 0)
        {
            var goldBehavior = new CardBehaviorDefinition(item.CardNo!, ability.DisplayName, 0, ability.EffectKind, 0, 0,
                CreatedBaseEquipmentTokenCount: spec.CreatedGoldCount, CreatedBaseEquipmentTokenName: "金币",
                CreatedBaseEquipmentTokenTags: "反应", CreatedBaseEquipmentTokenIsExhausted: true);
            CreateBaseEquipmentTokens(zones, cards, goldBehavior, item, events);
        }
        if (spec.CallRuneCount > 0)
        {
            var called = CallRunes(zones, cards, item.ControllerId, spec.CallRuneCount);
            events.Add(new("RUNES_CALLED", "主动技能召出休眠符文", new Dictionary<string, object?> {
                ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray() }));
        }
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
