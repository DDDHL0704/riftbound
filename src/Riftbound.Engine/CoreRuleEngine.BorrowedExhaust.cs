using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    internal static int BorrowedExhaustManaCost(MatchState state, string player, BorrowedExhaustSpec spec)
        => spec.ManaReductionPerFriendlyEphemeralUnit == 0 ? spec.Mana
            : Math.Max(0, spec.Mana - spec.ManaReductionPerFriendlyEphemeralUnit * CountFriendlyEphemeralFieldUnits(state, player));

    internal static bool CanActivateBorrowedExhaust(MatchState state, string player, BorrowedExhaustSpec spec)
        => (!spec.RequiresPlayedArmamentThisTurn || ControllerPlayedArmamentThisTurn(state, player))
            && BorrowedEnemyTargetRequirementMet(state, player, spec)
            && (spec.Reaction ? (spec.Kind == "RESOURCE" ? CanActivateReactionResourceSkill(state, player)
                    : CanActivateReactionAbility(state, player))
                : spec.Swift ? CanActivateSwiftAbility(state, player) : CanActivateOrdinaryPowerAbility(state, player));

    internal static bool BorrowedEnemyTargetRequirementMet(MatchState state, string player, BorrowedExhaustSpec spec)
        => spec.RequiredEnemyTargetEvents == 0
            || EzrealEnemyTargetsThisTurnCount(state.UntilEndOfTurnEffects, player) >= spec.RequiredEnemyTargetEvents;

    private static bool IsBorrowedReactionResource(MatchState state, string player, ActivateAbilityCommand command)
        => state.CardObjects.TryGetValue(command.SourceObjectId, out var source)
            && BorrowedExhaustRules.Abilities(state, player, source).Any(a => a.Id == command.AbilityId
                && a.Spec.Kind == "RESOURCE" && a.Spec.Reaction);

    internal static PowerPaymentPurpose BorrowedPowerPurpose(CardObjectState source)
        => source.Tags.Contains(CardObjectTags.EquipmentCard) ? PowerPaymentPurpose.Equipment : PowerPaymentPurpose.Other;

    internal static bool CanRecycleBorrowedAbilityCost(MatchState state, string player, string id, BorrowedExhaustSpec spec)
        => CanRecycleGraveyardCardAsAdditionalCost(state, player, id)
            && (!spec.RecyclesOnlyUnitsAsCost || state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard));

    internal static string[] BorrowedUnitExhaustCostChoices(MatchState state, string player, string sourceId, BorrowedExhaustSpec spec)
        => spec.ExhaustedUnitMinimumMightAsCost is { } minimum ? state.CardObjects.Values
            .Where(card => card.ObjectId != sourceId && CanExhaustUnitForEquipmentCost(state, player, card.ObjectId)
                && ResolveCurrentFieldUnitPower(state, card) >= minimum)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
            .Select(card => ExhaustFriendlyUnitOptionalCostPrefix + card.ObjectId).ToArray() : [];

    internal static string[] BorrowedDestructionCostChoices(MatchState state, string player, BorrowedExhaustSpec spec)
        => spec.DestroyedFriendlyCardTypeAsCost.Length == 0 ? [] : FriendlyDestructionChoices(state, player,
            requiredTag: spec.DestroyedFriendlyCardTypeAsCost switch {
                "UNIT" => CardObjectTags.UnitCard, "EQUIPMENT" => CardObjectTags.EquipmentCard, _ => null })
            .Select(card => DestroyFriendlyPermanentAdditionalCostPrefix + card.ObjectId).ToArray();

    internal static bool IsBorrowedExhaustTarget(MatchState state, string player, string id, BorrowedExhaustSpec spec, string sourceId,
        int index = 0)
        => spec.Kind is "ATTACH_ARMAMENT" or "REATTACH_ARMAMENT" && index == 1
            ? IsControlledArmamentTarget(state, player, id, spec.Kind == "ATTACH_ARMAMENT", spec.Kind == "REATTACH_ARMAMENT")
            : BorrowedExhaustRules.TargetsPlayer(spec) ? state.Seats.ContainsKey(id) && (spec.TargetScope != "OPPONENT" || id != player)
            : spec.TargetScope == "OWNED_TEEMO_UNIT" ? IsValidOwnedTeemoUnitTarget(state, player, id)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id)
            : spec.TargetScope == "OTHER_FRIENDLY_PERMANENT_OR_FACEDOWN" ? id != sourceId
                && state.CardObjects.TryGetValue(id, out var permanent) && IsObjectOnField(state.PlayerZones, id)
                && EffectiveFieldControllerId(state.PlayerZones, id, permanent) == player
                && (permanent.IsFaceDown || IsFaceUpNonStandbyUnit(permanent)
                    || permanent.Tags.Contains(CardObjectTags.EquipmentCard))
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, id)
            : spec.TargetScope == "BATTLEFIELD" ? BattlefieldLocalRules.Battlefield(state, id) is not null
            : spec.Kind is "READY_EQUIPMENT" or "EMPOWER_EQUIPMENT" ? IsLegendEquipmentReadyTarget(state, player, id)
                && (spec.Kind != "EMPOWER_EQUIPMENT" || id != sourceId)
            : state.CardObjects.TryGetValue(id, out var card) && IsFaceUpNonStandbyUnit(card)
                && IsObjectOnField(state.PlayerZones, id)
                && (spec.TargetScope is not ("BATTLEFIELD_UNIT" or "FRIENDLY_BATTLEFIELD_UNIT" or "EXHAUSTED_FRIENDLY_BATTLEFIELD_UNIT" or "ENEMY_BATTLEFIELD_UNIT")
                    || state.PlayerZones.Values.Any(z => z.Battlefields.Contains(id)))
                && (spec.TargetScope is not ("FRIENDLY_UNIT" or "EXHAUSTED_FRIENDLY_UNIT" or "FRIENDLY_BATTLEFIELD_UNIT" or "EXHAUSTED_FRIENDLY_BATTLEFIELD_UNIT")
                    || EffectiveFieldControllerId(state.PlayerZones, id, card) == player)
                && (spec.TargetScope is not ("EXHAUSTED_FRIENDLY_UNIT" or "EXHAUSTED_FRIENDLY_BATTLEFIELD_UNIT") || card.IsExhausted)
                && (spec.TargetScope is not ("ENEMY_UNIT" or "ENEMY_BATTLEFIELD_UNIT") || EffectiveFieldControllerId(state.PlayerZones, id, card) != player)
                && (spec.TargetScope != "FRIENDLY_DUEL_UNIT" || IsLegendDuelUnit(state, player, id))
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
            || targets.Where((id, index) => !IsBorrowedExhaustTarget(state, intent.PlayerId, id, ability.Spec, source.ObjectId, index)).Any())
            return RejectWithCorePrompts(state, "请选择技能要求的不同合法目标。", ErrorCodes.InvalidTarget);
        var requested = NormalizeOptionalCosts(command.OptionalCosts);
        var discardsHandCard = ability.Spec.DiscardsEquipmentAsCost || ability.Spec.DiscardsHandCardAsCost;
        if (!TryExtractRenektonPlayPayment(requested, out var withoutRenekton, out var renekton)
            || !TryExtractAbilityRunePaymentResourceActions(state, intent.PlayerId, withoutRenekton,
                out var extras, out var resources, out _) || extras.Count != (ability.Spec.Conversion.Length > 0 ? 1 : 0)
                    + (discardsHandCard ? 1 : 0) + ability.Spec.RecycledCardCostCount
                    + (ability.Spec.ExhaustedUnitMinimumMightAsCost.HasValue ? 1 : 0)
                    + (ability.Spec.DestroyedFriendlyCardTypeAsCost.Length > 0 ? 1 : 0)
                    + (ability.Spec.BanishesGraveyardPermanentAsCost ? 1 : 0))
            return RejectWithCorePrompts(state, "所选支付资源行动无效。", ErrorCodes.InvalidTarget);
        var banishCosts = extras.Where(id => id.StartsWith(LegendGraveyardBanishCostRules.Prefix, StringComparison.Ordinal)).ToArray();
        if (ability.Spec.BanishesGraveyardPermanentAsCost
            ? !LegendGraveyardBanishCostRules.TryRead(state, intent.PlayerId, banishCosts, out _)
            : banishCosts.Length != 0)
            return RejectWithCorePrompts(state, "请选择自己废牌堆中的单位或装备支付放逐费用。", ErrorCodes.InvalidTarget);
        var destructionCosts = extras.Where(id => id.StartsWith(DestroyFriendlyPermanentAdditionalCostPrefix, StringComparison.Ordinal)).ToArray();
        if (destructionCosts.Length != (ability.Spec.DestroyedFriendlyCardTypeAsCost.Length > 0 ? 1 : 0)
            || destructionCosts.Any(id => !BorrowedDestructionCostChoices(state, intent.PlayerId, ability.Spec).Contains(id)))
            return RejectWithCorePrompts(state, "请选择合法友方物体支付摧毁费用。", ErrorCodes.InvalidTarget);
        var unitExhaustCosts = extras.Where(id => id.StartsWith(ExhaustFriendlyUnitOptionalCostPrefix, StringComparison.Ordinal)).ToArray();
        if (unitExhaustCosts.Length != (ability.Spec.ExhaustedUnitMinimumMightAsCost.HasValue ? 1 : 0)
            || unitExhaustCosts.Any(id => !BorrowedUnitExhaustCostChoices(state, intent.PlayerId, source.ObjectId, ability.Spec).Contains(id)))
            return RejectWithCorePrompts(state, "请选择合法的活跃友方单位支付休眠费用。", ErrorCodes.InvalidTarget);
        if (ability.Spec.MovesToExhaustedCostUnit && targets.Contains(unitExhaustCosts.Single()[ExhaustFriendlyUnitOptionalCostPrefix.Length..]))
            return RejectWithCorePrompts(state, "移动目标必须是另一名友方单位。", ErrorCodes.InvalidTarget);
        var recycled = extras.Where(id => TryParseRecycleGraveyardCardAdditionalCost(id, out _))
            .Select(id => { TryParseRecycleGraveyardCardAdditionalCost(id, out var card); return card; }).ToArray();
        if (recycled.Length != ability.Spec.RecycledCardCostCount
            || recycled.Distinct(StringComparer.Ordinal).Count() != recycled.Length
            || recycled.Any(id => !CanRecycleBorrowedAbilityCost(state, intent.PlayerId, id, ability.Spec)))
            return RejectWithCorePrompts(state, "请选择自己废牌堆中足够且不同的合法牌支付回收费用。", ErrorCodes.InvalidTarget);
        var discard = extras.Where(id => id.StartsWith("DISCARD_HAND_CARD:", StringComparison.Ordinal)).ToArray();
        if (discard.Length != (discardsHandCard ? 1 : 0)
            || discard.Any(id => !SimpleActivationDiscardChoices(state, intent.PlayerId, ability.Spec.DiscardsHandCardAsCost).Contains(id)))
            return RejectWithCorePrompts(state, "请选择自己手中的合法牌支付弃置费用。", ErrorCodes.InvalidTarget);
        var ordered = renekton is null ? resources : OrderedRenektonPaymentResources(requested, resources, renekton);
        var tax = BorrowedExhaustRules.TargetsPlayer(ability.Spec) ? 0
            : ResolveSpellshieldTargetTaxPower(state, intent.PlayerId, targets, out _);
        var paymentId = PaymentCostRules.BuildPaymentId(state.Tick + 1, "ACTIVATE_ABILITY", intent.PlayerId, source.ObjectId, ability.Id);
        var mana = BorrowedExhaustManaCost(state, intent.PlayerId, ability.Spec);
        var plan = new PaymentCostRules.PaymentPlan(paymentId, "ACTIVATE_ABILITY", intent.PlayerId,
            mana, mana, ability.Spec.Power + tax,
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
        if (ability.Spec.BanishesGraveyardPermanentAsCost
            && !LegendGraveyardBanishCostRules.TryRead(paidState, intent.PlayerId, banishCosts, out _))
            return RejectWithCorePrompts(state, "支付资源行动后所选牌无法支付放逐费用。", ErrorCodes.InvalidTarget);
        if (destructionCosts.Any(id => !BorrowedDestructionCostChoices(paidState, intent.PlayerId, ability.Spec).Contains(id)))
            return RejectWithCorePrompts(state, "支付资源行动后所选物体无法支付摧毁费用。", ErrorCodes.InvalidTarget);
        if (ability.Spec.Kind == "RESOURCE" && destructionCosts.Any(id => !CanStartDestructionResourceGain(
            paidState with { RunePools = payment.RunePools }, intent.PlayerId, source.ObjectId,
            id[DestroyFriendlyPermanentAdditionalCostPrefix.Length..],
            ability.Spec.GeneratedPower + ability.Spec.GeneratedTraits.Values.Sum(), BorrowedExhaustRules.Effect)))
            return RejectWithCorePrompts(state, "资源数量超出支持范围，未支付摧毁费用。", ErrorCodes.UnsupportedCardBehavior);
        if (unitExhaustCosts.Any(id => !BorrowedUnitExhaustCostChoices(paidState, intent.PlayerId, source.ObjectId, ability.Spec).Contains(id)))
            return RejectWithCorePrompts(state, "支付资源行动后所选单位无法支付休眠费用。", ErrorCodes.InvalidTarget);
        if (recycled.Any(id => !CanRecycleBorrowedAbilityCost(paidState, intent.PlayerId, id, ability.Spec)))
            return RejectWithCorePrompts(state, "所选牌已无法支付回收费用。", ErrorCodes.InvalidTarget);
        if (!BorrowedExhaustRules.Abilities(paidState, intent.PlayerId, cards[source.ObjectId]).Any(a => a.Id == ability.Id))
            return RejectWithCorePrompts(state, "支付资源行动后来源已无法使用该技能。", ErrorCodes.InvalidTarget);
        source = cards[source.ObjectId];
        var destructionTriggers = new List<TriggerQueueItemState>();
        var destroyedOwners = new List<string>();
        var rngCursor = state.RngCursor;
        var borrowerDestroyedAsCost = false;
        // All targets bind before nonstandard costs can change their zones.
        var objectTargets = BorrowedExhaustRules.TargetsPlayer(ability.Spec) ? [] : targets;
        var targetGenerations = objectTargets.ToDictionary(id => id, id => cards[id].ObjectGeneration, StringComparer.Ordinal);
        var factories = objectTargets.Where(id => TokenObjectRules.IsToken(cards[id]))
            .ToDictionary(id => id, id => cards[id].TokenFactoryCardNo ?? cards[id].CardNo!, StringComparer.Ordinal);
        var borrowerWasOnBattlefield = zones.Values.Any(z => z.Battlefields.Contains(source.ObjectId, StringComparer.Ordinal));
        var context = new BorrowedExhaustContext(BorrowedExhaustRules.Capture(source, ability.BorrowerText),
            BorrowedExhaustRules.Capture(cards[ability.Donor.ObjectId], ability.DonorText), ability.Spec.Index,
            state.Tick + 1, source.IsEmpowered, factories.Count == 0 ? null : factories, borrowerWasOnBattlefield,
            ability.Spec.EmpoweredMight.HasValue || ability.Spec.ReadyTargetIfSourceEmpowered ? source.IsEmpowered : null,
            ability.Spec.RequiresPlayedArmamentThisTurn,
            ability.Spec.TargetScope == "FRIENDLY_DUEL_UNIT" ? new(state.SpellDuelState.BattlefieldObjectId!,
                cards[state.SpellDuelState.BattlefieldObjectId!].ObjectGeneration) : null);
        foreach (var cost in unitExhaustCosts)
        {
            var id = cost[ExhaustFriendlyUnitOptionalCostPrefix.Length..];
            cards[id] = ExhaustForInstruction(zones, cards, cards[id]);
            events.Add(new("UNIT_EXHAUSTED", "让友方单位休眠以支付获得的技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = id,
                ["wasExhausted"] = false, ["isExhausted"] = true, ["reason"] = "ABILITY_COST" }));
        }
        if (ability.Spec.MovesToExhaustedCostUnit) {
            var costUnit = cards[unitExhaustCosts.Single()[ExhaustFriendlyUnitOptionalCostPrefix.Length..]];
            context = context with { ExhaustedCostUnit = new(new(costUnit.ObjectId, costUnit.ObjectGeneration), costUnit.TokenFactoryCardNo) };
        }
        if (ability.Spec.RemovesSourceEmpowerment)
        {
            var changed = ApplyEmpowerStatusAction(state with { CardObjects = cards },
                new(source.ObjectId, source.ObjectGeneration), false, intent.PlayerId);
            source = changed.CardObjects[source.ObjectId];
            events.AddRange(changed.Events);
            cards[source.ObjectId] = source;
        }
        if (ability.Spec.DestroysSourceAsCost)
        {
            // Exhausting is part of the printed cost and is paid before the
            // source is destroyed. The captured text instance remains usable
            // for the resulting stack item after the zone change.
            source = ExhaustForInstruction(zones, cards, source);
            cards[source.ObjectId] = source;
            var costItem = new StackItemState($"cost-{state.Tick + 1}-{source.ObjectId}-BORROWED",
                intent.PlayerId, source.ObjectId, BorrowedExhaustRules.Effect,
                source.CardNo ?? context.Borrower.PhysicalCardNo, []);
            var removal = ResolveFieldDestructions(state, zones, cards, costItem, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), payment.RunePools,
                objectLocations: locations, explicitDestroyObjectIds: new HashSet<string> { source.ObjectId });
            if (removal.Events.Count == 0)
                return RejectWithCorePrompts(state, "无法支付摧毁来源的费用。", ErrorCodes.InvalidTarget);
            events.AddRange(removal.Events);
            destructionTriggers.AddRange(removal.TriggerQueue);
            destroyedOwners.AddRange(removal.DestroyedUnitOwnerIds);
            payment = payment with { RunePools = removal.RunePools };
        }
        foreach (var cost in destructionCosts)
        {
            var id = cost[DestroyFriendlyPermanentAdditionalCostPrefix.Length..];
            // Exhaust first: a source sacrificed as another printed cost must
            // never be restored from its pre-destruction object afterwards.
            source = ExhaustForInstruction(zones, cards, source);
            cards[source.ObjectId] = source;
            borrowerDestroyedAsCost = id == source.ObjectId;
            var costItem = new StackItemState($"cost-{state.Tick + 1}-{source.ObjectId}-BORROWED-FRIEND",
                intent.PlayerId, source.ObjectId, BorrowedExhaustRules.Effect, context.Borrower.PhysicalCardNo, []);
            var removal = ResolveFieldDestructions(state, zones, cards, costItem, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), payment.RunePools,
                objectLocations: locations, explicitDestroyObjectIds: new HashSet<string> { id });
            if (removal.Events.Count == 0)
                return RejectWithCorePrompts(state, "无法支付所选摧毁费用。", ErrorCodes.InvalidTarget);
            events.AddRange(removal.Events);
            destructionTriggers.AddRange(removal.TriggerQueue);
            destroyedOwners.AddRange(removal.DestroyedUnitOwnerIds);
            payment = payment with { RunePools = removal.RunePools };
        }
        if (discardsHandCard)
        {
            if (!SimpleActivationDiscardChoices(paidState, intent.PlayerId, ability.Spec.DiscardsHandCardAsCost).Contains(discard[0]))
                return RejectWithCorePrompts(state, "所选手牌已无法支付弃置费用。", ErrorCodes.InvalidTarget);
            var discarded = discard[0]["DISCARD_HAND_CARD:".Length..];
            if (!TryDiscardCardFromHand(zones, cards, intent.PlayerId, discarded))
                return RejectWithCorePrompts(state, "无法弃置所选手牌。", ErrorCodes.InvalidTarget);
            events.Add(new("CARD_DISCARDED", "弃置手牌以支付获得的技能费用", new Dictionary<string, object?> {
                ["playerId"] = intent.PlayerId, ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = discarded,
                ["reason"] = "ABILITY_COST", ["destinationZone"] = "GRAVEYARD" }));
            ResolveHandCardsDiscardedReadyPowerTriggers(zones, cards, intent.PlayerId, "CARD_DISCARDED", source.ObjectId, [discarded], events);
        }
        if (recycled.Length > 0)
        {
            var recycling = RecycleGraveyardCards(paidState, zones, cards, intent.PlayerId,
                source.ObjectId, recycled, "ABILITY_COST");
            events.AddRange(recycling.Events);
            rngCursor = recycling.RngCursor;
        }
        if (ability.Spec.BanishesGraveyardPermanentAsCost)
            PayGraveyardPermanentBanishCost(zones, cards, intent.PlayerId, source.ObjectId, ability.Id,
                banishCosts[0][LegendGraveyardBanishCostRules.Prefix.Length..], events, "支付获得的技能的放逐费用");
        if (ability.Spec.Kind == "RESOURCE")
            return ResolveBorrowedResource(state, intent, ability, plan, payment, zones, cards, locations, events, extras,
                source, destructionTriggers, destroyedOwners, borrowerDestroyedAsCost);
        if (!ability.Spec.DestroysSourceAsCost && !borrowerDestroyedAsCost && cards.ContainsKey(source.ObjectId))
            cards[source.ObjectId] = ExhaustForInstruction(zones, cards, source);
        var item = new StackItemState($"STACK-{state.Tick + 1}-{source.ObjectId}-BORROWED", intent.PlayerId,
            source.ObjectId, BorrowedExhaustRules.Effect, context.Borrower.PhysicalCardNo, targets,
            timingContext: StackTimingContextForNewStackItem(state)) {
            BorrowedExhaust = context,
            TargetGenerations = targetGenerations };
        if (ability.Spec.Kind == "DESTROY_DECK_PLAY")
        {
            var printed = BorrowedDestroyDeckPlayDefinition(context)!;
            item = item with { DestroyDeckPlay = new(context.Borrower.Object, item.CardNo!, printed.AbilityId,
                new(targets[0], targetGenerations[targets[0]]), CapturePublicMightFrame(state, state.PlayerZones, state.CardObjects)) };
        }
        var payload = new Dictionary<string, object?> { ["playerId"] = intent.PlayerId, ["controllerId"] = intent.PlayerId,
            ["sourceObjectId"] = source.ObjectId, ["cardNo"] = source.CardNo, ["abilityId"] = ability.Id,
            ["effectKind"] = item.EffectKind, ["targetObjectIds"] = targets.ToArray() };
        events.Add(new("ABILITY_ACTIVATED", "激活获得的技能", payload));
        events.Add(new(source.Tags.Contains(CardObjectTags.EquipmentCard) ? "EQUIPMENT_EXHAUSTED" : "UNIT_EXHAUSTED", "横置来源以支付技能费用", new Dictionary<string, object?>(payload) {
            ["targetObjectId"] = source.ObjectId, ["wasExhausted"] = false, ["isExhausted"] = true }));
        events.Add(new("COST_PAID", "支付技能费用", PaymentCostRules.BuildCostPaidPayload(plan, payment.RunePools, payment.PlayerExperience, payload)));
        events.Add(new("STACK_ITEM_ADDED", "技能加入结算链", new Dictionary<string, object?>(payload) { ["stackItemId"] = item.StackItemId }));
        var effects = source.Tags.Contains(CardObjectTags.UnitCard)
            ? MarkEzrealEnemyTargetsThisTurnForUnitAbility(state, state.UntilEndOfTurnEffects, intent.PlayerId, targets)
            : state.UntilEndOfTurnEffects;
        if (discardsHandCard)
            effects = MarkPlayerDiscardedHandCardsThisTurn(effects, intent.PlayerId, [discard[0]["DISCARD_HAND_CARD:".Length..]], zones);
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = intent.PlayerId,
            TimingState = state.SpellDuelState.IsActive ? TimingStates.SpellDuelClosed : TimingStates.NeutralClosed,
            PriorityPlayerId = intent.PlayerId, PassedPriorityPlayerIds = [],
            PlayerZones = zones, CardObjects = cards, ObjectLocations = ReconcileObjectLocations(locations, zones),
            RunePools = payment.RunePools, PlayerExperience = payment.PlayerExperience, StackItems = [..state.StackItems, item],
            TriggerQueue = state.TriggerQueue.Concat(destructionTriggers).ToArray(), RngCursor = rngCursor,
            DestroyedUnitOwnerIdsThisTurn = MergeDestroyedUnitOwnerIds(state.DestroyedUnitOwnerIdsThisTurn, destroyedOwners),
            UntilEndOfTurnEffects = effects };
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveBorrowedExhaustStack(MatchState state, StackItemState item)
    {
        if (!BorrowedExhaustRules.Valid(state, item)) throw new InvalidOperationException("Invalid captured borrowed skill.");
        var c = item.BorrowedExhaust!;
        var spec = BorrowedExhaustRules.Spec(c.Donor.CardNo, c.AbilityIndex)!;
        // 396/398: only the activated component was inherited. Its donor's
        // play trigger and exile references belong to another text instance.
        if (spec.Kind == "PLAY_LINKED_EXILE") return NoopStackResolutionResult(state);
        if (spec.Kind == "DESTROY_DECK_PLAY") return ResolveDestroyDeckPlay(state, item);
        if (spec.MovesToExhaustedCostUnit)
            return ResolveMovementToExhaustedCostUnit(state, item, c.ExhaustedCostUnit!,
                (frame, id) => IsBorrowedExhaustTarget(frame, item.ControllerId, id, spec, item.SourceObjectId));
        if (spec.Kind == "RETURN_UNIT_FREE_PLAY")
            return ResolveReturnUnitThenFreePlay(state, item, "获得的返手后打出技能", BorrowedExhaustRules.Effect,
                id => IsBorrowedExhaustTarget(state, item.ControllerId, id, spec, item.SourceObjectId));
        if (spec.Kind is "RETURN_TO_HAND" or "RETURN_AND_CREATE_GOLD")
            return ResolveBorrowedReturnSkill(state, item, spec);
        if (spec.Kind is "ATTACH_ARMAMENT" or "REATTACH_ARMAMENT")
            return ResolveBorrowedArmamentSkill(state, item, spec);
        if (spec.Kind == "GRANT_NEXT_SPELL_ECHO") return ResolveNextSpellEchoGrant(state, item);
        if (spec.Kind == "GRANT_NEXT_UNIT_READY") return ResolveActivatedNextUnitReady(state, item);
        if (spec.Kind == "GRANT_NEXT_SPELL_DAMAGE") return ResolveActivatedNextSpellDamage(state, item);
        if (spec.Kind == "GRANT_PAID_RECALL") return ResolveActivatedPaidRecall(state, item);
        if (spec.Kind == "COMMAND_OPPONENT_MOVEMENT")
            return ResolveCommandOpponentMovement(state, item, c.Borrower.Object, "获得的移动技能", BorrowedExhaustRules.Effect);
        if (spec.Kind == "TRANSFER_SOURCE_AND_RECALL")
            return ResolveTransferSourceAndRecall(state, item, c.Borrower.Object, BorrowedExhaustRules.Effect);
        if (spec.Kind == "GUESS_DECK_TOP")
            return ResolveGuessedDeckTop(state, item, "获得的猜牌技能", BorrowedExhaustRules.Effect);
        if (spec.Kind == "CREATE_UNIT_TOKEN")
        {
            if (BeginTokenReplacement(state, item) is { } replacement) return replacement;
            var factory = P6TokenFactoryCatalog.GetAll().First(f => f.TokenFamilyName == spec.TokenName
                && f.DefaultPower == spec.TokenMight && f.Tags.Contains(CardObjectTags.UnitCard)
                && (spec.TokenKeyword.Length == 0 || f.Tags.Contains(spec.TokenKeyword)));
            return CreateUnitTokenBatch(state, item, factory.CardNo, spec.TokenCount + AdditionalUnitTokens(item),
                spec.TokensEnterReady, instructionAllowsLocationChoice: !spec.TokensToBase);
        }
        if (spec.Kind is "MOVE_TO_BASE" or "MOVE_BETWEEN_BASE_AND_BATTLEFIELD" or "MOVE_DUEL_UNIT_TO_BASE")
            return ResolveBorrowedMovement(state, item, spec);
        if (spec.Kind == "CREATE_BATTLEFIELD_EQUIPMENT_TOKEN")
            return ResolveBorrowedBattlefieldEquipmentTokens(state, item, spec);
        if (spec.Kind == "INSIGHT_DRAW_EXPERIENCE")
            return item.InsightCompleted ? FinishInsight(state, item)
                : BeginInsight(state, item, NoopStackResolutionResult(state), spec.InsightCount);
        if (spec.Kind == "DISCARD_DRAW") return ResolvePrivateDiscardDraw(state, item, 1, spec.DrawCount);
        if (spec.Kind == "SCORE")
            return ResolveSkillScoreStack(state, item, spec.ScoreAmount, BorrowedExhaustRules.Effect, "获得的得分技能结算");
        var cards = state.CardObjects.ToDictionary();
        var zones = NormalizeZonesForSeats(state);
        var destroyed = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<GameEvent> { new("ABILITY_RESOLVED", "获得的技能结算", new Dictionary<string, object?> {
            ["sourceObjectId"] = item.SourceObjectId, ["stackItemId"] = item.StackItemId }) };
        if (spec.Kind == "CREATE_GOLD")
        {
            CreateBaseEquipmentTokens(zones, cards,
                new(c.Donor.CardNo, "获得的指示物技能", 0, item.EffectKind, 0, 0,
                    CreatedBaseEquipmentTokenCount: 1, CreatedBaseEquipmentTokenName: "金币",
                    CreatedBaseEquipmentTokenTags: CardObjectTags.EquipmentCard, CreatedBaseEquipmentTokenIsExhausted: true), item, events);
            return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        if (spec.Kind == "DESTROY_SELF")
        {
            if (!cards.TryGetValue(c.Borrower.Object.ObjectId, out var self)
                || self.ObjectGeneration != c.Borrower.Object.Generation || !IsObjectOnField(zones, self.ObjectId))
                return NoopStackResolutionResult(state) with { Events = events };
            var removal = ResolveFieldDestructions(state, zones, cards, item, new HashSet<string>(),
                state.DestroyedUnitOwnerIdsThisTurn.ToHashSet(StringComparer.Ordinal), state.RunePools,
                objectLocations: state.ObjectLocations, explicitDestroyObjectIds: new HashSet<string> { self.ObjectId },
                explicitDestroyReason: "SKILL_EFFECT");
            events.AddRange(removal.Events);
            return NoopStackResolutionResult(state) with { CardObjects = cards, PlayerZones = zones,
                RunePools = removal.RunePools, TriggerQueue = removal.TriggerQueue,
                DestroyedUnitOwnerIds = removal.DestroyedUnitOwnerIds, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
        }
        using (new MightInstructionScope(state, zones, cards, events))
        foreach (var id in spec.Kind == "BOON_SELF" ? new[] { c.Borrower.Object.ObjectId } : item.TargetObjectIds)
        {
            var current = state with { CardObjects = cards };
            if (spec.Kind == "BOON_SELF")
            {
                if (!cards.TryGetValue(id, out var self) || self.ObjectGeneration != c.Borrower.Object.Generation
                    || !IsFaceUpNonStandbyUnit(self) || !IsObjectOnField(zones, id)) continue;
            }
            else if (!StackObjectRules.ReferencesCurrentObject(current, item, id)
                || !IsBorrowedExhaustTarget(current, item.ControllerId, id, spec, item.SourceObjectId)) continue;
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
            else if (spec.Kind is "BOON_UNIT" or "BOON_SELF"
                || spec.Kind == "BOON_OR_MIGHT" && !target.Tags.Contains(CardObjectTags.Boon))
            {
                var behavior = new CardBehaviorDefinition(c.Donor.CardNo, "获得的增益技能", 0, item.EffectKind, 0, 1,
                    TargetScope: CardTargetScopes.AnyUnit, GrantsBoon: true);
                cards[id] = ApplyBoon(target, behavior, item, id, out var applied, zones, cards);
                events.AddRange(applied);
            }
            else if (spec.Kind == "EMPOWER_EQUIPMENT")
            {
                var empowered = ApplyEmpowerStatusAction(current, new(id, target.ObjectGeneration), true, item.ControllerId);
                cards[id] = empowered.CardObjects[id];
                events.AddRange(empowered.Events);
            }
            else if (spec.Kind == "ROAM_UNIT")
                GrantLegendRoam(cards, id, item.ControllerId, item.SourceObjectId,
                    item.EffectKind, events);
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
                // CN 359.3.e.12: a source removed by a different response has
                // no available Might. Ignore the instruction, including bonuses.
                var baseDamage = spec.Damage;
                if (spec.DamageFromSourceMight)
                {
                    if (!cards.TryGetValue(item.SourceObjectId, out var damageSource)
                        || damageSource.ObjectGeneration != c.Borrower.Object.Generation
                        || !IsFaceUpNonStandbyUnit(damageSource) || !IsObjectOnField(zones, damageSource.ObjectId)) continue;
                    baseDamage = ResolveCurrentFieldUnitPower(current, damageSource);
                }
                var amount = ApplySpellSkillDamageBonuses(current, item.ControllerId, id, baseDamage, item);
                var damage = ApplyDamageToCardObject(current, cards, item.ControllerId, id, amount, destroyed,
                    state.UntilEndOfTurnEffects.Contains(PreventSpellAndSkillDamageThisTurnEffectId), PreventSpellAndSkillDamageThisTurnEffectId);
                events.Add(new("DAMAGE_APPLIED", "获得的技能造成伤害", BuildDamagePayload(item.SourceObjectId, id, damage)));
            }
            else
            {
                var sourceEmpowered = cards.TryGetValue(item.SourceObjectId, out var source)
                    && source.ObjectGeneration == c.Borrower.Object.Generation && IsObjectOnField(zones, source.ObjectId)
                        ? source.IsEmpowered : c.LastKnownSourceEmpowered;
                var amount = spec.Kind == "BOON_OR_MIGHT" ? spec.ExistingBoonMight
                    : sourceEmpowered == true && spec.EmpoweredMight is { } stronger ? stronger : spec.Might;
                var behavior = new CardBehaviorDefinition(c.Donor.CardNo, "获得的战力技能", 0, item.EffectKind, 0, 1,
                    PowerModifierAmount: amount, MinimumPowerAfterModifier: spec.MinimumMight);
                cards[id] = ApplyPowerModifier(current, target, behavior, item, id, amount, out var modified);
                events.Add(modified);
            }
        }
        if (spec.Kind == "DRAW" && spec.DrawCount > 0)
        {
            var draw = ApplyDrawToPlayer(state, zones, cards, state.PlayerScores, item.ControllerId,
                spec.DrawCount, state.RngCursor, events, deferNormalVictory: true);
            if (spec.RuneCallCount > 0 && draw.WinnerPlayerId is null)
            {
                var called = CallRunes(zones, cards, item.ControllerId, spec.RuneCallCount);
                events.Add(new("RUNES_CALLED", $"{item.ControllerId} 召出 {called.CalledRuneObjectIds.Count} 枚休眠符文",
                    new Dictionary<string, object?> {
                        ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                        ["count"] = called.CalledRuneObjectIds.Count, ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray(),
                        ["reason"] = item.EffectKind }));
            }
            return NoopStackResolutionResult(state) with { CardObjects = cards, PlayerZones = zones,
                PlayerScores = draw.PlayerScores, WinnerPlayerId = draw.WinnerPlayerId,
                RngCursor = draw.RngCursor, Events = events,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };
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

    private static ResolutionResult RecordBorrowedSourceStatus(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = result.Events.ToList();
        var changed = false;
        var stack = result.State.StackItems.Select(item => {
            if (item.BorrowedExhaust is not { LastKnownSourceEmpowered: not null } context) return item;
            var binding = context.Borrower.Object;
            var empowered = context.LastKnownSourceEmpowered;
            if (result.State.CardObjects.TryGetValue(binding.ObjectId, out var current)
                && current.ObjectGeneration == binding.Generation && IsObjectOnField(result.State.PlayerZones, binding.ObjectId))
                empowered = current.IsEmpowered;
            else if (before.CardObjects.TryGetValue(binding.ObjectId, out var previous)
                && previous.ObjectGeneration == binding.Generation && IsObjectOnField(before.PlayerZones, binding.ObjectId))
            {
                empowered = previous.IsEmpowered;
                foreach (var ev in result.Events)
                    if (ev.Kind is "OBJECT_EMPOWERED" or "OBJECT_EMPOWER_REMOVED"
                        && EventPayloadString(ev, "sourceObjectId") == binding.ObjectId
                        && ev.Payload.TryGetValue("objectGeneration", out var generation)
                        && Convert.ToInt64(generation, System.Globalization.CultureInfo.InvariantCulture) == binding.Generation)
                        empowered = ev.Kind == "OBJECT_EMPOWERED";
            }
            if (empowered == context.LastKnownSourceEmpowered) return item;
            changed = true;
            events.Add(new("ABILITY_SOURCE_STATUS_CAPTURED", "保留获得的技能来源状态", new Dictionary<string, object?> {
                ["stackItemId"] = item.StackItemId,
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(result.State, binding.ObjectId, ProjectionViewers.Spectator),
                ["sourceGeneration"] = binding.Generation, ["isEmpowered"] = empowered }));
            return item with { BorrowedExhaust = context with { LastKnownSourceEmpowered = empowered } };
        }).ToArray();
        if (!changed) return result;
        var state = result.State with { StackItems = stack };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
